// SpaceCameraController.cs
using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraState { Terrain, PlanetaryLow, PlanetaryHigh, System, Interstellar }

public class SpaceCameraController : MonoBehaviour
{
    public static SpaceCameraController Instance { get; private set; }

    [Header("State Machine")]
    public CameraState currentState = CameraState.System;
    public float currentZLevel { get; private set; } = 1f;
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

    [Header("Resistance Bounce")]
    public float transitionResistance = 0.5f;
    private float scrollAccumulator = 0f;
    private float transitionCooldownTimer = 0f;

    private bool isInitialized = false;
    private float currentMinZDist;
    private float currentMaxZDist;
    private float currentSyncWeight = 0f;
    private float syncVelocity;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
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
        if (!isInitialized)
        {
            InitializeCamera();
            return;
        }

        if (transitionCooldownTimer > 0) transitionCooldownTimer -= Time.deltaTime;

        HandleInput();
        UpdateTracking();
        CalculateZLevel();
        CheckStateTransitions();
        UpdateCameraTransform();

        // FIX: Dynamically adjust clip planes to prevent the Frustum Error and Z-Fighting
        if (Camera.main != null)
        {
            Camera.main.nearClipPlane = Mathf.Clamp(currentDistance * 0.1f, 0.0001f, 10f);
            Camera.main.farClipPlane = Mathf.Clamp(currentDistance * 1000f, 100000f, 10000000f);
        }
    }

    private void InitializeCamera()
    {
        if (SystemDataGenerator.Instance != null && SystemDataGenerator.Instance.star != null)
        {
            focusedBody = SystemDataGenerator.Instance.star;
            currentState = CameraState.System;

            float distMult = SystemDisplayManager.Instance.trueScaleMultiplier;
            currentMinZDist = (float)(focusedBody.localSystemBoundaryKm * distMult);
            currentMaxZDist = (float)(SystemDataGenerator.Instance.systemEdgeKm * distMult);

            float logMin = Mathf.Log10(currentMinZDist);
            float logMax = Mathf.Log10(currentMaxZDist);
            float startLog = Mathf.Lerp(logMin, logMax, 0.5f);

            targetDistance = Mathf.Pow(10, startLog);
            currentDistance = targetDistance;

            currentFocusPoint = SystemDisplayManager.Instance.CalculateSystemViewPosition(focusedBody, 0).ToVector3();

            isInitialized = true;
        }
    }

    public void SetFocus(CelestialBody newBody)
    {
        if (newBody == focusedBody) return;

        if (currentState == CameraState.System || currentState == CameraState.Interstellar)
        {
            float distMult = SystemDisplayManager.Instance.trueScaleMultiplier;
            float newZZeroDist = (float)(newBody.localSystemBoundaryKm * distMult);
            float newZOneDist = (float)(SystemDataGenerator.Instance.systemEdgeKm * distMult);

            float logMin = Mathf.Log10(newZZeroDist);
            float logMax = Mathf.Log10(newZOneDist);
            float newLogDist = Mathf.Lerp(logMin, logMax, currentZLevel);

            targetDistance = Mathf.Pow(10, newLogDist);
        }

        focusedBody = newBody;

        if (currentState == CameraState.System || currentState == CameraState.Interstellar)
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

                if (IsPushingAgainstThreshold(scrollNormalized))
                {
                    scrollAccumulator += Mathf.Abs(scrollNormalized) * Time.deltaTime * 10f;
                }
                else
                {
                    scrollAccumulator = 0f;
                    targetDistance -= zoomAmount;

                    // FIX: Allow the camera to zoom in extremely close for tiny asteroids
                    if (targetDistance < 0.000001f) targetDistance = 0.000001f;
                }
            }
            else
            {
                scrollAccumulator = Mathf.Lerp(scrollAccumulator, 0f, Time.deltaTime * 5f);
            }
        }
    }

    private void UpdateTracking()
    {
        if (focusedBody == null) return;

        Vector3 targetFocus = Vector3.zero;

        if (currentState == CameraState.System || currentState == CameraState.Interstellar)
        {
            targetFocus = SystemDisplayManager.Instance.CalculateSystemViewPosition(focusedBody, TimeManager.Instance.totalSeconds).ToVector3();

            float distMult = SystemDisplayManager.Instance.trueScaleMultiplier;
            float targetMinZ = (float)(focusedBody.localSystemBoundaryKm * distMult);
            float targetMaxZ = (float)(SystemDataGenerator.Instance.systemEdgeKm * distMult);

            currentMinZDist = Mathf.Lerp(currentMinZDist, targetMinZ, Time.deltaTime * focusPanSpeed);
            currentMaxZDist = Mathf.Lerp(currentMaxZDist, targetMaxZ, Time.deltaTime * focusPanSpeed);
        }

        if (Vector3.Distance(currentFocusPoint, targetFocus) < 0.5f) currentFocusPoint = targetFocus;
        else currentFocusPoint = Vector3.Lerp(currentFocusPoint, targetFocus, Time.deltaTime * focusPanSpeed);
    }

    private void CalculateZLevel()
    {
        if (focusedBody == null || SystemDataGenerator.Instance == null) return;

        if (currentState == CameraState.System || currentState == CameraState.Interstellar)
        {
            if (currentDistance <= currentMinZDist) currentZLevel = 0f;
            else if (currentDistance >= currentMaxZDist) currentZLevel = 1f;
            else
            {
                float logMin = Mathf.Log10(currentMinZDist);
                float logMax = Mathf.Log10(currentMaxZDist);
                float logDist = Mathf.Log10(currentDistance);
                currentZLevel = (logDist - logMin) / (logMax - logMin);
            }
        }
        else
        {
            currentZLevel = 0f;
        }
    }

    private void GetThresholds(out float zZeroDist, out float zOneDist, out float planHighDist, out float planLowDist, out float terrainDist, out float scaleRatio)
    {
        float distMult = SystemDisplayManager.Instance.trueScaleMultiplier;
        zZeroDist = (float)(focusedBody.localSystemBoundaryKm * distMult);
        zOneDist = (float)(SystemDataGenerator.Instance.systemEdgeKm * distMult);

        float localRadius = ViewManager.Instance.localViewUnityRadius;
        float systemRadiusAtZ0 = SystemDisplayManager.Instance.CalculateSystemViewRadius(focusedBody, 0f);

        scaleRatio = localRadius / systemRadiusAtZ0;

        planHighDist = zZeroDist * scaleRatio;
        planLowDist = localRadius * 10f;
        terrainDist = localRadius * 2f;
    }

    private bool IsPushingAgainstThreshold(float scrollDirection)
    {
        if (focusedBody == null || transitionCooldownTimer > 0) return false;

        GetThresholds(out float zZeroDist, out float zOneDist, out float planHighDist, out float planLowDist, out float terrainDist, out float scaleRatio);

        if (scrollDirection > 0)
        {
            if (currentState == CameraState.Interstellar && targetDistance <= zOneDist) return true;
            if (currentState == CameraState.System && targetDistance <= zZeroDist) return true;
            if (currentState == CameraState.PlanetaryHigh && targetDistance <= planLowDist) return true;
            if (currentState == CameraState.PlanetaryLow && targetDistance <= terrainDist) return true;
        }
        else if (scrollDirection < 0)
        {
            if (currentState == CameraState.Terrain && targetDistance >= terrainDist) return true;
            if (currentState == CameraState.PlanetaryLow && targetDistance >= planLowDist) return true;
            if (currentState == CameraState.PlanetaryHigh && targetDistance >= planHighDist) return true;
            if (currentState == CameraState.System && targetDistance >= zOneDist) return true;
        }

        return false;
    }

    private void CheckStateTransitions()
    {
        if (focusedBody == null || scrollAccumulator < transitionResistance || transitionCooldownTimer > 0) return;

        GetThresholds(out float zZeroDist, out float zOneDist, out float planHighDist, out float planLowDist, out float terrainDist, out float scaleRatio);

        if (currentState == CameraState.Interstellar && targetDistance <= zOneDist)
        {
            currentState = CameraState.System;
            ExecuteTransition();
        }
        else if (currentState == CameraState.System && targetDistance <= zZeroDist)
        {
            if (!focusedBody.isHighResReady) return;

            currentState = CameraState.PlanetaryHigh;

            targetDistance *= scaleRatio;
            currentDistance *= scaleRatio;
            distanceVelocity *= scaleRatio;
            currentFocusPoint = Vector3.zero;

            ViewManager.Instance.TransitionToLocalView(focusedBody);
            ExecuteTransition();
        }
        else if (currentState == CameraState.PlanetaryHigh && targetDistance <= planLowDist)
        {
            currentState = CameraState.PlanetaryLow;
            ExecuteTransition();
        }
        else if (currentState == CameraState.PlanetaryLow && targetDistance <= terrainDist)
        {
            currentState = CameraState.Terrain;
            ExecuteTransition();
        }
        else if (currentState == CameraState.Terrain && targetDistance >= terrainDist)
        {
            currentState = CameraState.PlanetaryLow;
            ExecuteTransition();
        }
        else if (currentState == CameraState.PlanetaryLow && targetDistance >= planLowDist)
        {
            currentState = CameraState.PlanetaryHigh;
            ExecuteTransition();
        }
        else if (currentState == CameraState.PlanetaryHigh && targetDistance >= planHighDist)
        {
            currentState = CameraState.System;

            targetDistance /= scaleRatio;
            currentDistance /= scaleRatio;
            distanceVelocity /= scaleRatio;
            currentFocusPoint = SystemDisplayManager.Instance.CalculateSystemViewPosition(focusedBody, TimeManager.Instance.totalSeconds).ToVector3();

            ViewManager.Instance.TransitionToSystemView();
            ExecuteTransition();
        }
        else if (currentState == CameraState.System && targetDistance >= zOneDist)
        {
            currentState = CameraState.Interstellar;
            ExecuteTransition();
        }
    }

    private void ExecuteTransition()
    {
        scrollAccumulator = 0f;
        transitionCooldownTimer = 0.2f;
        Debug.Log($"State Transitioned to: {currentState}");
    }

    private void UpdateCameraTransform()
    {
        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);

        Quaternion baseRotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        float targetSync = (currentState == CameraState.PlanetaryLow || currentState == CameraState.Terrain) ? 1f : 0f;
        currentSyncWeight = Mathf.SmoothDamp(currentSyncWeight, targetSync, ref syncVelocity, 1.0f);

        Quaternion finalRotation = baseRotation;
        if (currentSyncWeight > 0.001f && focusedBody != null)
        {
            Quaternion planetRotation = Quaternion.Euler((float)focusedBody.axialTilt, focusedBody.currentRotationAngle, 0);
            Quaternion syncedRotation = planetRotation * baseRotation;
            finalRotation = Quaternion.Slerp(baseRotation, syncedRotation, currentSyncWeight);
        }

        Vector3 position = currentFocusPoint - (finalRotation * Vector3.forward * currentDistance);

        transform.position = position;
        transform.rotation = finalRotation;
    }
}