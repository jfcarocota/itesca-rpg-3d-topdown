using StixGames.NatureCore.Utility;
using UnityEditor;
using UnityEngine;

namespace StixGames.GrassShader
{
    [CustomPropertyDrawer(typeof(LayerFieldAttribute))]
    class LayerFieldAttributeEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.intValue = EditorGUI.LayerField(position, label, property.intValue);
        }
    }
}