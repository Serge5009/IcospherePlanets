using UnityEngine;

public class SolarSystemBootstrapper : MonoBehaviour
{
    private void Start()
    {
        SystemDataGenerator.Instance.GenerateData();
        SystemMeshGenerator.Instance.GenerateMeshes(SystemDataGenerator.Instance.allBodies);
    }
}