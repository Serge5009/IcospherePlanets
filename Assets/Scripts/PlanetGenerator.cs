using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetGenerator : MonoBehaviour
{
    [Header("Planet Settings")]
    [Min(1f)]
    public float radius = 10f;

    [Range(0, 7)]
    public int subdivisions = 3;

    private MeshFilter meshFilter;
    private List<Vector3> vertices;
    private List<int> triangles;
    private Dictionary<long, int> midpointCache;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        GeneratePlanet();
    }

    public void GeneratePlanet()
    {
        vertices = new List<Vector3>();
        triangles = new List<int>();
        midpointCache = new Dictionary<long, int>();

        CreateIcosahedron();

        for (int i = 0; i < subdivisions; i++)
        {
            Subdivide();
        }

        BuildMesh();
    }

    private void CreateIcosahedron()
    {
        // The Golden Ratio
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        // Base 12 vertices of an icosahedron
        AddVertex(new Vector3(-1, t, 0));
        AddVertex(new Vector3(1, t, 0));
        AddVertex(new Vector3(-1, -t, 0));
        AddVertex(new Vector3(1, -t, 0));

        AddVertex(new Vector3(0, -1, t));
        AddVertex(new Vector3(0, 1, t));
        AddVertex(new Vector3(0, -1, -t));
        AddVertex(new Vector3(0, 1, -t));

        AddVertex(new Vector3(t, 0, -1));
        AddVertex(new Vector3(t, 0, 1));
        AddVertex(new Vector3(-t, 0, -1));
        AddVertex(new Vector3(-t, 0, 1));

        // 20 faces (triangles) with correct clockwise winding order for Unity
        // 5 faces around point 0
        AddTriangle(0, 11, 5);
        AddTriangle(0, 5, 1);
        AddTriangle(0, 1, 7);
        AddTriangle(0, 7, 10);
        AddTriangle(0, 10, 11);

        // 5 adjacent faces
        AddTriangle(1, 5, 9);
        AddTriangle(5, 11, 4);
        AddTriangle(11, 10, 2);
        AddTriangle(10, 7, 6);
        AddTriangle(7, 1, 8);

        // 5 faces around point 3
        AddTriangle(3, 9, 4);
        AddTriangle(3, 4, 2);
        AddTriangle(3, 2, 6);
        AddTriangle(3, 6, 8);
        AddTriangle(3, 8, 9);

        // 5 adjacent faces
        AddTriangle(4, 9, 5);
        AddTriangle(2, 4, 11);
        AddTriangle(6, 2, 10);
        AddTriangle(8, 6, 7);
        AddTriangle(9, 8, 1);
    }

    private void Subdivide()
    {
        List<int> newTriangles = new List<int>();
        midpointCache.Clear();

        // Iterate through existing triangles and split each into 4 new triangles
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];

            int a = GetMidpoint(v1, v2);
            int b = GetMidpoint(v2, v3);
            int c = GetMidpoint(v3, v1);

            // Triangle 1 (Top)
            newTriangles.Add(v1);
            newTriangles.Add(a);
            newTriangles.Add(c);

            // Triangle 2 (Bottom Right)
            newTriangles.Add(v2);
            newTriangles.Add(b);
            newTriangles.Add(a);

            // Triangle 3 (Bottom Left)
            newTriangles.Add(v3);
            newTriangles.Add(c);
            newTriangles.Add(b);

            // Triangle 4 (Center)
            newTriangles.Add(a);
            newTriangles.Add(b);
            newTriangles.Add(c);
        }

        triangles = newTriangles;
    }

    private int GetMidpoint(int v1, int v2)
    {
        // Ensure the smaller index is always first to create a consistent dictionary key
        bool firstIsSmaller = v1 < v2;
        long smallerIndex = firstIsSmaller ? v1 : v2;
        long greaterIndex = firstIsSmaller ? v2 : v1;

        // Bitwise shift to combine two 32-bit ints into a single 64-bit long key
        long key = (smallerIndex << 32) + greaterIndex;

        if (midpointCache.TryGetValue(key, out int midpointIndex))
        {
            return midpointIndex;
        }

        // Calculate the midpoint, normalize it to push it to the sphere surface, and scale by radius
        Vector3 point1 = vertices[v1];
        Vector3 point2 = vertices[v2];
        Vector3 midpoint = (point1 + point2).normalized * radius;

        int newIndex = vertices.Count;
        vertices.Add(midpoint);
        midpointCache.Add(key, newIndex);

        return newIndex;
    }

    private void AddVertex(Vector3 vertex)
    {
        // Normalize and scale the base icosahedron vertices
        vertices.Add(vertex.normalized * radius);
    }

    private void AddTriangle(int v1, int v2, int v3)
    {
        triangles.Add(v1);
        triangles.Add(v2);
        triangles.Add(v3);
    }

    private void BuildMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Icosphere";

        // Crucial: Allows meshes with more than 65,535 vertices
        mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }
}