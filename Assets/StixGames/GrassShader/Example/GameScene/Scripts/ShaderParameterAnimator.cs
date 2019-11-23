using UnityEngine;

namespace StixGames.GrassShader.Example
{
    public class ShaderParameterAnimator : MonoBehaviour
    {
        public string ShaderParameter;
        public float Value;

        public void Update()
        {
            var material = GetComponent<Renderer>().material;
            material.SetFloat(ShaderParameter, Value);
        }
    }
}