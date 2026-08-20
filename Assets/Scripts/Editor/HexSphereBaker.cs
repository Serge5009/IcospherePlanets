using UnityEditor;
using UnityEngine;
using System.IO;

public class HexSphereBaker : EditorWindow
{
    private int subdivisionLevel = 4;
    private string savePath = "Assets/BakedSpheres";

    [MenuItem("Strategy Tools/Hex Sphere Baker")]
    public static void ShowWindow()
    {
        GetWindow<HexSphereBaker>("Hex Sphere Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Bake Hexagonal Spheres", EditorStyles.boldLabel);

        subdivisionLevel = EditorGUILayout.IntSlider("Subdivisions", subdivisionLevel, 0, 7);
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        if (GUILayout.Button("Bake Sphere"))
        {
            Bake();
        }
    }

    private void Bake()
    {
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        Mesh mesh;
        Vector3[] cellCenters;

        HexSphereBuilder.GenerateTopology(subdivisionLevel, out mesh, out cellCenters);

        string meshPath = $"{savePath}/HexSphere_Mesh_Sub{subdivisionLevel}.asset";
        AssetDatabase.CreateAsset(mesh, meshPath);

        HexSphereTemplate template = ScriptableObject.CreateInstance<HexSphereTemplate>();
        template.subdivisions = subdivisionLevel;
        template.bakedMesh = mesh;
        template.cellCenters = cellCenters;

        string soPath = $"{savePath}/HexSphere_Template_Sub{subdivisionLevel}.asset";
        AssetDatabase.CreateAsset(template, soPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Successfully baked Hex Sphere Level {subdivisionLevel} to {savePath}");
    }
}