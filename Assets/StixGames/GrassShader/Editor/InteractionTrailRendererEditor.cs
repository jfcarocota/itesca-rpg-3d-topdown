using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StixGames.GrassShader
{
    [CustomEditor(typeof(InteractionTrailRenderer))]
    public class InteractionTrailRendererEditor : Editor
    {
        private SerializedProperty layer;

        public override void OnInspectorGUI()
        {
            var trailRenderers = targets.Cast<InteractionTrailRenderer>();

            if (GUILayout.Button(new GUIContent("Clear Trail")))
            {
                foreach (var interactionTrailRenderer in trailRenderers)
                {
                    interactionTrailRenderer.ClearPath();
                }
            }

            base.OnInspectorGUI();
        }
    }
}