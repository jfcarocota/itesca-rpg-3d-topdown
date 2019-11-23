using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StixGames.GrassShader
{
    [CustomEditor(typeof(GrassTextureAtlasTemplate))]
    [CanEditMultipleObjects]
    public class GrassTextureAtlasTemplateEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            if (targets.Length > 1)
            {
                EditorGUILayout.LabelField(
                    "You can't create the texture atlas when more than one atlas template is selected.");
                return;
            }

            var template = (GrassTextureAtlasTemplate) target;

            if (template.Textures.Count == 0 || template.Textures.All(x => x == null))
            {
                EditorGUILayout.LabelField("No textures in the atlas!");
                return;
            }

            var result = template.Process();

            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            EditorGUILayout.LabelField(new GUIContent("Duplicate Textures",
                    "The number of textures that had to be rendered twice to fill " +
                    "the atlas with the current settings. " +
                    "Add more textures or reduce row count, to avoid duplication."),
                new GUIContent(result.DuplicateTextures.ToString()));
            EditorGUILayout.LabelField(new GUIContent("Wasted Space",
                    "The amount of space on the texture atlas that was " +
                    "wasted, because some textures are wider than others. " +
                    "Ideally use only texture with similar width in the same atlas."),
                new GUIContent(string.Format("{0:F1}%", result.WastedSpace * 100), "The amount of empty space between textures. If your blades of grass have approximately the same size, there will be less wasted space."));
            EditorGUILayout.LabelField(new GUIContent("Atlas Width", "The amount of textures per row"),
                new GUIContent(result.AtlasWidth.ToString()));
            EditorGUILayout.LabelField(new GUIContent("Atlas Height", "The amount of rows of textures"),
                new GUIContent(result.AtlasHeight.ToString()));

            EditorGUILayout.Space();

            if (template.TextureAtlas != null && GUILayout.Button("Update texture"))
            {
                var path = AssetDatabase.GetAssetPath(template.TextureAtlas);

                result = template.Process(path);
                template.TextureAtlas = result.TextureAtlas;
            }

            if (GUILayout.Button(template.TextureAtlas == null ? "Create texture" : "Create new texture"))
            {
                var path = EditorUtility.SaveFilePanelInProject("Save texture atlas", "Texture Atlas", "png",
                    "Select where you want to save the texture atlas");

                result = template.Process(path);
                template.TextureAtlas = result.TextureAtlas;
            }
        }
    }

    [CustomPreview(typeof(GrassTextureAtlasTemplate))]
    public class GrassTextureAtlasTemplatePreview : ObjectPreview
    {
        public override bool HasPreviewGUI()
        {
            var template = (GrassTextureAtlasTemplate)target;
            return template.TextureAtlas != null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var template = (GrassTextureAtlasTemplate) target;

            if (r.height < r.width)
            {
                var originalWidth = r.width;
                r.width = r.height;
                r.x += (originalWidth - r.width) / 2;
            }
            else
            {
                var originalHeight = r.height;
                r.height = r.width;
                r.y += (originalHeight - r.height) / 2;
            }

            //GL.Begin(GL.QUADS);
            //GL.Vertex3(r.xMin, r.yMin, 0);
            //GL.Vertex3(r.xMin, r.yMax, 0);
            //GL.Vertex3(r.xMax, r.yMax, 0);
            //GL.Vertex3(r.xMax, r.yMin, 0);
            //GL.End();

            Graphics.DrawTexture(r, template.TextureAtlas);
        }
    }
}