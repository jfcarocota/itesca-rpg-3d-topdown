using System;
using StixGames.NatureCore;
using StixGames.NatureCore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace StixGames.GrassShader
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(NatureMeshFilter))]
    [AddComponentMenu("Stix Games/General/Grass Renderer", 1)]
    public class GrassRenderer : MonoBehaviour
    {
        public Material Material;
        [LayerField] public int Layer;
        public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;
        public bool ReceiveShadows = true;

        [Space]
        public bool AutoSetFloorColor = false;

        private NatureMeshFilter _natureMeshFilter;
        public NatureMeshFilter NatureMeshFilter
        {
            get { return _natureMeshFilter != null ? _natureMeshFilter : (_natureMeshFilter = GetComponent<NatureMeshFilter>()); }
        }

        private void Start()
        {
            if (AutoSetFloorColor)
            {
                UpdateFloorColor();
            }
        }

        private void LateUpdate()
        {
            if (Material == null)
            {
                return;
            }

            if (!GrassUtility.IsGrassMaterial(Material))
            {
                throw new InvalidOperationException("GrassMaterial does not use the DX11 Grass Shader");
            }

            if (Application.isEditor && !Application.isPlaying && AutoSetFloorColor)
            {
                UpdateFloorColor();
            }

            if (Material.shader.isSupported)
            {
                var meshes = NatureMeshFilter.GetMeshes();
                foreach (var mesh in meshes)
                {
                    //Update bounds with grass height
                    var original = mesh.bounds;
                    var modified = original;
                    modified.Expand(GrassUtility.GetMaxGrassHeight(Material));
                    mesh.bounds = modified;

                    //Draw mesh
                    Graphics.DrawMesh(mesh, transform.localToWorldMatrix, Material, Layer, null, 0, null, ShadowCastingMode, ReceiveShadows);

                    //Restore bounds
                    mesh.bounds = original;
                }
            }
        }

        private void UpdateFloorColor()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                var floorMaterial = renderer.sharedMaterial;

                var baseColor = floorMaterial.color;
                var colorTexture = floorMaterial.mainTexture;
                var offset = floorMaterial.mainTextureOffset;
                var scale = floorMaterial.mainTextureScale;

                GrassUtility.SetFloorColor(Material, baseColor);
                GrassUtility.SetFloorColorTexture(Material, colorTexture, offset, scale);
                return;
            }

            var terrain = GetComponent<Terrain>();
            if (terrain != null)
            {
                //TODO: Add terrain support for this

                Debug.LogWarning("Auto setting floor color isn't supported for terrain yet.");
            }
        }
    }
}
