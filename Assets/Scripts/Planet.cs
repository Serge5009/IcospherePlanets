using UnityEngine;
using UnityEngine.InputSystem;

public class Planet : MonoBehaviour
{
    [Header("Logical Data")]
    public string planetName;
    public float radiusKm;

    [Header("Visual Data")]
    public float unityRadius = 1000f;

    public Cell[] cells;

    private void Update()
    {
        HandleMouseClick();
    }

    private void HandleMouseClick()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            if (IntersectRaySphere(ray, transform.position, unityRadius, out Vector3 hitPoint))
            {
                Vector3 localHit = transform.InverseTransformPoint(hitPoint);
                int clickedCellId = GetNearestCell(localHit);

                Debug.Log($"[{planetName}] Clicked Cell ID: {clickedCellId}");
            }
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
}