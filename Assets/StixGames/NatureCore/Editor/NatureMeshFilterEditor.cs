using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StixGames.NatureCore
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(NatureMeshFilter))]
    public class NatureMeshFilterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button(new GUIContent("Update cache", "When you change your mesh, the old one can still be cached here. Press this button to update it.")))
            {
                ResetMeshes();
            }

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                base.OnInspectorGUI();

                if (check.changed)
                {
                    ResetMeshes();
                }
            }
        }

        private void ResetMeshes()
        {
            foreach (var t in targets.Cast<NatureMeshFilter>())
            {
                t.ResetCache();
            }
        }
    }
}
