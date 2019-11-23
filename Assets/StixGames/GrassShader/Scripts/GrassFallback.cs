using System;
using System.Collections.Generic;
using StixGames.NatureCore;
using StixGames.NatureCore.UnityOctree;
using StixGames.NatureCore.Utility;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace StixGames.GrassShader
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(NatureMeshFilter), typeof(GrassRenderer))]
    [AddComponentMenu("Stix Games/General/Grass Fallback", 2)]
    public class GrassFallback : MonoBehaviour
    {
        [Header("Texture Atlas Render Settings")]
        public GrassFallbackProcessor PreProcessor = new GrassFallbackProcessor();

        [Header("Random sampling")]
        [Tooltip("The seed used to generate the levels of detail")]
        public int Seed = 12345;

        [Header("Performance")]
        [Tooltip("If enabled, the fallback will use GPU instancing to speed up the rendering. If your hardware supports this, you should use it.")]
        public bool UseInstancing = true;

        [Tooltip("The different levels of detail for your grass fallback.")]
        public FallbackLOD[] LevelsOfDetail = new FallbackLOD[]
        {
            new FallbackLOD
            {
                Density = 0.5f,
                FadeStart = 20,
                FadeEnd = 40,
            },
            new FallbackLOD
            {
                Density = 0.1f,
                FadeStart = 40,
                FadeEnd = 100,
            },
        };

        [Tooltip("The number of billboards each spot of grass will use.")]
        public int BillboardsPerSpot = 3;

        [Space]
        [Tooltip("The fallback uses an octree to optimize rendering. " +
                 "This setting defines how granular the octree will be approximately.\n" +
                 "You can use this to fine tune your optimization. Requires clearing the cache to take effect.")]
        public float BillboardsPerOctreeNode = 5;

        [Header("Smoothing")]
        public bool SmoothTexture = true;
        public bool SmoothWidth;
        public bool SmoothHeight;

        [Header("Visuals")]
        [Tooltip("The layer the fallback grass will be put on.")]
        [LayerField] public int FallbackLayer = 0;
        public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;
        public bool ReceiveShadows = true;

        [Space]
        [Tooltip("Stretches the billboard width")]
        public float WidthMultiplier = 1;

        [Space]
        [Range(0, 1)]
        [Tooltip("The alpha value at which the texture will be cut off")]
        public float TextureCutoff = 0.2f;

        [Space]
        [Tooltip("If enabled, the materials subsurface scattering parameter will be overridden.")]
        public bool OverrideSubsurfaceScattering = false;

        [Range(0, 1)]
        [Tooltip("The subsurface scattering value that will be used if override is enabled.")]
        public float SubsurfaceScattering = 0.5f;

        private readonly List<BoundsOctree<FallbackPosition>> lodOctrees = new List<BoundsOctree<FallbackPosition>>();

        private readonly List<Mesh> meshes = new List<Mesh>();
        private DensityMode densityMode;
        private Texture2D densityTexture;
        private Vector4 densityValues;

        // /<summary>
        // /The largest number of billboards that fits into a mesh
        // /</summary>
        private const int MaxBillboards = 8191;

        // 4 front, 4 back
        private const int MaxVertices = MaxBillboards * 8;

        // Two front, two back triangles, each consisting of 3 points
        private const int TrianglesPerMesh = MaxBillboards * 3 * 4;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<Vector2> atlasIndices01 = new List<Vector2>();
        private readonly List<Vector2> atlasIndices23 = new List<Vector2>();
        private readonly List<Color> posAndSmoothing = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        // This is defined by the hardware and Unity itself.
        private const int MaxInstances = 500;
        private Mesh instanceMesh;
        private readonly Matrix4x4[] instanceMatrices = new Matrix4x4[MaxInstances];
        private readonly Vector4[] materialBlockIndices = new Vector4[MaxInstances];
        private readonly Vector4[] materialBlockPosAndSmoothing = new Vector4[MaxInstances];
        private int instanceCount = 0;

        private NatureMeshFilter _natureMeshFilter;
        public NatureMeshFilter NatureMeshFilter
        {
            get { return _natureMeshFilter != null ? _natureMeshFilter : (_natureMeshFilter = GetComponent<NatureMeshFilter>()); }
        }

        private GrassRenderer _grassRenderer;
        public GrassRenderer GrassRenderer
        {
            get { return _grassRenderer != null ? _grassRenderer : (_grassRenderer = GetComponent<GrassRenderer>()); }
        }

        public void Start()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            if (GrassRenderer.Material == null)
            {
                Debug.LogWarning("Your grass renderer requires a material, or the fallback won't work.");
                return;
            }

            densityTexture = GrassRenderer.Material.GetTexture("_DensityTexture") as Texture2D;
            densityValues = GrassRenderer.Material.GetVector("_DensityValues");

            densityMode = GrassUtility.GetDensityMode(GrassRenderer.Material);

            InitializeLevelsOfDetail();
        }

        private void InitializeLevelsOfDetail()
        {
            // Don't do anything if the material hasn't been processed yet
            if (!PreProcessor.IsProcessed)
            {
                return;
            }

            // Don't update if nothing has changed
            if (lodOctrees.Count == LevelsOfDetail.Length)
            {
                return;
            }

            // Clear just to be save
            lodOctrees.Clear();

            // Get the triangles of the fallback mesh
            var meshTriangles = NatureMeshFilter.GetTriangleList(true);

            // Get a random position for the grass billboard
            var rng = new System.Random(Seed);
            var posSampler = new Sampler2D(new HaltonSampler(2, rng.Range(1, 1000)),
                new HaltonSampler(3, rng.Range(1, 1000)));
            var rngSampler = new RandomSampler(rng.Next());

            foreach (var lod in LevelsOfDetail)
            {
                var minNodeSize = BillboardsPerOctreeNode * (1.0f / lod.Density);
                var octree = new BoundsOctree<FallbackPosition>(minNodeSize, transform.position, minNodeSize, 1.2f);
                foreach (var triangle in meshTriangles)
                {
                    var billboardCount = triangle.Area * lod.Density;

                    if (billboardCount < 1 && rngSampler.NextFloat() > billboardCount)
                    {
                        continue;
                    }

                    for (int i = 0; i < billboardCount; i++)
                    {
                        // Randomly generate the position of the billboard
                        var coords = triangle.GetRandomCoordinates(posSampler);

                        Vector4 density = Vector4.one;
                        switch (densityMode)
                        {
                            case DensityMode.Value:
                                density = densityValues;
                                break;
                            case DensityMode.Vertex:
                                density = triangle.GetVertexColor(coords);
                                break;
                            case DensityMode.Texture:
                                if (densityTexture != null)
                                {
                                    var densityUV = triangle.GetUVCoordinates(coords);
                                    density = densityTexture.GetPixelBilinear(densityUV.x, densityUV.y);
                                }

                                break;
                        }

                        // Ignore this billboard if the density is too low
                        if (density.x < PreProcessor.DensityCutoff && density.y < PreProcessor.DensityCutoff &&
                            density.z < PreProcessor.DensityCutoff && density.w < PreProcessor.DensityCutoff)
                        {
                            continue;
                        }

                        // Calculate position
                        var pos = triangle.GetPosition(coords);
                        var up = triangle.Normal;

                        // Generate forward first, so right will be based on the normal
                        var baseForward = rng.RandomUnitCircle();

                        var atlasIndices = new Vector4(
                            PreProcessor.LookupTable.GetRandomTexture(density.x, 0, rngSampler),
                            PreProcessor.LookupTable.GetRandomTexture(density.y, 1, rngSampler),
                            PreProcessor.LookupTable.GetRandomTexture(density.z, 2, rngSampler),
                            PreProcessor.LookupTable.GetRandomTexture(density.w, 3, rngSampler)
                        );

                        // All indices are set to render nothing.
                        if (atlasIndices.x < 0 && atlasIndices.y < 0 && atlasIndices.z < 0 && atlasIndices.w < 0)
                        {
                            continue;
                        }

                        var fallbackPosition = new FallbackPosition
                        {
                            ModelMatrix = UseInstancing
                                ? Matrix4x4.TRS(pos, Quaternion.LookRotation(baseForward, up),
                                    Vector3.one)
                                : Matrix4x4.identity,
                            AtlasIndices = atlasIndices,
                            Pos = pos,
                            BaseForward = baseForward,
                            Up = up,
                        };

                        // Calculate the billboards bounds
                        var halfWidth = PreProcessor.BillboardWidth * 0.5f;
                        var forward = baseForward * halfWidth;
                        var side = Vector3.Cross(forward, up) * halfWidth;

                        var bounds = new Bounds(pos, Vector3.zero);
                        bounds.Encapsulate(pos + up * PreProcessor.BillboardHeight);
                        bounds.Encapsulate(pos + forward);
                        bounds.Encapsulate(pos - forward);
                        bounds.Encapsulate(pos + side);
                        bounds.Encapsulate(pos - side);

                        octree.Add(fallbackPosition, bounds);
                    }
                }

                lodOctrees.Add(octree);
            }
        }

        public void ClearFallback()
        {
            lodOctrees.Clear();
        }

        public void UpdateFallback()
        {
            ClearFallback();
            Initialize();
        }

        private void OnEnable()
        {
            Camera.onPreCull += UpdateGrassFallback;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= UpdateGrassFallback;
        }

        public void UpdateGrassFallback(Camera current)
        {
            Profiler.BeginSample("GrassFallback.UpdateGrassFallback");

            // Debug turn off rending in editor scene, so we can look at the main camera from above
            //if (current != Camera.main)
            //{
            //   return;
            //}

            // This can happen sometimes, at least in the editor
            if (current == null)
            {
                return;
            }

            // Don't render to preview views
            if (current.cameraType == CameraType.Preview)
            {
                return;
            }

            // If the grass renderer isn't set up, don't render the fallback
            if (GrassRenderer.Material == null)
            {
                return;
            }

            // In case something goes wrong, do nothing
            if (PreProcessor.FallbackMaterial == null)
            {
                return;
            }

            // Don't calculate or render grass fallback if the grass renderer is enabled
            if (GrassRenderer.enabled && GrassRenderer.Material.shader.isSupported)
            {
                return;
            }

#if UNITY_EDITOR
            // Debug initialize after hot reload
            Initialize();
#endif

            if (UseInstancing)
            {
                // Regenerate every frame, in case BillboardsPerSpot is changed.
                InitializeInstancing();
                instanceCount = 0;
            }

            PreProcessor.FallbackMaterial.enableInstancing = UseInstancing;
            PreProcessor.FallbackMaterial.SetFloat("_Cutoff", TextureCutoff);

            if (OverrideSubsurfaceScattering)
            {
                PreProcessor.FallbackMaterial.SetFloat("_Subsurface", SubsurfaceScattering);
            }

            CleanLists();

            var cameraPos = current.transform.position;

            if (LevelsOfDetail == null || LevelsOfDetail.Length == 0)
            {
                return;
            }

            // Iterate all fallback billboards within the camera frustum
            for (var lodLevel = 0; lodLevel < LevelsOfDetail.Length; lodLevel++)
            {
                var lod = LevelsOfDetail[lodLevel];
                var octree = lodOctrees[lodLevel];

                var planes =
                    NatureGeometryUtility.CalculateFrustumPlanes(current, lod.FadeEnd, PreProcessor.BillboardHeight);

                // Iterate all billboards in this fallback level of detail
                var targetPositions = octree.GetWithinFrustum(planes);
                foreach (var position in targetPositions)
                {
                    // Smooth billboards close to the camera to max
                    var smoothVal = Mathf.Clamp01(1 - ((position.Pos - cameraPos).magnitude - lod.FadeStart) / (lod.FadeEnd - lod.FadeStart));

                    // Render cached billboards
                    if (UseInstancing)
                    {
                        instanceMatrices[instanceCount] = position.ModelMatrix;
                        materialBlockIndices[instanceCount] = position.AtlasIndices;
                        materialBlockPosAndSmoothing[instanceCount] = new Vector4(position.Pos.x,
                            position.Pos.y, position.Pos.z, smoothVal);
                        instanceCount++;

                        if (instanceCount >= instanceMatrices.Length)
                        {
                            RenderInstances(current);
                            instanceCount = 0;
                        }
                    }
                    else
                    {
                        for (int j = 0; j < BillboardsPerSpot; j++)
                        {
                            var forward = Quaternion.Euler(0, j * (180.0f / BillboardsPerSpot), 0) *
                                          position.BaseForward;
                            var right = Vector3.Cross(position.Up, forward);
                            GenerateBillboard(position.Pos, right, position.Up, forward, position.AtlasIndices,
                                smoothVal);
                        }
                    }
                }
            }

            if (UseInstancing)
            {
                // Render the last instance
                RenderInstances(current);
                instanceCount = 0;

                // Delete all meshes, in case the mode was switched
                SetMeshRendererRequired(0);
            }
            else
            {
                // Sets the amount of required mesh renderers for the given triangles, 
                // by dividing by the max vertices per mesh and rounding it up
                SetMeshRendererRequired((vertices.Count + MaxVertices - 1) / MaxVertices);

                ClearMeshes();
                SetMeshData();
                UpdateMeshes();

                RenderMeshes(current);
            }

            Profiler.EndSample();
        }

        private void InitializeInstancing()
        {
            // Mesh
            CleanLists();

            for (int i = 0; i < BillboardsPerSpot; i++)
            {
                var rotPerBillboard = 180f / BillboardsPerSpot;
                var rot = Quaternion.Euler(0, rotPerBillboard * i, 0);
                GenerateBillboard(Vector3.zero, rot * Vector3.right, rot * Vector3.up, Vector3.forward, Vector4.zero, 1);
            }

            if (instanceMesh == null)
            {
                instanceMesh = new Mesh();
            }

            instanceMesh.Clear();
            instanceMesh.SetVertices(vertices);
            instanceMesh.SetUVs(0, uv);
            instanceMesh.SetNormals(normals);
            instanceMesh.SetColors(posAndSmoothing);
            instanceMesh.SetTriangles(triangles, 0);

            instanceMesh.RecalculateBounds();
            instanceMesh.RecalculateNormals();
            instanceMesh.RecalculateTangents();

            CleanLists();
        }

        private void RenderInstances(Camera current)
        {
            Profiler.BeginSample("GrassFallback.RenderInstances");

            if (SmoothTexture)
            {
                PreProcessor.FallbackMaterial.EnableKeyword("SMOOTH_TEXTURE");
            }
            else
            {
                PreProcessor.FallbackMaterial.DisableKeyword("SMOOTH_TEXTURE");
            }

            if (SmoothHeight)
            {
                PreProcessor.FallbackMaterial.EnableKeyword("SMOOTH_HEIGHT");
            }
            else
            {
                PreProcessor.FallbackMaterial.DisableKeyword("SMOOTH_HEIGHT");
            }

            if (SmoothWidth)
            {
                PreProcessor.FallbackMaterial.EnableKeyword("SMOOTH_WIDTH");
            }
            else
            {
                PreProcessor.FallbackMaterial.DisableKeyword("SMOOTH_WIDTH");
            }

            var properties = new MaterialPropertyBlock();
            properties.SetVectorArray("_InstanceAtlasIndices", materialBlockIndices);
            properties.SetVectorArray("_InstancePosSmoothing", materialBlockPosAndSmoothing);
            Graphics.DrawMeshInstanced(instanceMesh, 0, PreProcessor.FallbackMaterial, instanceMatrices, instanceCount,
                properties, ShadowCastingMode, ReceiveShadows, FallbackLayer, current);

            Profiler.EndSample();
        }

        private void RenderMeshes(Camera current)
        {
            Profiler.BeginSample("GrassFallback.RenderMeshes");

            if (SmoothTexture)
            {
                PreProcessor.FallbackMaterial.EnableKeyword("SMOOTH_TEXTURE");
            }
            else
            {
                PreProcessor.FallbackMaterial.DisableKeyword("SMOOTH_TEXTURE");
            }

            // We don't need any changes between individual meshes.
            var properties = new MaterialPropertyBlock();

            foreach (var mesh in meshes)
            {
                Graphics.DrawMesh(mesh, Matrix4x4.identity, PreProcessor.FallbackMaterial, FallbackLayer, current, 0,
                    properties, ShadowCastingMode, ReceiveShadows);
            }

            Profiler.EndSample();
        }

        private void SetMeshData()
        {
            for (var i = 0; i < meshes.Count; i++)
            {
                // Vertices
                meshes[i].SetVertices(vertices.GetRange(i * MaxVertices,
                    Math.Min(MaxVertices, vertices.Count - i * MaxVertices)));

                // Normals
                meshes[i].SetNormals(normals.GetRange(i * MaxVertices,
                    Math.Min(MaxVertices, normals.Count - i * MaxVertices)));

                // UV
                meshes[i].SetUVs(0,
                    uv.GetRange(i * MaxVertices, Math.Min(MaxVertices, uv.Count - i * MaxVertices)));

                // Atlas indices, inside texture coordinates
                meshes[i].SetUVs(1,
                    atlasIndices01.GetRange(i * MaxVertices,
                        Math.Min(MaxVertices, atlasIndices01.Count - i * MaxVertices)));
                meshes[i].SetUVs(2,
                    atlasIndices23.GetRange(i * MaxVertices,
                        Math.Min(MaxVertices, atlasIndices23.Count - i * MaxVertices)));

                // Position and smoothing value, inside vertex color
                meshes[i].SetColors(posAndSmoothing.GetRange(i * MaxVertices,
                    Math.Min(MaxVertices, posAndSmoothing.Count - i * MaxVertices)));

                // Triangles
                meshes[i].SetTriangles(
                    triangles.GetRange(i * TrianglesPerMesh,
                        Math.Min(TrianglesPerMesh, triangles.Count - i * TrianglesPerMesh)), 0);
            }
        }

        private void GenerateBillboard(Vector3 pos, Vector3 right, Vector3 up, Vector3 forward, Vector4 atlasIndices,
            float lodSmoothing)
        {
            Profiler.BeginSample("GrassFallback.GenerateBillboard");

            float colorAlpha = 1.0f;

            if (SmoothHeight)
            {
                up *= lodSmoothing;
            }

            if (SmoothWidth)
            {
                right *= lodSmoothing;
            }

            if (SmoothTexture)
            {
                colorAlpha *= lodSmoothing;
            }

            var originalPos = pos;
            right *= PreProcessor.BillboardWidth * WidthMultiplier;
            up *= PreProcessor.BillboardHeight;
            pos -= right * 0.5f;
            int index = vertices.Count % MaxVertices;

            // Add vertices, one for back, one for front, but in the same order, so the uv's will match front and back
            vertices.Add(pos);
            vertices.Add(pos + up);
            vertices.Add(pos + right + up);
            vertices.Add(pos + right);
            vertices.Add(pos);
            vertices.Add(pos + up);
            vertices.Add(pos + right + up);
            vertices.Add(pos + right);

            // Normals
            // Front
            normals.Add(-forward);
            normals.Add(-forward);
            normals.Add(-forward);
            normals.Add(-forward);
            // Back
            normals.Add(forward);
            normals.Add(forward);
            normals.Add(forward);
            normals.Add(forward);

            // Uv
            uv.Add(new Vector2(0, 0));
            uv.Add(new Vector2(0, 1));
            uv.Add(new Vector2(1, 1));
            uv.Add(new Vector2(1, 0));
            uv.Add(new Vector2(0, 0));
            uv.Add(new Vector2(0, 1));
            uv.Add(new Vector2(1, 1));
            uv.Add(new Vector2(1, 0));

            // Indices
            var atlasIndexVector01 = new Vector2(atlasIndices.x, atlasIndices.y);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            atlasIndices01.Add(atlasIndexVector01);
            var atlasIndexVector23 = new Vector2(atlasIndices.z, atlasIndices.w);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);
            atlasIndices23.Add(atlasIndexVector23);

            // Center position and smoothing
            posAndSmoothing.Add(new Color(originalPos.x, 0, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 1, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 1, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 0, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 0, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 1, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 1, originalPos.z, colorAlpha));
            posAndSmoothing.Add(new Color(originalPos.x, 0, originalPos.z, colorAlpha));

            // Triangles
            // Bottom front
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);

            // Bottom back
            triangles.Add(index + 4);
            triangles.Add(index + 4 + 2);
            triangles.Add(index + 4 + 1);

            // Top front
            triangles.Add(index + 0);
            triangles.Add(index + 2);
            triangles.Add(index + 3);

            // Top back
            triangles.Add(index + 4 + 0);
            triangles.Add(index + 4 + 3);
            triangles.Add(index + 4 + 2);

            Profiler.EndSample();
        }

        ///<summary>
        /// Sets the amount of mesh renderers to exactly this amount, they will be created and deleted as necessary.
        ///</summary>
        ///<param name="count"></param>
        private void SetMeshRendererRequired(int count)
        {
            // Create new meshes
            while (count > meshes.Count)
            {
                var mesh = new Mesh();
                mesh.name = string.Format("{0} Grass Fallback", gameObject.name);
                mesh.MarkDynamic();
                meshes.Add(mesh);
            }

            // Delete meshes that are larger than the required
            while (count < meshes.Count)
            {
#if UNITY_EDITOR
                DestroyImmediate(meshes[0]);
#else
                Destroy(meshes[0]);
#endif
                meshes.RemoveAt(0);
            }
        }

        private void UpdateMeshes()
        {
            foreach (var mesh in meshes)
            {
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
            }
        }

        private void CleanLists()
        {
            vertices.Clear();
            normals.Clear();
            uv.Clear();
            atlasIndices01.Clear();
            atlasIndices23.Clear();
            posAndSmoothing.Clear();
            triangles.Clear();
        }

        private void ClearMeshes()
        {
            foreach (var mesh in meshes)
            {
                mesh.Clear();
            }
        }

        [Serializable]
        public class FallbackLOD
        {
            [Tooltip("Billboards per square unit.")]
            public float Density = 0.001f;

            [Tooltip("The distance at which the level of detail start to fade away.")]
            public float FadeStart = 100;

            [Tooltip("The distance at which the level of detail has faded away completely.")]
            public float FadeEnd = 150;
        }

        private class FallbackPosition
        {
            public Matrix4x4 ModelMatrix;
            public Vector4 AtlasIndices;
            public Vector3 Pos, BaseForward, Up;
        }
    }
}