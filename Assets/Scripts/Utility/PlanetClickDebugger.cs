using UnityEngine;
using UnityEngine.InputSystem;

public class PlanetClickDebugger : MonoBehaviour
{
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            Ray ray = mainCam.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                CelestialBodyLink link = hit.collider.GetComponent<CelestialBodyLink>();
                if (link != null && link.body != null)
                {
                    PrintPlanetStats(link.body);
                }
            }
        }
    }

    private void PrintPlanetStats(CelestialBody body)
    {
        string bedrockName = body.dominantBedrock != null ? body.dominantBedrock.bedrockName : "Unknown";

        Debug.Log($"=== PLANET SCAN: {body.name} ===");
        Debug.Log($"Type: {body.bodyType} | Archetype: {body.archetype}");
        Debug.Log($"Mass: {body.massEarths:F3} Earths | Radius: {body.radiusKm:F0} km");

        Debug.Log($"Distance from Star: {(body.orbit.semiMajorAxis / AstroMath.AU_TO_KM):F2} AU");
        Debug.Log($"Rotation Period: {(body.rotationPeriodSeconds / 3600f):F1} Hours");
        Debug.Log($"Dominant Bedrock: {bedrockName}");

        if (body.bodyType != BodyType.Star && body.bodyType != BodyType.GasGiant)
        {
            Debug.Log($"--- CORE & MAGNETOSPHERE ---");
            Debug.Log($"Core Mass Fraction: {(body.coreMassFraction * 100f):F1}%");
            Debug.Log($"Core Temp: {body.coreTemperatureKelvin:F0} K");
            Debug.Log($"Core Active: {body.isCoreActive}");
            Debug.Log($"Magnetosphere Strength: {body.magnetosphereStrength:F2} (Earth = 1.0)");
        }
        Debug.Log($"============================");
    }
}