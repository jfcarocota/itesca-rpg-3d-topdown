using UnityEngine;
using System.Linq;
using StixGames.GrassShader;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(GrassFallback))]
public class GrassFallbackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TextureAtlasGenerator();

        EditorGUILayout.Space();

        //TODO Button for caching the fallback octree
        base.OnInspectorGUI();
    }

    private void TextureAtlasGenerator()
    {
        var fallbackTargets = targets.Cast<GrassFallback>().ToArray();
        if (fallbackTargets.Select(x => x.GetComponent<GrassRenderer>()).Any(x => x == null || x.Material == null || !GrassUtility.IsGrassMaterial(x.Material)))
        {
            EditorGUILayout.LabelField(targets.Length > 1 ? 
                "One of the selected objects has no original material, or the material does not use the DirectX 11 Grass Shader." 
                : "The object you selected has no original material, or the material does not use the DirectX 11 Grass Shader.");
            return;
        }

        if (fallbackTargets.Any(x => x.NatureMeshFilter.GetMeshes() == null))
        {
            EditorGUILayout.LabelField(targets.Length > 1 ?
                "One of the selected objects has no mesh."
                : "The object you selected has no mesh.");
            return;
        }

        //Add default values to preprocessor

        if (GUILayout.Button(targets.Length > 1 ? "Generate Texture Atlases" : "Generate Texture Atlas"))
        {
            GenerateTextureAtlases(fallbackTargets);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button(new GUIContent("Clear Fallback Cache", "If you've changed your material or mesh, your can update the fallback cache here.")))
        {
            foreach (var fallback in fallbackTargets)
            {
                fallback.UpdateFallback();
            }
        }
    }

    private static void GenerateTextureAtlases(GrassFallback[] fallbackTargets)
    {
        foreach (var fallback in fallbackTargets)
        {
            var material = fallback.GetComponent<GrassRenderer>().Material;
            var meshes = fallback.NatureMeshFilter.GetMeshes();

            fallback.PreProcessor.Meshes = meshes;
            fallback.PreProcessor.Process(material);
        }
    }
}
