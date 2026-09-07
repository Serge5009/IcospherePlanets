using System.Collections.Generic;
using UnityEngine;

public static class HexSphereBuilder
{
    public static void GenerateTemplateData(
        int subdivisions, int relaxationPasses,
        float stretchMax, float macroStrength, float macroFreq,
        float microStrength, float microFreq, int microOctaves,
        out HexSphereVariant variant, out int[] offsets, out byte[] counts, out int[] neighbors)
    {
        List<Vector3> icoVerts = new List<Vector3>();
        List<int> icoTris = new List<int>();
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();

        CreateIcosahedron(1f, icoVerts, icoTris);
        for (int i = 0; i < subdivisions; i++) Subdivide(1f, icoVerts, icoTris, midpointCache);

        List<Vector3> centroids = new List<Vector3>();
        Dictionary<int, List<int>> vertexToCentroids = new Dictionary<int, List<int>>();
        for (int i = 0; i < icoVerts.Count; i++) vertexToCentroids[i] = new List<int>();

        int centroidIndex = 0;
        for (int i = 0; i < icoTris.Count; i += 3)
        {
            int v1 = icoTris[i], v2 = icoTris[i + 1], v3 = icoTris[i + 2];
            Vector3 centroid = ((icoVerts[v1] + icoVerts[v2] + icoVerts[v3]) / 3f).normalized;
            centroids.Add(centroid);
            vertexToCentroids[v1].Add(centroidIndex);
            vertexToCentroids[v2].Add(centroidIndex);
            vertexToCentroids[v3].Add(centroidIndex);
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
        }

        BuildNeighborGraph(icoVerts.Count, icoTris, out offsets, out counts, out neighbors);

        for (int pass = 0; pass < relaxationPasses; pass++)
        {
            Vector3[] newCenters = new Vector3[icoVerts.Count];
            for (int i = 0; i < icoVerts.Count; i++)
            {
                Vector3 avg = Vector3.zero;
                List<int> perim = vertexToCentroids[i];
                foreach (int p in perim) avg += centroids[p];
                newCenters[i] = (avg / perim.Count).normalized;
            }

            for (int i = 0; i < icoVerts.Count; i++) icoVerts[i] = newCenters[i];

            centroids.Clear();
            for (int i = 0; i < icoTris.Count; i += 3)
            {
                centroids.Add(((icoVerts[icoTris[i]] + icoVerts[icoTris[i + 1]] + icoVerts[icoTris[i + 2]]) / 3f).normalized);
            }
        }

        if (stretchMax > 0f || macroStrength > 0f || microStrength > 0f)
        {
            Vector3 stretch = new Vector3(
                Random.Range(1f - stretchMax, 1f + stretchMax),
                Random.Range(1f - stretchMax, 1f + stretchMax),
                Random.Range(1f - stretchMax, 1f + stretchMax)
            );

            float volume = stretch.x * stretch.y * stretch.z;
            float correction = Mathf.Pow(1f / volume, 1f / 3f);
            stretch *= correction;

            float noiseOffset = Random.Range(0f, 1000f);

            for (int i = 0; i < icoVerts.Count; i++)
            {
                icoVerts[i] = SculptVertex(icoVerts[i], stretch, macroStrength, macroFreq, microStrength, microFreq, microOctaves, noiseOffset);
            }

            for (int i = 0; i < centroids.Count; i++)
            {
                centroids[i] = SculptVertex(centroids[i], stretch, macroStrength, macroFreq, microStrength, microFreq, microOctaves, noiseOffset);
            }
        }

        List<Vector3> visualVerts = new List<Vector3>();
        List<int> visualTris = new List<int>();
        List<Vector2> visualUV2 = new List<Vector2>();
        List<Vector2> visualUV3 = new List<Vector2>();

        float[] areaFractions = new float[icoVerts.Count];
        float totalArea = 0f;

        for (int i = 0; i < icoVerts.Count; i++)
        {
            List<int> perim = vertexToCentroids[i];
            int count = perim.Count;

            float uvX = i % 2000;
            float uvY = Mathf.FloorToInt(i / 2000f);
            Vector2 encodedId = new Vector2(uvX, uvY);

            int startIndex = visualVerts.Count;
            float cellArea = 0f;

            for (int j = 0; j < count; j++)
            {
                visualVerts.Add(centroids[perim[j]]);
                visualUV2.Add(encodedId);
                visualUV3.Add(new Vector2(1, 0));
            }

            for (int j = 1; j < count - 1; j++)
            {
                visualTris.Add(startIndex);
                visualTris.Add(startIndex + j);
                visualTris.Add(startIndex + j + 1);

                Vector3 v0 = centroids[perim[0]];
                Vector3 v1 = centroids[perim[j]];
                Vector3 v2 = centroids[perim[j + 1]];
                cellArea += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            }

            areaFractions[i] = cellArea;
            totalArea += cellArea;
        }

        for (int i = 0; i < areaFractions.Length; i++)
        {
            areaFractions[i] /= totalArea;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"HexSphere_Sub{subdivisions}";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = visualVerts.ToArray();
        mesh.triangles = visualTris.ToArray();
        mesh.uv2 = visualUV2.ToArray();
        mesh.uv3 = visualUV3.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        variant = new HexSphereVariant
        {
            bakedMesh = mesh,
            cellCenters = icoVerts.ToArray(),
            areaFractions = areaFractions
        };
    }

    private static Vector3 SculptVertex(Vector3 p, Vector3 stretch, float macStr, float macFreq, float micStr, float micFreq, int micOct, float offset)
    {
        if (macStr > 0f)
        {
            Vector3 warp = new Vector3(
                GeneratorUtility.FBM(p, macFreq, offset, 1),
                GeneratorUtility.FBM(p, macFreq, offset + 100f, 1),
                GeneratorUtility.FBM(p, macFreq, offset + 200f, 1)
            ) - new Vector3(0.5f, 0.5f, 0.5f);
            p += warp * macStr;
        }

        p = new Vector3(p.x * stretch.x, p.y * stretch.y, p.z * stretch.z);

        if (micStr > 0f)
        {
            float bump = GeneratorUtility.FBM(p, micFreq, offset + 300f, micOct) - 0.5f;
            p += p.normalized * (bump * micStr);
        }

        return p;
    }

    private static void BuildNeighborGraph(int cellCount, List<int> icoTris, out int[] offsets, out byte[] counts, out int[] neighbors)
    {
        offsets = new int[cellCount];
        counts = new byte[cellCount];

        HashSet<int>[] neighborSets = new HashSet<int>[cellCount];
        for (int i = 0; i < cellCount; i++) neighborSets[i] = new HashSet<int>();

        for (int i = 0; i < icoTris.Count; i += 3)
        {
            int c1 = icoTris[i];
            int c2 = icoTris[i + 1];
            int c3 = icoTris[i + 2];

            neighborSets[c1].Add(c2); neighborSets[c1].Add(c3);
            neighborSets[c2].Add(c1); neighborSets[c2].Add(c3);
            neighborSets[c3].Add(c1); neighborSets[c3].Add(c2);
        }

        List<int> flatNeighbors = new List<int>();
        int currentOffset = 0;

        for (int i = 0; i < cellCount; i++)
        {
            offsets[i] = currentOffset;
            counts[i] = (byte)neighborSets[i].Count;

            foreach (int neighbor in neighborSets[i])
            {
                flatNeighbors.Add(neighbor);
            }
            currentOffset += counts[i];
        }

        neighbors = flatNeighbors.ToArray();
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
}