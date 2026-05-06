using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LingoteRush.Editor
{
    public static class MoltenGoldVisualAssetBuilder
    {
        private const string RootFolder = "Assets/Visuals/Smelting/MoltenGold";
        private const string MaterialsFolder = "Assets/Visuals/Smelting/MoltenGold/Materials";
        private const string PrefabsFolder = "Assets/Visuals/Smelting/MoltenGold/Prefabs";
        private const string MaterialPath = "Assets/Visuals/Smelting/MoltenGold/Materials/M_MoltenGold_URP.mat";
        private const string PrefabPath = "Assets/Visuals/Smelting/MoltenGold/Prefabs/PF_MoltenGoldVisual.prefab";

        [MenuItem("Lingote Rush/Visuals/Rebuild Molten Gold Assets")]
        public static void BuildAll()
        {
            EnsureFolders();
            var moltenMaterial = BuildMaterial();
            BuildPrefab(moltenMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Molten gold visual assets rebuilt at 'Assets/Visuals/Smelting/MoltenGold'.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Visuals"))
            {
                AssetDatabase.CreateFolder("Assets", "Visuals");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Visuals/Smelting"))
            {
                AssetDatabase.CreateFolder("Assets/Visuals", "Smelting");
            }

            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                AssetDatabase.CreateFolder("Assets/Visuals/Smelting", "MoltenGold");
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder(RootFolder, "Materials");
            }

            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            {
                AssetDatabase.CreateFolder(RootFolder, "Prefabs");
            }
        }

        private static Material BuildMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                throw new MissingReferenceException("Universal Render Pipeline/Lit shader was not found.");
            }

            var moltenMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (moltenMaterial == null)
            {
                moltenMaterial = new Material(shader);
                AssetDatabase.CreateAsset(moltenMaterial, MaterialPath);
            }

            moltenMaterial.name = "M_MoltenGold_URP";
            moltenMaterial.shader = shader;
            moltenMaterial.SetColor("_BaseColor", new Color(1f, 0.56f, 0.08f, 1f));
            moltenMaterial.SetColor("_EmissionColor", new Color(1.65f, 0.7f, 0.16f, 1f) * 1.85f);
            moltenMaterial.SetFloat("_Metallic", 0.72f);
            moltenMaterial.SetFloat("_Smoothness", 0.92f);
            moltenMaterial.EnableKeyword("_EMISSION");
            moltenMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(moltenMaterial);
            return moltenMaterial;
        }

        private static void BuildPrefab(Material moltenMaterial)
        {
            var moltenVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moltenVisual.name = "MoltenGoldVisual";
            moltenVisual.transform.localPosition = Vector3.zero;
            moltenVisual.transform.localRotation = Quaternion.identity;
            moltenVisual.transform.localScale = new Vector3(0.18f, 0.045f, 0.18f);

            var collider = moltenVisual.GetComponent<Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = moltenVisual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = moltenMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

            PrefabUtility.SaveAsPrefabAsset(moltenVisual, PrefabPath);
            Object.DestroyImmediate(moltenVisual);
        }
    }
}
