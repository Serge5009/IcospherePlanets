using System.Collections.Generic;
using UnityEngine;

public static class HexSphereBuilder
{
    public static void GenerateTopology(int subdivisions, out Mesh mesh, out Vector3[] cellCenters)
    {
        List<Vector3> icoVerts = new List<Vector3>();
        List<int> icoTris = new List<int>();
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();

        List<Vector3> visualVerts = new List<Vector3>();
        List<int> visualTris = new List<int>();
        List<Vector2> visualUV2 = new List<Vector2>();
        List<Vector2> visualUV3 = new List<Vector2>();

        CreateIcosahedron(1f, icoVerts, icoTris);
        for (int i = 0; i < subdivisions; i++) Subdivide(1f, icoVerts, icoTris, midpointCache);

        GenerateDualMesh(1f, icoVerts, icoTris, visualVerts, visualTris, visualUV2, visualUV3);

        mesh = new Mesh();
        mesh.name = $"HexSphere_Sub{subdivisions}";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = visualVerts.ToArray();
        mesh.triangles = visualTris.ToArray();
        mesh.uv2 = visualUV2.ToArray();
        mesh.uv3 = visualUV3.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        cellCenters = icoVerts.ToArray();
    }

    private static void CreateIcosahedron(float radius, List<Vector3> verts, List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        verts.Add(new Vector3(-1, t, 0).normalized * radius); verts.Add(new Vector3(1, t, 0).normalized * radius);
        verts.Add(new Vector3(-1, -t, 0).normalized * radius); verts.Add(new Vector3(1, -t, 0).normalized * radius);
        verts.Add(new Vector3(0, -1, t).normalized * radius); verts.Add(new Vector3(0, 1, t).normalized * radius);
        verts.Add(new Vector3(0, -1, -t).normalized * radius); verts.Add(new Vector3(0, 1, -t).normalized * radius);
        verts.Add(new Vector3(t, 0, -1).normalized * radius); verts.Add(new Vector3(t, 0, 1).normalized * radius);
        verts.Add(new Vector3(-t, 0, -1).normalized * radius); verts.Add(new Vector3(-t, 0, 1).normalized * radius);

        int[] tData = {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        tris.AddRange(tData);
    }

    private static void Subdivide(float radius, List<Vector3> verts, List<int> tris, Dictionary<long, int> cache)
    {
        List<int> newTriangles = new List<int>();
        cache.Clear();
        for (int i = 0; i < tris.Count; i += 3)
        {
            int v1 = tris[i], v2 = tris[i + 1], v3 = tris[i + 2];
            int a = GetMidpoint(v1, v2, radius, verts, cache);
            int b = GetMidpoint(v2, v3, radius, verts, cache);
            int c = GetMidpoint(v3, v1, radius, verts, cache);
            newTriangles.Add(v1); newTriangles.Add(a); newTriangles.Add(c);
            newTriangles.Add(v2); newTriangles.Add(b); newTriangles.Add(a);
            newTriangles.Add(v3); newTriangles.Add(c); newTriangles.Add(b);
            newTriangles.Add(a); newTriangles.Add(b); newTriangles.Add(c);
        }
        tris.Clear();
        tris.AddRange(newTriangles);
    }

    private static int GetMidpoint(int v1, int v2, float radius, List<Vector3> verts, Dictionary<long, int> cache)
    {
        bool firstIsSmaller = v1 < v2;
        long smallerIndex = firstIsSmaller ? v1 : v2;
        long greaterIndex = firstIsSmaller ? v2 : v1;
        long key = (smallerIndex << 32) + greaterIndex;
        if (cache.TryGetValue(key, out int midpointIndex)) return midpointIndex;
        Vector3 midpoint = (verts[v1] + verts[v2]).normalized * radius;
        int newIndex = verts.Count;
        verts.Add(midpoint);
        cache.Add(key, newIndex);
        return newIndex;
    }

    private static void GenerateDualMesh(float radius, List<Vector3> icoVerts, List<int> icoTris, List<Vector3> visualVerts, List<int> visualTris, List<Vector2> visualUV2, List<Vector2> visualUV3)
    {
        List<Vector3> centroids = new List<Vector3>();
        Dictionary<int, List<int>> vertexToCentroids = new Dictionary<int, List<int>>();
        for (int i = 0; i < icoVerts.Count; i++) vertexToCentroids[i] = new List<int>();

        int centroidIndex = 0;
        for (int i = 0; i < icoTris.Count; i += 3)
        {
            int v1 = icoTris[i], v2 = icoTris[i + 1], v3 = icoTris[i + 2];
            Vector3 centroid = ((icoVerts[v1] + icoVerts[v2] + icoVerts[v3]) / 3f).normalized * radius;
            centroids.Add(centroid);
            vertexToCentroids[v1].Add(centroidIndex); vertexToCentroids[v2].Add(centroidIndex); vertexToCentroids[v3].Add(centroidIndex);
            centroidIndex++;
        }

        for (int i = 0; i < icoVerts.Count; i++)
        {
            Vector3 cellCenter = icoVerts[i];
            List<int> connectedCentroids = vertexToCentroids[i];

            Vector3 normal = cellCenter.normalized;
            Vector3 referenceDirection = (centroids[connectedCentroids[0]] - cellCenter).normalized;
            connectedCentroids.Sort((a, b) =>
            {
                Vector3 dirA = (centroids[a] - cellCenter).normalized;
                Vector3 dirB = (centroids[b] - cellCenter).normalized;
                return Vector3.SignedAngle(referenceDirection, dirA, normal).CompareTo(Vector3.SignedAngle(referenceDirection, dirB, normal));
            });

            float uvX = i % 2000;
            float uvY = Mathf.FloorToInt(i / 2000f);
            Vector2 encodedId = new Vector2(uvX, uvY);

            int centerVertexIndex = visualVerts.Count;
            visualVerts.Add(cellCenter);
            visualUV2.Add(encodedId);
            visualUV3.Add(new Vector2(0, 0));

            int perimeterStartIndex = visualVerts.Count;
            for (int j = 0; j < connectedCentroids.Count; j++)
            {
                visualVerts.Add(centroids[connectedCentroids[j]]);
                visualUV2.Add(encodedId);
                visualUV3.Add(new Vector2(1, 0));
            }

            int count = connectedCentroids.Count;
            for (int j = 0; j < count; j++)
            {
                int nextIndex = (j + 1) % count;
                visualTris.Add(centerVertexIndex);
                visualTris.Add(perimeterStartIndex + j);
                visualTris.Add(perimeterStartIndex + nextIndex);
            }
        }
    }
}