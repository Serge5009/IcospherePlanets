using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference clickAction;
    public InputActionReference pointerPositionAction;

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
        if (clickAction != null && clickAction.action.WasPressedThisFrame())
        {
            if (pointerPositionAction == null) return;

            Vector2 mousePos = pointerPositionAction.action.ReadValue<Vector2>();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                CelestialBodyLink link = hit.collider.GetComponent<CelestialBodyLink>();
                if (link != null)
                {
                    Debug.Log($"Focused on: {link.body.name}");
                    SpaceCameraController.Instance.SetFocus(link.body);
                }
            }
        }
    }
}