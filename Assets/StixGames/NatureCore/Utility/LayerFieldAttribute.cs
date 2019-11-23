using System;
using UnityEngine;

namespace StixGames.NatureCore.Utility
{
    /// <summary>
    /// Attribute to select a single layer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class LayerFieldAttribute : PropertyAttribute
    {
    }
}