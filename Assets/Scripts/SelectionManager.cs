using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [Header("Input Actions")]
    public InputActionReference clickAction;
    public InputActionReference pointerPositionAction;

    public event Action<CelestialBody> OnPlanetSelected;
    public event Action<CelestialBody, int> OnCellSelected;

    private Planet currentHoveredPlanet;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        if (clickAction != null) clickAction.action.Enable();
        if (pointerPositionAction != null) pointerPositionAction.action.Enable();
    }

    private void OnDisable()
    {
        if (clickAction != null) clickAction.action.Disable();
        if (pointerPositionAction != null) pointerPositionAction.action.Disable();
    }

    private void Update()
    {
        if (pointerPositionAction == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        Vector2 mousePos = pointerPositionAction.action.ReadValue<Vector2>();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        bool clicked = clickAction != null && clickAction.action.WasPressedThisFrame();

        CameraState state = SpaceCameraController.Instance.currentState;

        if (state == CameraState.System || state == CameraState.Interstellar)
        {
            HandleSystemViewSelection(ray, clicked);
        }
        else
        {
            HandleLocalViewSelection(ray, clicked);
        }
    }

    private void HandleSystemViewSelection(Ray ray, bool clicked)
    {
        ClearHover();

        if (clicked && Physics.Raycast(ray, out RaycastHit hit))
        {
            CelestialBodyLink link = hit.collider.GetComponent<CelestialBodyLink>();
            if (link != null)
            {
                SpaceCameraController.Instance.SetFocus(link.body);
                OnPlanetSelected?.Invoke(link.body);
            }
        }
    }

    private void HandleLocalViewSelection(Ray ray, bool clicked)
    {
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Planet planet = hit.collider.GetComponent<Planet>();
            CelestialBodyLink link = hit.collider.GetComponent<CelestialBodyLink>();

            if (planet != null && link != null)
            {
                bool isMainPlanet = (planet.meshData == link.body.localViewData);

                if (!isMainPlanet)
                {
                    ClearHover();

                    if (clicked)
                    {
                        SpaceCameraController.Instance.SetFocus(link.body);
                        OnPlanetSelected?.Invoke(link.body);
                    }
                    return;
                }
                else
                {
                    int bestId = -1;

                    if (hit.collider is SphereCollider)
                    {
                        Vector3 localHit = planet.transform.InverseTransformPoint(hit.point).normalized;
                        float bestDot = -1f;
                        CellTopology[] topos = planet.meshData.topologies;

                        for (int i = 0; i < topos.Length; i++)
                        {
                            float d = Vector3.Dot(topos[i].localPosition.normalized, localHit);
                            if (d > bestDot)
                            {
                                bestDot = d;
                                bestId = i;
                            }
                        }
                    }
                    else if (hit.collider is MeshCollider)
                    {
                        Mesh mesh = planet.meshData.sharedMesh;
                        int[] tris = mesh.triangles;
                        Vector2[] uvs = mesh.uv2;

                        int vertIndex = tris[hit.triangleIndex * 3];
                        Vector2 encodedId = uvs[vertIndex];

                        bestId = Mathf.RoundToInt(encodedId.y) * 2000 + Mathf.RoundToInt(encodedId.x);
                    }

                    if (bestId != -1)
                    {
                        if (currentHoveredPlanet != planet)
                        {
                            ClearHover();
                            currentHoveredPlanet = planet;
                        }

                        planet.SetHoveredCell(bestId);

                        if (clicked)
                        {
                            OnCellSelected?.Invoke(planet.bodyData, bestId);
                        }
                    }
                    return;
                }
            }
        }

        ClearHover();
    }

    private void ClearHover()
    {
        if (currentHoveredPlanet != null)
        {
            currentHoveredPlanet.SetHoveredCell(-1);
            currentHoveredPlanet = null;
        }
    }
}