using UnityEngine;

namespace StixGames.GrassShader
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Stix Games/Interaction/Interaction Object", 1)]
    public class InteractionObject : MonoBehaviour, IInteractionMesh
    {
        private MeshFilter _meshFilter;

        public MeshFilter MeshFilter
        {
            get { return _meshFilter ? _meshFilter : (_meshFilter = GetComponent<MeshFilter>()); }
        }

        private Renderer _renderer;

        public Renderer Renderer
        {
            get { return _renderer ? _renderer : (_renderer = GetComponent<Renderer>()); }
        }

        private void OnEnable()
        {
            RenderTextureInteraction.AddInteractionObject(this);
        }

        private void OnDisable()
        {
            RenderTextureInteraction.RemoveInteractionObject(this);
        }

        public Mesh GetMesh()
        {
            return MeshFilter == null ? null : MeshFilter.sharedMesh;
        }

        public Matrix4x4 GetMatrix()
        {
            return transform.localToWorldMatrix;
        }

        public Material GetMaterial()
        {
            return Renderer.sharedMaterial;
        }
    }
}