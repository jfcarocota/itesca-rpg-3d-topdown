using StixGames.NatureCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StixGames.GrassShader
{
    public class TerrainConverterWindow : EditorWindow
    {
        private Material newMaterial;
        private bool castShadow = true;
        private bool receiveShadow = true;
    
        [MenuItem("Window/Stix Games/Deprecated/Terrain Converter")]
        public static void Init()
        {
            var window = GetWindow<TerrainConverterWindow>();
            window.Show();
        }

        private void OnGUI()
        {
            newMaterial = (Material) EditorGUILayout.ObjectField(new GUIContent("New Material",
                    "Select the material you want to use for the new terrain-mesh."), newMaterial, 
                typeof(Material), false);

            castShadow = EditorGUILayout.Toggle(new GUIContent("Cast shadows",
                "Sets if the generated meshes should cast shadows. " +
                "This can be changed in the inspector of the sub-meshes afterwards."), castShadow);
        
            receiveShadow = EditorGUILayout.Toggle(new GUIContent("Receive shadows",
                "Sets if the generated meshes should receive shadows. " +
                "This can be changed in the inspector of the sub-meshes afterwards."), receiveShadow);
            
            EditorGUILayout.Space();
        
            if (Selection.activeGameObject == null)
            {
                EditorGUILayout.LabelField(new GUIContent("Select a terrain"));
                return;
            }
        
            var terrain = Selection.activeGameObject.GetComponent<Terrain>();

            if (terrain == null)
            {
                EditorGUILayout.LabelField(new GUIContent("Select a terrain"));
                return;
            }

            if (GUILayout.Button(new GUIContent("Convert to mesh", "Converts the currently selected terrain to a mesh.")))
            {
                GenerateTerrainMeshObjects(terrain);
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button(new GUIContent("Extract detail textures", "Extracts the current detail textures for use with the DirectX 11 grass shader.")))
            {
                ExtractDetailTextures(terrain);
            }
        }

        private void GenerateTerrainMeshObjects(Terrain terrain)
        {
            var terrainRemesher = new TerrainRemesher(terrain);

            var meshes = terrainRemesher.GenerateTerrainMesh();

            var parent = new GameObject(terrain.name + " Mesh").transform;

            foreach (var mesh in meshes)
            {
                var obj = new GameObject("SubTerrain");
                obj.transform.parent = parent;
                obj.AddComponent<MeshFilter>().mesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.material = newMaterial;
                renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = receiveShadow;
            }

            var terrainTransform = terrain.transform;
            parent.position = terrainTransform.position;
            parent.rotation = terrainTransform.rotation;
            parent.localScale = terrainTransform.localScale;
            parent.parent = terrainTransform;

            Undo.RegisterCreatedObjectUndo(parent.gameObject, "Converted terrain to mesh.");
        }

        private void ExtractDetailTextures(Terrain terrain)
        {
            var data = terrain.terrainData;

            var detailCount = data.detailPrototypes.Length;

            for (int i = 0; i < detailCount; i += 4)
            {
                string title = "Extract terrain detail textures " + (i+1) + "-" + Mathf.Min(i + 4, detailCount);
                string path = EditorUtility.SaveFilePanelInProject(title, "extractedDensityTexture", 
                    "png", "Choose where to save the extracted density texture");
            
                if (path == null)
                {
                    return;
                }

                var details = new int[4][,];
                for (int j = 0; j < 4; j++)
                {
                    if (i + j < detailCount)
                    {
                        details[j] = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i+j);
                    }
                    else
                    {
                        details[j] = new int[data.detailWidth, data.detailHeight];
                    }
                }

                var colors = new Color[data.detailWidth * data.detailHeight];
                for (int x = 0; x < data.detailWidth; x++)
                {
                    for (int y = 0; y < data.detailHeight; y++)
                    {
                        //The shader uses different UV's than Unity terrain.. or I fucked up the conversion to terrain...
                        var index = x * data.detailHeight + y;
                        colors[index] =  new Color(details[0][x,y], details[1][x,y], details[2][x,y], details[3][x,y]) / 16;
                    }
                }
                
                var tex = new Texture2D(data.detailWidth, data.detailHeight, TextureFormat.ARGB32, false, true);
                tex.SetPixels(colors);

                GrassTextureUtility.SaveTextureToFile(path, tex);
            }
        }
    }
}
