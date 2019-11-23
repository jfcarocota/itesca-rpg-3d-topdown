using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StixGames.NatureCore.Utility;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StixGames.GrassShader
{
    [Serializable]
    public class GrassFallbackProcessor
    {
        [Tooltip("This number will define how the grass for the texture atlas will be randomied.")]
        [Header("General")]
        public int Seed = 12345;

        [Tooltip("This specifies how many blades of grass will be generated with full density.")]
        public int BladesOfGrass = 25;

        [Tooltip("If disabled, the renderer will not create a specular texture for this material. " +
                 "This can be used to prevent intense specular highlights, that can occur because the normals on grass fall back are different than for real grass.")]
        public bool RenderSpecularTexture = true;

        [Header("Texture Atlas")]
        public int TargetTextureSize = 4096;
        public int RowsPerGrassType = 2;

        [Tooltip("Anti aliasing for the texture atlas. Valid values are 1, 2, 4, and 8")]
        public int TextureAntiAliasing = 4;

        [Tooltip("Padding between textures, in pixels")]
        public int TexturePadding = 10;

        [Tooltip("Bleeds out the color of the texture, to prevent black borders around the texture.")]
        public int DilationSteps = 50;

        /// <summary>
        /// Densities below this value will be ignored, they will not get their own spot in the texture atlas
        /// </summary>
        [Header("Density")]
        [Range(0, 1)]
        [Tooltip("Densities below this value will be ignored, they will not get their own spot in the texture atlas. Setting this value too low will result in empty spots in the texture atlas.")]
        public float DensityCutoff = 0.05f;

        /// <summary>
        /// The fallback material will randomize the fallback density with this span.
        /// So if it has a value of 0.1 a density of 0.5 will randomly use textures for 0.4 and 0.6 as well,
        /// to add variation.
        /// </summary>
        [Tooltip("To add variation, the fallback can select a texture that was rendered for a higher or a lower density, within this span.")]
        [Range(0, 0.5f)]
        public float DensityRandomization = 0.1f;

        [Space]
        [Tooltip("EXPERIMENTAL: If enabled, the four density types will be balanced, so the types that don't occur much, or at all have less space on the atlas.")]
        public bool BalanceDensities = false;

        [Space]
        [Tooltip("When creating the histogram for density distributions, this value defines how many times the texture is sampled.")]
        public int HistogramSamples = 100000;

        [Tooltip("The precision the texture lookup table will use. E.g. when this has a value of 5, the densities 0 to 0.2 will be seen as the same density.")]
        public int LookupTableEntries = 100;

        [HideInInspector] public float BillboardWidth;
        [HideInInspector] public float BillboardHeight;

        public int TextureColumns
        {
            get { return RowsPerGrassType * 4; }
        }

        public int TextureRows
        {
            get { return RowsPerGrassType * 4; }
        }

        public int TotalTextures
        {
            get { return TextureColumns * TextureRows; }
        }

        public int TexturesPerGrassType
        {
            get { return RowsPerGrassType * TextureColumns; }
        }

        public bool IsProcessed
        {
            get { return FallbackMaterial != null && LookupTable != null && LookupTable.LookupTableEntries != 0; }
        }

        /// <summary>
        /// The mesh the material is used on, in case vertex density is used.
        /// </summary>
        [HideInInspector] public Mesh[] Meshes;

        /// <summary>
        /// The fallback material
        /// </summary>
        [Header("Internal")]
        public Material FallbackMaterial;

        private Texture2D albedoAtlasTexture;
        private Texture2D specularSmoothnessAtlasTexture;
        private Texture2D depthAtlasTexture;
        private Texture2D normalAtlasTexture;

        public TextureLookupTable LookupTable;

        private readonly List<float> densityList0 = new List<float>();
        private readonly List<float> densityList1 = new List<float>();
        private readonly List<float> densityList2 = new List<float>();
        private readonly List<float> densityList3 = new List<float>();

        private readonly List<List<float>> densityDistributions = new List<List<float>>();

        private TextureIndexRange textureIndices0;
        private TextureIndexRange textureIndices1;
        private TextureIndexRange textureIndices2;
        private TextureIndexRange textureIndices3;

        /// <summary>
        /// The original material that will be used to create <see cref="FallbackMaterial"/>.
        /// </summary>
        private Material originalMaterial;

        public void Process(Material originalMaterial)
        {
            if (originalMaterial == null)
            {
                throw new ArgumentNullException("originalMaterial");
            }

            this.originalMaterial = originalMaterial;

            if (!GrassUtility.IsGrassMaterial(originalMaterial))
            {
                throw new InvalidOperationException("OriginalMaterial must use the Stix Games Grass Shader");
            }

            var densityMode = GrassUtility.GetDensityMode(originalMaterial);

            //Clear previous
            Clear();

            //Create histograms, sorted lists with all density values
            switch (densityMode)
            {
                case DensityMode.Value:
                    CreateHistogramFromSingle(GrassUtility.GetValueDensity(originalMaterial));
                    break;
                case DensityMode.Vertex:
                    //Just selecting the colors will result in slightly wrong results, as the borders could be duplicated
                    //But this should not be significant
                    CreateHistogramFromMultiple(Meshes.SelectMany(x => x.colors).ToArray());
                    break;
                case DensityMode.Texture:
                    if (GrassUtility.GetDensityTexture(originalMaterial) == null)
                    {
                        CreateHistogramFromSingle(Vector4.one);
                    }
                    else
                    {
                        CreateHistogramFromMultiple(GrassUtility.GetDensityTexture(originalMaterial).GetPixels());
                    }
                    break;
            }

            //Sort lists
            densityList0.Sort();
            densityList1.Sort();
            densityList2.Sort();
            densityList3.Sort();

            //Calculate which density values should be used to create the texture
            CreateDensityDistribution();

            //Create texture atlas with different densities
            RenderTextureAtlas();
        }

        private void Clear()
        {
            ClearTextureIndices();
            ClearFallbackMaterial();
            ClearDensityLists();
        }

        private void ClearTextureIndices()
        {
            textureIndices0 = new TextureIndexRange(0, 0);
            textureIndices1 = new TextureIndexRange(0, 0);
            textureIndices2 = new TextureIndexRange(0, 0);
            textureIndices3 = new TextureIndexRange(0, 0);
        }

        private void ClearFallbackMaterial()
        {
            if (FallbackMaterial == null)
            {
                return;
            }

            var texture = FallbackMaterial.GetTexture("_TextureAtlas");
            var specSmooth = FallbackMaterial.GetTexture("_SpecularSmooth");
            var depth = FallbackMaterial.GetTexture("_Depth");
            var normal = FallbackMaterial.GetTexture("_Normal");

#if UNITY_EDITOR
            Object.DestroyImmediate(texture, true);
            Object.DestroyImmediate(specSmooth, true);
            Object.DestroyImmediate(depth, true);
            Object.DestroyImmediate(normal, true);
            Object.DestroyImmediate(FallbackMaterial, true);
#else
            Object.Destroy(texture);
            Object.Destroy(specSmooth);
            Object.Destroy(depth);
            Object.Destroy(normal);
            Object.Destroy(FallbackMaterial);
#endif
        }

        private void ClearDensityLists()
        {
            densityDistributions.Clear();
            densityList0.Clear();
            densityList1.Clear();
            densityList2.Clear();
            densityList3.Clear();
        }

        private Vector4 GetSingleDensity(float density, int type)
        {
            switch (type)
            {
                case 0:
                    return new Vector4(density, 0, 0, 0);
                case 1:
                    return new Vector4(0, density, 0, 0);
                case 2:
                    return new Vector4(0, 0, density, 0);
                case 3:
                    return new Vector4(0, 0, 0, density);
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        private void CreateHistogramFromSingle(Vector4 density)
        {
            densityList0.Add(density.x);
            densityList1.Add(density.y);
            densityList2.Add(density.z);
            densityList3.Add(density.w);
        }

        private void CreateHistogramFromMultiple(Color[] densities)
        {
            int stepSize = 1;
            if (densities.Length > HistogramSamples)
            {
                stepSize = densities.Length / HistogramSamples;
            }

            for (var i = 0; i < densities.Length; i += stepSize)
            {
                var color = densities[i];
                if (color.r > DensityCutoff)
                {
                    densityList0.Add(color.r);
                }

                if (color.g > DensityCutoff)
                {
                    densityList1.Add(color.g);
                }

                if (color.b > DensityCutoff)
                {
                    densityList2.Add(color.b);
                }

                if (color.a > DensityCutoff)
                {
                    densityList3.Add(color.a);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var list = GetDensityList(i);
                if (list.Count == 0)
                {
                    list.Add(0);
                }
            }
        }

        private IList<float> GetDensityList(int i)
        {
            switch (i)
            {
                case 0:
                    return densityList0;
                case 1:
                    return densityList1;
                case 2:
                    return densityList2;
                case 3:
                    return densityList3;
                default:
                    throw new ArgumentOutOfRangeException("i");
            }
        }

        private TextureIndexRange GetTextureIndex(int i)
        {
            switch (i)
            {
                case 0:
                    return textureIndices0;
                case 1:
                    return textureIndices1;
                case 2:
                    return textureIndices2;
                case 3:
                    return textureIndices3;
                default:
                    throw new ArgumentOutOfRangeException("i");
            }
        }

        private void SetTextureIndex(int i, TextureIndexRange range)
        {
            switch (i)
            {
                case 0:
                    textureIndices0 = range;
                    break;
                case 1:
                    textureIndices1 = range;
                    break;
                case 2:
                    textureIndices2 = range;
                    break;
                case 3:
                    textureIndices3 = range;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("i");
            }
        }

        /// <summary>
        /// Takes the densityLists and uses them to create the density distribution and a lookup table 
        /// </summary>
        private void CreateDensityDistribution()
        {
            //Create a lookup table
            LookupTable = new TextureLookupTable(LookupTableEntries, 4, TextureColumns);

            if (BalanceDensities)
            {
                var activeTextures = GrassUtility.GetGrassTypeCount(originalMaterial);

                //Sum up the amount of entries in all density lists
                var totalCount = 0;
                for (int i = 0; i < activeTextures; i++)
                {
                    totalCount += GetDensityList(i).Count;
                }

                //Balance the texture types, so types that exist only in very small areas have less density
                var nextStart = 0;
                for (int i = 0; i < activeTextures; i++)
                {
                    var textureCount = Mathf.RoundToInt(TotalTextures * ((float)GetDensityList(i).Count / totalCount));
                    SetTextureIndex(i, new TextureIndexRange(nextStart, nextStart + textureCount));
                    nextStart += textureCount;
                }
            }
            else
            {
                //Just cut the texture in 4 spaces
                textureIndices0 = new TextureIndexRange(0, TexturesPerGrassType);
                textureIndices1 = new TextureIndexRange(TexturesPerGrassType, 2 * TexturesPerGrassType);
                textureIndices2 = new TextureIndexRange(2 * TexturesPerGrassType, 3 * TexturesPerGrassType);
                textureIndices3 = new TextureIndexRange(3 * TexturesPerGrassType, 4 * TexturesPerGrassType);
            }

            //Median cut color quantization, but as all 4 channels are independent we can split each channel seperately
            //Cut the density in equal parts. 
            for (int i = 0; i < 4; i++)
            {
                var densityList = GetDensityList(i);
                var textureIndexRange = GetTextureIndex(i);
                int textureCount = textureIndexRange.Size;
                var medianCutStep = (double)densityList.Count / textureCount;

                var distribution = new List<float>();

                var cutStep = 0.0;
                for (int j = 0; j < textureIndexRange.Size; j++)
                {
                    int index = (int) cutStep;
                    distribution.Add(densityList[index]);

                    cutStep += medianCutStep;
                }

                densityDistributions.Add(distribution);
                AddLookupTableRow(distribution, i, textureIndexRange.Min);
            }
        }

        /// <summary>
        /// Creates <see cref="LookupTable"/>, a lookup table for densities to texture indices.
        /// </summary>
        /// <param name="densityDistribution"></param>
        /// <param name="startIndex"></param>
        private void AddLookupTableRow(List<float> densityDistribution, int rowIndex, int startIndex)
        {
            var row = new List<TextureIndexRange>();
            for (int x = 0; x < LookupTable.LookupTableEntries; x++)
            {
                var density = (float) x / LookupTable.LookupTableEntries;

                //Randomize between a few textures, to create variety
                var min = Mathf.Max(density - DensityRandomization, 0);
                var max = Mathf.Min(density + DensityRandomization, 1);

                var renderChance = Mathf.Clamp01(1.0f - (DensityCutoff - min) / (max - min));

                //Set the chance for rendering to 0, if there shouldn't be anything, at all
                if (density < DensityCutoff)
                {
                    renderChance = 0;
                }

                //When the amount of textures for this grass type is zero, never render.
                if (densityDistribution.Count == 0)
                {
                    renderChance = 0;
                }

                int minIndex = -1;
                int maxIndex = -1;
                for (int i = 0; i < densityDistribution.Count; i++)
                {
                    if (minIndex < 0 && densityDistribution[i] >= min)
                    {
                        minIndex = i;
                    }

                    if (maxIndex < 0 && densityDistribution[i] > max)
                    {
                        maxIndex = i;
                    }
                }

                //If the target density is larger than any in the lookup table, use the largest available.
                if (minIndex < 0)
                {
                    minIndex = densityDistribution.Count - 1;
                }
                if (maxIndex < 0)
                {
                    maxIndex = densityDistribution.Count - 1;
                }

                row.Add(new TextureIndexRange(startIndex + minIndex, startIndex + maxIndex + 1, renderChance));
            }
            LookupTable.SetRow(rowIndex, row);
        }

        private void RenderTextureAtlas()
        {
            //TODO: Use commandbuffer for speedup
            //Prepare rendertextures
            var tileSize = TargetTextureSize / TextureColumns;
            var albedoAtlas = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, TextureAntiAliasing);
            var specularSmoothnessAtlas = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, TextureAntiAliasing);
            var depthAtlas = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, TextureAntiAliasing);
            var normalAtlas = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, TextureAntiAliasing);
            var albedoAtlasTarget = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, TextureAntiAliasing);
            var specularSmoothnessAtlasTarget = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, TextureAntiAliasing);
            var depthAtlasTarget = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, TextureAntiAliasing);
            var normalAtlasTarget = RenderTexture.GetTemporary(TargetTextureSize, TargetTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, TextureAntiAliasing);
            var albedoBuffer = albedoAtlas.colorBuffer;
            var depthBuffer = albedoAtlas.depthBuffer;
            var specularSmoothnessBuffer = specularSmoothnessAtlas.colorBuffer;
            var depthTextureBuffer = depthAtlas.colorBuffer;
            var normalBuffer = normalAtlas.colorBuffer;
            var renderBuffers = new[] { albedoBuffer, specularSmoothnessBuffer, depthTextureBuffer, normalBuffer };

            //Compatibility settings
            var renderTessellation = 1;

            //Prepare texture and material
            albedoAtlasTexture = new Texture2D(TargetTextureSize, TargetTextureSize, TextureFormat.ARGB32, true, false);
            specularSmoothnessAtlasTexture = new Texture2D(TargetTextureSize, TargetTextureSize, TextureFormat.ARGB32, true, true);
            depthAtlasTexture = new Texture2D(TargetTextureSize, TargetTextureSize, TextureFormat.ARGB32, true, true);
            normalAtlasTexture = new Texture2D(TargetTextureSize, TargetTextureSize, TextureFormat.ARGB32, true, true);
            var renderMaterial = new Material(originalMaterial);
            renderMaterial.shader = Shader.Find("Hidden/Stix Games/Grass Fallback Renderer");
            GrassUtility.SetDensityMode(renderMaterial, DensityMode.Value);
            GrassUtility.SetWindParams(renderMaterial, Vector4.zero);
            GrassUtility.SetTargetTessellation(renderMaterial, renderTessellation);
            GrassUtility.SetRandomizedDirectionMode(renderMaterial, true);
            GrassUtility.SetColorHeightTexture(renderMaterial, null);

            //Get values necessary for camera settings
            var disorder = GrassUtility.GetDisorderValue(renderMaterial);
            var softness = GrassUtility.GetMaxSoftnessValue(renderMaterial);
            var grassOffset = disorder * softness;
            var grassHeight = GrassUtility.GetMaxGrassHeight(renderMaterial);

            //Create render camera
            var orthographicSize = (grassHeight + grassOffset * 2) * 0.5f;
            var farClipPlane = grassHeight + grassOffset;
            var nearClipPlane = -grassOffset;

            //Set up billboard info for renderer
            BillboardHeight = grassHeight;
            BillboardWidth = orthographicSize * 2;

            try
            {
                GL.sRGBWrite = true;
                GL.PushMatrix();
                GL.LoadIdentity();
                var matrix = Matrix4x4.Ortho(-orthographicSize, orthographicSize, 0, grassHeight, nearClipPlane, farClipPlane);
                GL.LoadProjectionMatrix(matrix);

                Shader.SetGlobalVector("_WorldSpaceCameraPos", Vector4.zero);

                //Clear depth texture. This is not actually used as depth buffer, so it starting as white will not influence the rendering
                RenderTexture.active = depthAtlas;
                GL.Clear(true, true, new Color(1, 1, 1, 1));

                //Clear normal map
                RenderTexture.active = normalAtlas;
                GL.Clear(true, true, new Color(0.5f, 0.5f, 1.0f, 1));

                //Set render target
                Graphics.SetRenderTarget(renderBuffers, depthBuffer);

                //Calculate uv offset for atlas padding
                float perTileOffset = TexturePadding / ((float) TargetTextureSize / TextureColumns);

                //Sampler for placing individual blades of grass, used in compatibility mode
                //var grassPositionRandomizer = new Sampler2D(new HaltonSampler(2, rng.Range(1, 1000)), new HaltonSampler(3, rng.Range(1, 1000)));
                var grassPositionRandomizer = new Sampler2D(new RandomSampler(Seed), new RandomSampler(Seed + 1));

                //Render the individual entries in the atlas
                int textureIndex = 0;
                for (int i = 0; i < densityDistributions.Count; i++)
                {
                    var distribution = densityDistributions[i];

                    for (var j = 0; j < distribution.Count; j++)
                    {
#if UNITY_EDITOR
                        var title = string.Format("Creating texture atlas for {0}", originalMaterial.name);
                        var info = string.Format("Grass type {0}, texture: {1}", i, j);
                        var grassTypeSize = 1.0f / densityDistributions.Count;
                        var distributionSize = grassTypeSize * (1.0f / distribution.Count);
                        EditorUtility.DisplayProgressBar(title, info, grassTypeSize * i + distributionSize * j);
#endif

                        //Set the tile the texture will be rendered into
                        var x = (textureIndex % TextureColumns) * tileSize + TexturePadding;
                        var y = (textureIndex / TextureColumns) * tileSize + TexturePadding;
                        GL.Viewport(new Rect(x, y, tileSize - 2 * TexturePadding, tileSize - 2 * TexturePadding));

                        //Set target density on the material
                        var densityValue = distribution[j];
                        var density = GetSingleDensity(densityValue, i);
                        GrassUtility.SetValueDensity(renderMaterial, density);
                        renderMaterial.SetPass(0);

                        //Without tessellation, only the first vertex is used
                        var mesh = new Mesh();
                        var vertices = new List<Vector3>();
                        var triangles = new List<int>();
                        for (int k = 0; k < BladesOfGrass; k++)
                        {
                            var pos = grassPositionRandomizer.NextVector2D();
                            var xPos = pos.x * grassHeight - grassHeight * 0.5f;
                            var zPos = -pos.y * grassHeight;
                            var bladeOfGrassPos = new Vector3(xPos, 0, zPos);
                            vertices.Add(bladeOfGrassPos);
                            vertices.Add(bladeOfGrassPos + new Vector3(0, 0, 0.001f));
                            vertices.Add(bladeOfGrassPos + new Vector3(0.001f, 0, 0));
                            triangles.Add(k * 3);
                            triangles.Add(k * 3 + 1);
                            triangles.Add(k * 3 + 2);
                        }
                        mesh.SetVertices(vertices);
                        mesh.SetTriangles(triangles, 0);
                        mesh.UploadMeshData(true);

                        Graphics.DrawMeshNow(mesh, Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(1, 1, 1)));

                        textureIndex++;
                    }
                }
                
                //Dilate the texture atlas, to prevent black borders
                var dilateMat = new Material(Shader.Find("Hidden/TextureDilate"));
                dilateMat.SetFloat("_PixelSize", 1.0f / TargetTextureSize);
                for (int i = 0; i < DilationSteps; i++)
                {
#if UNITY_EDITOR
                    var title = string.Format("Creating texture atlas for {0}", originalMaterial.name);
                    var info = string.Format("Dilating texture, step {0}", i);
                    var dilationStepSize = 1.0f / DilationSteps;
                    EditorUtility.DisplayProgressBar(title, info, dilationStepSize * i);
#endif

                    Graphics.Blit(albedoAtlas, albedoAtlasTarget, dilateMat);
                    Graphics.Blit(albedoAtlasTarget, albedoAtlas);

                    //Uses alpha for other information, so the regular dilate can't be used
                    //Graphics.Blit(specularSmoothnessAtlas, specularSmoothnessAtlasTarget, dilateMat);
                    //Graphics.Blit(depthNormalAtlas, depthNormalAtlasTarget, mat);

                    //var tempAlbedo = albedoAtlas;
                    //albedoAtlas = albedoAtlasTarget;
                    //albedoAtlasTarget = tempAlbedo;

                    //var tempSpec = specularSmoothnessAtlas;
                    //specularSmoothnessAtlas = specularSmoothnessAtlasTarget;
                    //specularSmoothnessAtlasTarget = tempSpec;

                    //var tempDepthNormal = depthNormalAtlas;
                    //depthNormalAtlas = depthNormalAtlasTarget;
                    //depthNormalAtlasTarget = tempDepthNormal;
                }


                //Read rendertextures to texture
                albedoAtlasTexture = SaveToTexture("Albedo", albedoAtlasTexture, albedoAtlas, false, false);
                if (RenderSpecularTexture)
                {
                    specularSmoothnessAtlasTexture = SaveToTexture("Specular", specularSmoothnessAtlasTexture, specularSmoothnessAtlas, true, false);
                }
                else
                {
                    specularSmoothnessAtlasTexture = null;
                }
                depthAtlasTexture = SaveToTexture("Depth", depthAtlasTexture, depthAtlas, true, false);
                normalAtlasTexture = SaveToTexture("Normal", normalAtlasTexture, normalAtlas, true, true);

                //Create Fallback material
                FallbackMaterial = new Material(Shader.Find("Stix Games/GrassFallback"));
                FallbackMaterial.name = string.Format("{0} - Fallback", originalMaterial.name);
                FallbackMaterial.SetTexture("_TextureAtlas", albedoAtlasTexture);
                FallbackMaterial.SetTexture("_SpecularSmooth", specularSmoothnessAtlasTexture);
                FallbackMaterial.SetTexture("_Depth", depthAtlasTexture);
                FallbackMaterial.SetTexture("_Normal", normalAtlasTexture);
                FallbackMaterial.SetFloat("_Subsurface", GrassUtility.GetMaxSubsurface(originalMaterial));
                FallbackMaterial.SetVector("_WindParams", originalMaterial.GetVector("_WindParams"));
                FallbackMaterial.SetFloat("_WindRotation", originalMaterial.GetFloat("_WindRotation"));
                FallbackMaterial.SetFloat("_SoftnessFactor", GrassUtility.GetMinSoftnessValue(originalMaterial) * grassHeight);
                FallbackMaterial.SetInt("_AtlasSize", TextureColumns);
                FallbackMaterial.SetFloat("_AtlasOffset", perTileOffset);
            }
            catch (Exception)
            {
                //In case something goes wrong, delete the texture atlas.
                //Not sure anything could go wrong here, so it's just in case, to prevent memory leaks.
#if UNITY_EDITOR
                Object.DestroyImmediate(albedoAtlasTexture, true);
                Object.DestroyImmediate(specularSmoothnessAtlasTexture, true);
                Object.DestroyImmediate(depthAtlasTexture, true);
                Object.DestroyImmediate(normalAtlasTexture, true);
#else
                Object.Destroy(albedoAtlasTexture);
                Object.Destroy(specularSmoothnessAtlasTexture);
                Object.Destroy(depthAtlasTexture);
                Object.Destroy(normalAtlasTexture);
#endif

                throw;
            }
            finally
            {
#if UNITY_EDITOR
                EditorUtility.ClearProgressBar();
#endif
                RenderTexture.active = null;
                GL.PopMatrix();
                RenderTexture.ReleaseTemporary(albedoAtlas);
                RenderTexture.ReleaseTemporary(specularSmoothnessAtlas);
                RenderTexture.ReleaseTemporary(depthAtlas);
                RenderTexture.ReleaseTemporary(normalAtlas);
                RenderTexture.ReleaseTemporary(albedoAtlasTarget);
                RenderTexture.ReleaseTemporary(specularSmoothnessAtlasTarget);
                RenderTexture.ReleaseTemporary(depthAtlasTarget);
                RenderTexture.ReleaseTemporary(normalAtlasTarget);

#if UNITY_EDITOR
                Object.DestroyImmediate(renderMaterial);
#else
                Object.Destroy(renderMaterial);
#endif
            }
        }

        private Texture2D SaveToTexture(string name, Texture2D target, RenderTexture sourceTexture, bool isLinear, bool isNormal)
        {
#if UNITY_EDITOR
            var saveTitle = string.Format("Creating texture atlas for {0}", originalMaterial.name);
            var saveInfo = string.Format("Saving textures: {0}", name);
            EditorUtility.DisplayProgressBar(saveTitle, saveInfo, 0.33f);
#endif
            RenderTexture.active = sourceTexture;
            target.ReadPixels(new Rect(0, 0, TargetTextureSize, TargetTextureSize), 0, 0, false);
            target.Apply(true);
            target.name = string.Format("{0} - {1} Texture Atlas", originalMaterial.name, name);

#if UNITY_EDITOR
            var materialPath = AssetDatabase.GetAssetPath(originalMaterial);
            var folderPath = Path.Combine(Path.GetDirectoryName(materialPath) ?? "", "Fallback");
            var path = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(materialPath) ?? "Material");
            var savePath = string.Format("{0} - {1} Texture Atlas.png", path, name);
            Directory.CreateDirectory(folderPath);
            var newTarget = GrassTextureUtility.SaveTextureToFile(savePath, target, false, isLinear, isNormal);
            Object.DestroyImmediate(target);
            target = newTarget;
#endif
            return target;
        }
    }
}
