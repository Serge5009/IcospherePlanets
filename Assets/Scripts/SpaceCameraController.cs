using UnityEngine;
using UnityEngine.InputSystem;

public class SpaceCameraController : MonoBehaviour
{
    public static SpaceCameraController Instance { get; private set; }

    [Header("Focus")]
    public Vector3 currentFocusPoint = Vector3.zero;
    private Vector3 targetFocusPoint = Vector3.zero;
    public float focusPanSpeed = 5f;

    [Header("Orbit Rotation")]
    public float rotationSpeed = 0.2f;
    public float rotationSmoothTime = 0.1f;

    private float currentYaw = 45f;
    private float currentPitch = 30f;
    private float targetYaw = 45f;
    private float targetPitch = 30f;

    private float yawVelocity;
    private float pitchVelocity;

    [Header("Zoom")]
    public float zoomSpeed = 0.001f;
    public float zoomSmoothTime = 0.1f;

    public float currentDistance = 100f;
    private float targetDistance = 100f;
    private float distanceVelocity;

    private float minZoomDistance = 10f;
    private float maxZoomDistance = 1000f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void LateUpdate()
    {
        HandleInput();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            targetYaw += mouseDelta.x * rotationSpeed;
            targetPitch -= mouseDelta.y * rotationSpeed;

            targetPitch = Mathf.Clamp(targetPitch, -85f, 85f);
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float zoomAmount = scroll * zoomSpeed * targetDistance;
            targetDistance = Mathf.Clamp(targetDistance - zoomAmount, minZoomDistance, maxZoomDistance);
        }
    }

    private void UpdateCameraTransform()
    {
        currentFocusPoint = Vector3.Lerp(currentFocusPoint, targetFocusPoint, Time.deltaTime * focusPanSpeed);

        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);

        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        Vector3 position = currentFocusPoint - (rotation * Vector3.forward * currentDistance);

        transform.position = position;
        transform.rotation = rotation;
    }

    public void SetFocus(Vector3 newFocusPoint, float bodyRadius)
    {
        targetFocusPoint = newFocusPoint;

        minZoomDistance = bodyRadius * 1.2f;
        maxZoomDistance = bodyRadius * 500f;

        targetDistance = Mathf.Clamp(targetDistance, minZoomDistance, maxZoomDistance);
    }

    public void UpdateTrackingPosition(Vector3 movingPosition)
    {
        targetFocusPoint = movingPosition;
    }
}