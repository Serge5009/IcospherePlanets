using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraState { SystemView, LocalView }

public class SpaceCameraController : MonoBehaviour
{
    public static SpaceCameraController Instance { get; private set; }

    public CameraState currentState = CameraState.SystemView;
    private CelestialBody focusedBody;

    [Header("Input Actions")]
    public InputActionReference lookAction;
    public InputActionReference zoomAction;
    public InputActionReference orbitButtonAction;

    [Header("Focus & Tracking")]
    public Vector3 currentFocusPoint = Vector3.zero;
    public float focusPanSpeed = 5f;

    [Header("Orbit Rotation")]
    public float rotationSpeed = 0.2f;
    public float rotationSmoothTime = 0.1f;
    private float currentYaw = 45f, currentPitch = 30f;
    private float targetYaw = 45f, targetPitch = 30f;
    private float yawVelocity, pitchVelocity;

    [Header("Zoom & Transition")]
    public float zoomSpeed = 0.05f;
    public float zoomSmoothTime = 0.1f;
    public float currentDistance = 100f;
    private float targetDistance = 100f;
    private float distanceVelocity;

    public float transitionThresholdRadii = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        if (Camera.main != null) Camera.main.farClipPlane = 100000f;
    }

    private void OnEnable()
    {
        if (lookAction != null) lookAction.action.Enable();
        if (zoomAction != null) zoomAction.action.Enable();
        if (orbitButtonAction != null) orbitButtonAction.action.Enable();
    }

    private void OnDisable()
    {
        if (lookAction != null) lookAction.action.Disable();
        if (zoomAction != null) zoomAction.action.Disable();
        if (orbitButtonAction != null) orbitButtonAction.action.Disable();
    }

    private void LateUpdate()
    {
        HandleInput();
        UpdateTracking();
        CheckLODTransition();
        UpdateCameraTransform();
    }

    public void SetFocus(CelestialBody body)
    {
        focusedBody = body;

        if (currentState == CameraState.SystemView)
        {
            SystemDisplayManager.Instance.UpdateTrailContext(focusedBody, currentState);
        }
    }

    private void HandleInput()
    {
        if (orbitButtonAction != null && orbitButtonAction.action.IsPressed())
        {
            if (lookAction != null)
            {
                Vector2 lookDelta = lookAction.action.ReadValue<Vector2>();
                targetYaw += lookDelta.x * rotationSpeed;
                targetPitch -= lookDelta.y * rotationSpeed;
                targetPitch = Mathf.Clamp(targetPitch, -85f, 85f);
            }
        }

        if (zoomAction != null)
        {
            float scrollRaw = zoomAction.action.ReadValue<Vector2>().y;
            float scrollNormalized = 0f;
            if (scrollRaw > 0) scrollNormalized = 1f;
            else if (scrollRaw < 0) scrollNormalized = -1f;

            if (scrollNormalized != 0f)
            {
                float zoomAmount = scrollNormalized * zoomSpeed * targetDistance;
                targetDistance -= zoomAmount;
                if (targetDistance < 0.1f) targetDistance = 0.1f;
            }
        }
    }

    private void UpdateTracking()
    {
        if (focusedBody == null) return;

        Vector3 targetFocus = Vector3.zero;

        if (currentState == CameraState.SystemView)
        {
            targetFocus = SystemDisplayManager.Instance.CalculateSystemViewPosition(focusedBody, TimeManager.Instance.totalSeconds).ToVector3();
        }

        if (Vector3.Distance(currentFocusPoint, targetFocus) < 0.5f)
        {
            currentFocusPoint = targetFocus;
        }
        else
        {
            currentFocusPoint = Vector3.Lerp(currentFocusPoint, targetFocus, Time.deltaTime * focusPanSpeed);
        }
    }

    private void CheckLODTransition()
    {
        if (focusedBody == null || focusedBody.visualObject == null) return;

        float systemRadius = SystemDisplayManager.Instance.CalculateBaseSystemViewRadius(focusedBody);
        float localRadius = ViewManager.Instance.localViewUnityRadius;
        float scaleRatio = localRadius / systemRadius;

        if (currentState == CameraState.SystemView)
        {
            float thresholdDistance = systemRadius * transitionThresholdRadii;

            if (targetDistance < thresholdDistance)
            {
                if (!focusedBody.isHighResReady) return;

                currentState = CameraState.LocalView;

                targetDistance *= scaleRatio;
                currentDistance *= scaleRatio;
                distanceVelocity *= scaleRatio;
                currentFocusPoint = Vector3.zero;

                ViewManager.Instance.TransitionToLocalView(focusedBody);
            }
        }
        else if (currentState == CameraState.LocalView)
        {
            float thresholdDistance = localRadius * transitionThresholdRadii;

            if (targetDistance > thresholdDistance)
            {
                currentState = CameraState.SystemView;

                targetDistance /= scaleRatio;
                currentDistance /= scaleRatio;
                distanceVelocity /= scaleRatio;

                currentFocusPoint = SystemDisplayManager.Instance.CalculateSystemViewPosition(focusedBody, TimeManager.Instance.totalSeconds).ToVector3();

                ViewManager.Instance.TransitionToSystemView();
            }
        }
    }

    private void UpdateCameraTransform()
    {
        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        Vector3 position = currentFocusPoint - (rotation * Vector3.forward * currentDistance);

        transform.position = position;
        transform.rotation = rotation;
    }
}