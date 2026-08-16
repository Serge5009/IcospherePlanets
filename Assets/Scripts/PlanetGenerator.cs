using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    private MeshFilter meshFilter;

    private List<Vector3> icoVertices;
    private List<int> icoTriangles;
    private Dictionary<long, int> midpointCache;

    private List<Vector3> visualVertices;
    private List<int> visualTriangles;

    private List<Vector2> visualUV2;

    private List<Vector2> visualUV3;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    public void Generate(Planet planet, int subdivisions)
    {
        icoVertices = new List<Vector3>();
        icoTriangles = new List<int>();
        midpointCache = new Dictionary<long, int>();

        visualVertices = new List<Vector3>();
        visualTriangles = new List<int>();
        visualUV2 = new List<Vector2>();
        visualUV3 = new List<Vector2>();

        CreateIcosahedron(planet.unityRadius);

        for (int i = 0; i < subdivisions; i++)
        {
            Subdivide(planet.unityRadius);
        }

        GenerateDualMesh(planet.unityRadius);
        BuildMesh();

        InitializeCellData(planet);
        planet.InitializeVisuals();
    }

    private void CreateIcosahedron(float radius)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        AddIcoVertex(new Vector3(-1, t, 0), radius);
        AddIcoVertex(new Vector3(1, t, 0), radius);
        AddIcoVertex(new Vector3(-1, -t, 0), radius);
        AddIcoVertex(new Vector3(1, -t, 0), radius);

        AddIcoVertex(new Vector3(0, -1, t), radius);
        AddIcoVertex(new Vector3(0, 1, t), radius);
        AddIcoVertex(new Vector3(0, -1, -t), radius);
        AddIcoVertex(new Vector3(0, 1, -t), radius);

        AddIcoVertex(new Vector3(t, 0, -1), radius);
        AddIcoVertex(new Vector3(t, 0, 1), radius);
        AddIcoVertex(new Vector3(-t, 0, -1), radius);
        AddIcoVertex(new Vector3(-t, 0, 1), radius);

        AddIcoTriangle(0, 11, 5); AddIcoTriangle(0, 5, 1); AddIcoTriangle(0, 1, 7); AddIcoTriangle(0, 7, 10); AddIcoTriangle(0, 10, 11);
        AddIcoTriangle(1, 5, 9); AddIcoTriangle(5, 11, 4); AddIcoTriangle(11, 10, 2); AddIcoTriangle(10, 7, 6); AddIcoTriangle(7, 1, 8);
        AddIcoTriangle(3, 9, 4); AddIcoTriangle(3, 4, 2); AddIcoTriangle(3, 2, 6); AddIcoTriangle(3, 6, 8); AddIcoTriangle(3, 8, 9);
        AddIcoTriangle(4, 9, 5); AddIcoTriangle(2, 4, 11); AddIcoTriangle(6, 2, 10); AddIcoTriangle(8, 6, 7); AddIcoTriangle(9, 8, 1);
    }

    private void Subdivide(float radius)
    {
        List<int> newTriangles = new List<int>();
        midpointCache.Clear();

        for (int i = 0; i < icoTriangles.Count; i += 3)
        {
            int v1 = icoTriangles[i];
            int v2 = icoTriangles[i + 1];
            int v3 = icoTriangles[i + 2];

            int a = GetMidpoint(v1, v2, radius);
            int b = GetMidpoint(v2, v3, radius);
            int c = GetMidpoint(v3, v1, radius);

            newTriangles.Add(v1); newTriangles.Add(a); newTriangles.Add(c);
            newTriangles.Add(v2); newTriangles.Add(b); newTriangles.Add(a);
            newTriangles.Add(v3); newTriangles.Add(c); newTriangles.Add(b);
            newTriangles.Add(a); newTriangles.Add(b); newTriangles.Add(c);
        }

        icoTriangles = newTriangles;
    }

    private int GetMidpoint(int v1, int v2, float radius)
    {
        bool firstIsSmaller = v1 < v2;
        long smallerIndex = firstIsSmaller ? v1 : v2;
        long greaterIndex = firstIsSmaller ? v2 : v1;
        long key = (smallerIndex << 32) + greaterIndex;

        if (midpointCache.TryGetValue(key, out int midpointIndex))
        {
            return midpointIndex;
        }

        Vector3 midpoint = (icoVertices[v1] + icoVertices[v2]).normalized * radius;
        int newIndex = icoVertices.Count;
        icoVertices.Add(midpoint);
        midpointCache.Add(key, newIndex);

        return newIndex;
    }

    private void AddIcoVertex(Vector3 vertex, float radius)
    {
        icoVertices.Add(vertex.normalized * radius);
    }

    private void AddIcoTriangle(int v1, int v2, int v3)
    {
        icoTriangles.Add(v1);
        icoTriangles.Add(v2);
        icoTriangles.Add(v3);
    }

    private void GenerateDualMesh(float radius)
    {
        List<Vector3> centroids = new List<Vector3>();
        Dictionary<int, List<int>> vertexToCentroids = new Dictionary<int, List<int>>();

        for (int i = 0; i < icoVertices.Count; i++)
        {
            vertexToCentroids[i] = new List<int>();
        }

        int centroidIndex = 0;
        for (int i = 0; i < icoTriangles.Count; i += 3)
        {
            int v1 = icoTriangles[i];
            int v2 = icoTriangles[i + 1];
            int v3 = icoTriangles[i + 2];

            Vector3 centroid = ((icoVertices[v1] + icoVertices[v2] + icoVertices[v3]) / 3f).normalized * radius;
            centroids.Add(centroid);

            vertexToCentroids[v1].Add(centroidIndex);
            vertexToCentroids[v2].Add(centroidIndex);
            vertexToCentroids[v3].Add(centroidIndex);

            centroidIndex++;
        }

        for (int i = 0; i < icoVertices.Count; i++)
        {
            Vector3 cellCenter = icoVertices[i];
            List<int> connectedCentroids = vertexToCentroids[i];

            SortCentroidsClockwise(cellCenter, connectedCentroids, centroids);
            CreateVisualCell(i, cellCenter, connectedCentroids, centroids);
        }
    }

    private void SortCentroidsClockwise(Vector3 center, List<int> connectedCentroids, List<Vector3> centroids)
    {
        Vector3 normal = center.normalized;
        Vector3 referenceDirection = (centroids[connectedCentroids[0]] - center).normalized;

        connectedCentroids.Sort((a, b) =>
        {
            Vector3 dirA = (centroids[a] - center).normalized;
            Vector3 dirB = (centroids[b] - center).normalized;

            float angleA = Vector3.SignedAngle(referenceDirection, dirA, normal);
            float angleB = Vector3.SignedAngle(referenceDirection, dirB, normal);

            return angleA.CompareTo(angleB);
        });
    }

    private void CreateVisualCell(int cellId, Vector3 center, List<int> connectedCentroids, List<Vector3> centroids)
    {
        float uvX = cellId % 2000;
        float uvY = Mathf.FloorToInt(cellId / 2000f);
        Vector2 encodedId = new Vector2(uvX, uvY);

        int centerVertexIndex = visualVertices.Count;
        visualVertices.Add(center);
        visualUV2.Add(encodedId);
        visualUV3.Add(new Vector2(0, 0));

        int perimeterStartIndex = visualVertices.Count;
        for (int i = 0; i < connectedCentroids.Count; i++)
        {
            visualVertices.Add(centroids[connectedCentroids[i]]);
            visualUV2.Add(encodedId);
            visualUV3.Add(new Vector2(1, 0));
        }

        int count = connectedCentroids.Count;
        for (int i = 0; i < count; i++)
        {
            int nextIndex = (i + 1) % count;
            visualTriangles.Add(centerVertexIndex);
            visualTriangles.Add(perimeterStartIndex + i);
            visualTriangles.Add(perimeterStartIndex + nextIndex);
        }
    }

    private void BuildMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Goldberg Polyhedron";
        mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(visualVertices);
        mesh.SetTriangles(visualTriangles, 0);

        mesh.SetUVs(1, visualUV2);
        mesh.SetUVs(2, visualUV3);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }

    private void InitializeCellData(Planet planet)
    {
        planet.cells = new Cell[icoVertices.Count];

        for (int i = 0; i < icoVertices.Count; i++)
        {
            planet.cells[i] = new Cell
            {
                id = i,
                localPosition = icoVertices[i]
            };
        }
    }
}