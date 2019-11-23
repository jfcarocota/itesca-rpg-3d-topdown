using UnityEditor;
using UnityEngine;

namespace StixGames.GrassShader
{
    [CustomEditor(typeof(GrassRenderer))]
    [CanEditMultipleObjects]
    public class GrassRendererEditor : Editor
    {
        private MaterialEditor materialEditor;
        private Material material;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            if (targets.Length > 1)
            {
                EditorGUILayout.LabelField("You can't edit the material when more than one object is selected.");
                return;
            }

            var grassRenderer = (GrassRenderer)target;

            if (grassRenderer.Material == null)
            {
                return;
            }

            if (!GrassUtility.IsGrassMaterial(grassRenderer.Material))
            {
                EditorGUILayout.LabelField("You material doesn't use the DX11 Grass Shader!");
            }

            //Update the material editor if necessary
            if (materialEditor == null || grassRenderer.Material != material)
            {
                ClearMaterialEditor();

                material = grassRenderer.Material;
                materialEditor = (MaterialEditor)CreateEditor(material);
            }

            materialEditor.DrawHeader();
            bool isDefaultMaterial = !AssetDatabase.GetAssetPath(grassRenderer.Material).StartsWith("Assets");
            using (new EditorGUI.DisabledScope(isDefaultMaterial))
            {
                //Just trying to get the material editor to look as close to the original as possible.
                //Unity uses a lot of closed off stuff here, unfortunately
#if UNITY_5
                materialEditor.SetDefaultGUIWidths();
                var offset = EditorGUIUtility.currentViewWidth * 0.55f -64f;
                EditorGUIUtility.fieldWidth += offset - 10;
                EditorGUIUtility.labelWidth -= offset + 15;
#endif

                //Draw the material editor
                materialEditor.OnInspectorGUI();
            }
        }

        private void OnDisable()
        {
            ClearMaterialEditor();
        }

        private void ClearMaterialEditor()
        {
            if (materialEditor != null)
            {
                DestroyImmediate(materialEditor);
            }
        }
    }
}
