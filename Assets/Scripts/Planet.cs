using UnityEngine;
using UnityEngine.InputSystem;

public struct CellVisualData
{
    public Vector4 color;
    public int isHovered;
}

public class Planet : MonoBehaviour
{
    [Header("Logical Data")]
    public string planetName;
    public float radiusKm;

    [Header("Visual Data")]
    public float unityRadius = 1000f;

    public Cell[] cells;

    private ComputeBuffer visualBuffer;
    private CellVisualData[] visualDataArray;
    private int currentHoveredId = -1;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    public void InitializeVisuals()
    {
        visualDataArray = new CellVisualData[cells.Length];

        int totalNations = GeopoliticsManager.Instance.GetTotalNations();

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].ownerId = Random.Range(0, totalNations);

            Color nationColor = GeopoliticsManager.Instance.GetNation(cells[i].ownerId).mapColor;

            visualDataArray[i].color = nationColor;
            visualDataArray[i].isHovered = 0;
        }

        visualBuffer = new ComputeBuffer(cells.Length, 20);
        visualBuffer.SetData(visualDataArray);

        meshRenderer = GetComponent<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetBuffer("_CellVisualData", visualBuffer);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    private void Update()
    {
        HandleMouseInteraction();
    }

    private void HandleMouseInteraction()
    {
        if (Mouse.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);

        int hoveredId = -1;

        if (IntersectRaySphere(ray, transform.position, unityRadius, out Vector3 hitPoint))
        {
            Vector3 localHit = transform.InverseTransformPoint(hitPoint);
            hoveredId = GetNearestCell(localHit);
        }

        if (hoveredId != currentHoveredId)
        {
            if (currentHoveredId != -1)
            {
                visualDataArray[currentHoveredId].isHovered = 0;
            }

            if (hoveredId != -1)
            {
                visualDataArray[hoveredId].isHovered = 1;
            }

            currentHoveredId = hoveredId;

            if (visualBuffer != null)
            {
                visualBuffer.SetData(visualDataArray);
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && hoveredId != -1)
        {
            int ownerId = cells[hoveredId].ownerId;
            string ownerName = GeopoliticsManager.Instance.GetNation(ownerId).name;
            Debug.Log($"[{planetName}] Clicked Cell {hoveredId}. Owner: {ownerName} (ID: {ownerId})");
        }
    }

    private bool IntersectRaySphere(Ray ray, Vector3 sphereCenter, float sphereRadius, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        Vector3 L = sphereCenter - ray.origin;
        float tca = Vector3.Dot(L, ray.direction);
        if (tca < 0) return false;
        float d2 = Vector3.Dot(L, L) - tca * tca;
        float radius2 = sphereRadius * sphereRadius;
        if (d2 > radius2) return false;
        float thc = Mathf.Sqrt(radius2 - d2);
        float t0 = tca - thc;
        hitPoint = ray.origin + ray.direction * t0;
        return true;
    }

    private int GetNearestCell(Vector3 localHitPoint)
    {
        int nearestId = -1;
        float minDistanceSqr = float.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            float distSqr = (cells[i].localPosition - localHitPoint).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearestId = i;
            }
        }
        return nearestId;
    }

    private void OnDestroy()
    {
        if (visualBuffer != null)
        {
            visualBuffer.Release();
            visualBuffer = null;
        }
    }
}