using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class HexSphereBaker : EditorWindow
{
    [Header("Target")]
    private HexSphereTemplate targetTemplate;

    [Header("Base Settings")]
    private int subdivisionLevel = 4;
    private int relaxationPasses = 3;

    [Header("Sculpting Settings")]
    private int variantsToAppend = 5;

    [Tooltip("How much the asteroid can stretch into a cigar or flatten into a disc.")]
    private float stretchMax = 0.5f;

    [Tooltip("Bends and twists the overall shape of the asteroid.")]
    private float macroStrength = 0.4f;
    private float macroFreq = 1.5f;

    [Tooltip("Adds surface roughness, craters, and ridges.")]
    private float microStrength = 0.15f;
    private float microFreq = 3.0f;
    private int microOctaves = 2;

    private string savePath = "Assets/BakedSpheres";

    [MenuItem("Strategy Tools/Hex Sphere Baker")]
    public static void ShowWindow()
    {
        GetWindow<HexSphereBaker>("Hex Sphere Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Hex Sphere Template Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetTemplate = (HexSphereTemplate)EditorGUILayout.ObjectField("Target Template", targetTemplate, typeof(HexSphereTemplate), false);

        EditorGUILayout.Space();

        if (targetTemplate == null)
        {
            GUILayout.Label("--- CREATE NEW BASE TEMPLATE ---", EditorStyles.boldLabel);
            subdivisionLevel = EditorGUILayout.IntSlider("Subdivisions", subdivisionLevel, 0, 7);
            relaxationPasses = EditorGUILayout.IntSlider("Relaxation Passes", relaxationPasses, 0, 10);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Base Template (Variant 0)", GUILayout.Height(40)))
            {
                CreateBaseTemplate();
            }
        }
        else
        {
            GUILayout.Label($"--- APPEND TO: {targetTemplate.name} ---", EditorStyles.boldLabel);

            GUI.enabled = false;
            EditorGUILayout.IntField("Subdivisions", targetTemplate.subdivisions);
            EditorGUILayout.IntField("Relaxation Passes", targetTemplate.relaxationPasses);
            EditorGUILayout.IntField("Current Variants", targetTemplate.variants != null ? targetTemplate.variants.Length : 0);
            GUI.enabled = true;

            EditorGUILayout.Space();
            variantsToAppend = EditorGUILayout.IntSlider("Variants to Append", variantsToAppend, 1, 20);

            EditorGUILayout.Space();
            GUILayout.Label("1. Global Scale", EditorStyles.boldLabel);
            stretchMax = EditorGUILayout.Slider("Stretch/Squash Max", stretchMax, 0f, 0.8f);

            EditorGUILayout.Space();
            GUILayout.Label("2. Macro Bending (Shape)", EditorStyles.boldLabel);
            macroStrength = EditorGUILayout.Slider("Bend Strength", macroStrength, 0f, 1f);
            macroFreq = EditorGUILayout.Slider("Bend Frequency", macroFreq, 0.1f, 5f);

            EditorGUILayout.Space();
            GUILayout.Label("3. Micro Detail (Surface)", EditorStyles.boldLabel);
            microStrength = EditorGUILayout.Slider("Roughness Strength", microStrength, 0f, 0.5f);
            microFreq = EditorGUILayout.Slider("Roughness Frequency", microFreq, 0.1f, 10f);
            microOctaves = EditorGUILayout.IntSlider("Roughness Octaves", microOctaves, 1, 4);

            EditorGUILayout.Space();
            if (GUILayout.Button($"Append {variantsToAppend} Variants", GUILayout.Height(40)))
            {
                AppendVariants();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear All Variants (Keep Base)", GUILayout.Height(30)))
            {
                ClearVariants();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    private void CreateBaseTemplate()
    {
        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

        EditorUtility.DisplayProgressBar("Baking Base Sphere", "Generating Topology...", 0.5f);

        HexSphereBuilder.GenerateTemplateData(
            subdivisionLevel, relaxationPasses,
            0f, 0f, 0f, 0f, 0f, 1,
            out HexSphereVariant variant, out int[] offsets, out byte[] counts, out int[] neighbors
        );

        string meshPath = $"{savePath}/HexSphere_Mesh_Sub{subdivisionLevel}_Var0.asset";
        AssetDatabase.CreateAsset(variant.bakedMesh, meshPath);

        HexSphereTemplate template = ScriptableObject.CreateInstance<HexSphereTemplate>();
        template.subdivisions = subdivisionLevel;
        template.relaxationPasses = relaxationPasses;
        template.neighborOffsets = offsets;
        template.neighborCounts = counts;
        template.neighbors = neighbors;
        template.variants = new HexSphereVariant[] { variant };

        string soPath = $"{savePath}/HexSphere_Template_Sub{subdivisionLevel}.asset";
        AssetDatabase.CreateAsset(template, soPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        targetTemplate = template;

        EditorUtility.ClearProgressBar();
        Debug.Log($"Created Base Template: {soPath}");
    }

    private void AppendVariants()
    {
        if (targetTemplate == null) return;

        int startIndex = targetTemplate.variants != null ? targetTemplate.variants.Length : 0;
        List<HexSphereVariant> newVariantsList = new List<HexSphereVariant>();
        if (targetTemplate.variants != null) newVariantsList.AddRange(targetTemplate.variants);

        string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(targetTemplate));

        for (int i = 0; i < variantsToAppend; i++)
        {
            EditorUtility.DisplayProgressBar("Appending Variants", $"Generating Variant {i + 1}/{variantsToAppend}...", (float)i / variantsToAppend);

            float curStretch = stretchMax * Random.Range(0.8f, 1.2f);
            float curMacStr = macroStrength * Random.Range(0.8f, 1.2f);
            float curMacFreq = macroFreq * Random.Range(0.8f, 1.2f);
            float curMicStr = microStrength * Random.Range(0.8f, 1.2f);
            float curMicFreq = microFreq * Random.Range(0.8f, 1.2f);

            HexSphereBuilder.GenerateTemplateData(
                targetTemplate.subdivisions, targetTemplate.relaxationPasses,
                curStretch, curMacStr, curMacFreq, curMicStr, curMicFreq, microOctaves,
                out HexSphereVariant variant, out _, out _, out _
            );

            string meshPath = $"{path}/HexSphere_Mesh_Sub{targetTemplate.subdivisions}_Var{startIndex + i}.asset";
            AssetDatabase.CreateAsset(variant.bakedMesh, meshPath);

            newVariantsList.Add(variant);
        }

        targetTemplate.variants = newVariantsList.ToArray();
        EditorUtility.SetDirty(targetTemplate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();
        Debug.Log($"Appended {variantsToAppend} variants to {targetTemplate.name}");
    }

    private void ClearVariants()
    {
        if (targetTemplate == null || targetTemplate.variants == null || targetTemplate.variants.Length <= 1) return;

        if (EditorUtility.DisplayDialog("Clear Variants?", "This will delete all distorted mesh assets from your hard drive and reset the template to Variant 0. This cannot be undone.", "Delete", "Cancel"))
        {
            for (int i = 1; i < targetTemplate.variants.Length; i++)
            {
                string meshPath = AssetDatabase.GetAssetPath(targetTemplate.variants[i].bakedMesh);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    AssetDatabase.DeleteAsset(meshPath);
                }
            }

            targetTemplate.variants = new HexSphereVariant[] { targetTemplate.variants[0] };
            EditorUtility.SetDirty(targetTemplate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Cleared all variants from {targetTemplate.name}. Reset to Variant 0.");
        }
    }
}