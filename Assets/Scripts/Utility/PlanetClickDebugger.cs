using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;

public class PlanetClickDebugger : MonoBehaviour
{
    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

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

    private void PrintPlanetStats(CelestialBody b)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== {b.name.ToUpper()} ({b.archetype}) ===");

        sb.AppendLine($"[PHYSICS] M: {b.massEarths:F3}E | R: {b.radiusKm:F0}km | g: {b.surfaceGravity:F2}m/s² | Dist: {(b.orbit.semiMajorAxis / AstroMath.AU_TO_KM):F2}AU | Spin: {(b.rotationPeriodSeconds / 3600f):F1}h");

        string rock = b.dominantBedrock != null ? b.dominantBedrock.bedrockName : "None";
        if (b.bodyType != BodyType.Star && b.bodyType != BodyType.GasGiant)
        {
            sb.AppendLine($"[GEOLOGY] Bedrock: {rock} | Core: {(b.coreMassFraction * 100f):F0}% ({b.coreTemperatureKelvin:F0}K, {(b.isCoreActive ? "Active" : "Dead")}) | Mag-Shield: {b.magnetosphereStrength:F2}");
        }

        if (b.surfacePressureAtm > 0)
        {
            sb.Append($"[ATMOS] P: {b.surfacePressureAtm:F3}atm | Heat+: {b.greenhouseHeatContribution:F1}K | Tox: {b.toxicityLevel:F2} | Gases: ");
            foreach (var kvp in b.atmosphericGasesKg)
            {
                GasTemplate gas = DataLibrary.Instance.GetGas(kvp.Key);
                string gasName = gas != null ? gas.gasName : $"ID{kvp.Key}";
                double massMt = kvp.Value / 1e9;
                sb.Append($"{gasName}: {massMt:F0}Mt, ");
            }
            sb.AppendLine();
        }
        else if (b.bodyType != BodyType.Star)
        {
            sb.AppendLine($"[ATMOS] Vacuum (0 atm)");
        }

        Debug.Log(sb.ToString());
    }
}