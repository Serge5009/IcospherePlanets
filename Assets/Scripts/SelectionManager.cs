using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
        if (currentHoveredPlanet != null)
        {
            currentHoveredPlanet.SetHoveredCell(-1);
            currentHoveredPlanet = null;
        }

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
                    if (currentHoveredPlanet != null)
                    {
                        currentHoveredPlanet.SetHoveredCell(-1);
                        currentHoveredPlanet = null;
                    }

                    if (clicked)
                    {
                        SpaceCameraController.Instance.SetFocus(link.body);
                        OnPlanetSelected?.Invoke(link.body);
                    }
                    return;
                }
                else
                {
                    Vector3 localHit = planet.transform.InverseTransformPoint(hit.point).normalized;

                    int bestId = -1;
                    float bestDot = -1f;
                    CellTopology[] topos = planet.meshData.topologies;

                    for (int i = 0; i < topos.Length; i++)
                    {
                        float d = Vector3.Dot(topos[i].localPosition, localHit);
                        if (d > bestDot)
                        {
                            bestDot = d;
                            bestId = i;
                        }
                    }

                    if (bestId != -1)
                    {
                        if (currentHoveredPlanet != planet)
                        {
                            if (currentHoveredPlanet != null) currentHoveredPlanet.SetHoveredCell(-1);
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

        if (currentHoveredPlanet != null)
        {
            currentHoveredPlanet.SetHoveredCell(-1);
            currentHoveredPlanet = null;
        }
    }
}