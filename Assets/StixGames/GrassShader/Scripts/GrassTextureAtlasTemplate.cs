using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StixGames.NatureCore.Utility;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace StixGames.GrassShader
{
    [Serializable]
    [CreateAssetMenu(menuName = "Stix Games/Grass Texture Atlas Template", order = 1000)]
    public class GrassTextureAtlasTemplate : ScriptableObject
    {
        [Header("Texture Atlas Settings")] [Tooltip("Target texture size, should be power of two.")]
        public int TargetSize = 4096;

        [Tooltip("The number of rows. If too low, it will automatically be set to an amount where all textures fit.")]
        public int Rows = 1;

        [Tooltip("The area around the textures, to prevent texture bleeding.")]
        public int Padding = 10;

        [Tooltip(
            "The number of dilation steps, creating a transparent border around the textures, to prevent black borders around the textures.")]
        public int DilationSteps = 50;

        [Header("Random Variations")]
        [Tooltip(
            "When Overdraw is higher than 0, each texture will be randomly composed from multiple blades of grass.")]
        public int Overdraw = 0;

        [Tooltip(
            "When Overdraw is higher than 0, this seed is used to create the random variations. 0 is random each time you update.")]
        public int Seed = 0;

        [Tooltip("When Overdraw is higher than 0, Variations defines how many random combinations will be created.")]
        public int Variations = 20;

        [Tooltip("When Overdraw is higher than 0, Size Variation randomly changes the height of blades of grass.")]
        [Range(0.0f, 1.0f)]
        public float SizeVariation = 0.8f;

        [Tooltip("When Overdraw is higher than 0, Position Variation randomly shifts the position of blades of grass.")]
        [Range(1.0f, 2.0f)]
        public float PositionVariation = 1.0f;

        [Header("Textures")] [Tooltip("The textures that will be filled into the texture atlas.")]
        public List<Texture2D> Textures;

        [HideInInspector] public Texture2D TextureAtlas;

        //[Tooltip("Normalize the height, so all texture use the full space available. If disabled, smaller textures will be treated as smaller objects.")]
        //public bool NormalizeHeight = true;

        public GrassTextureAtlasTemplate()
        {
            Textures = new List<Texture2D>();
        }

        /// <summary>
        /// Processes the textures with the current settings. If <see cref="path"/> is null, it will not create the actual texture, taking far less performance.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public GrassTextureAtlasResult Process(string path = null)
        {
            if (Textures.Count == 0 || Textures.All(x => x == null))
            {
                throw new InvalidOperationException("Textures must have elements that are not null");
            }

            if (Rows <= 0)
            {
                throw new InvalidOperationException("Rows must be greater than 0");
            }

            if (Overdraw < 0)
            {
                throw new InvalidOperationException("Overdraw must be greater or equal to 0");
            }

            bool createTexture = !string.IsNullOrEmpty(path);
            var textureName = path != null ? Path.GetFileNameWithoutExtension(path) : "Unnamed";

            var filteredTextures = Textures.Where(x => x != null).ToList();

            RenderTexture atlasTexture = null;
            if (createTexture)
            {
                atlasTexture = RenderTexture.GetTemporary(TargetSize, TargetSize, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
            }

            var textureSlots = (Overdraw == 0 ? filteredTextures.Count : Variations);

            //Figure out the minimum required amount of rows
            var maxAspectRatio = filteredTextures.Max(x => (float) x.width / x.height) * PositionVariation;
            bool texturesFit = false;
            do
            {
                var height = TargetSize / Rows;
                var width = (int) (height * maxAspectRatio);

                if (width * Mathf.CeilToInt((float) textureSlots / Rows) > TargetSize)
                {
                    Rows++;
                }
                else
                {
                    texturesFit = true;
                }
            } while (!texturesFit);

            //Calculate area per texture
            var tilesPerRow = Mathf.CeilToInt((float) textureSlots / Rows);
            var tileWidth = 1.0f / tilesPerRow;
            var tileHeight = 1.0f / Rows;

            //Copy texture to atlas
            if (createTexture)
            {
                GL.PushMatrix();
                Graphics.SetRenderTarget(atlasTexture);
                GL.Clear(true, true, new Color(0, 0, 0, 0));
                GL.LoadPixelMatrix(0, TargetSize, TargetSize, 0);
            }

            //Set the random seed for overdraw
            if (Overdraw != 0 || Seed != 0)
            {
                Random.InitState(Seed);
            }

            var duplicateTextures = 0;
            var wastedSpace = 0.0f;
            for (int overdraw = 0; overdraw <= Overdraw; overdraw++)
            {
                for (int i = 0; i < tilesPerRow * Rows; i++)
                {
#if UNITY_EDITOR
                    if (createTexture)
                    {
                        bool cancelOperation = EditorUtility.DisplayCancelableProgressBar("Creating texture atlas",
                            string.Format("Overdraw: {0}/{1} Texture: {2}/{3}", overdraw + 1, Overdraw + 1, i + 1,
                                tilesPerRow * Rows), (float) i / (tilesPerRow * Rows));

                        if (cancelOperation)
                        {
                            EditorUtility.ClearProgressBar();
                            return new GrassTextureAtlasResult
                            {
                                TextureAtlas = TextureAtlas,

                                DuplicateTextures = duplicateTextures,
                                WastedSpace = wastedSpace,

                                AtlasWidth = tilesPerRow,
                                AtlasHeight = Rows,
                            };
                        }
                    }
#endif

                    if (i >= filteredTextures.Count && Overdraw == 0)
                    {
                        duplicateTextures++;
                    }

                    var texture =
                        filteredTextures[
                            Overdraw == 0 ? i % filteredTextures.Count : Random.Range(0, filteredTextures.Count)];

                    var tileX = i % tilesPerRow;
                    var tileY = i / tilesPerRow;

                    var textureAspect = (float) texture.width / texture.height;
                    var realWidth = tileHeight * textureAspect;
                    var xOffset = Mathf.Max((tileWidth - realWidth) / 2.0f, 0);

                    if (Overdraw == 0)
                    {
                        wastedSpace += 2 * xOffset * tileHeight;
                    }

                    if (createTexture)
                    {
                        //Mirror the texture
                        if (Overdraw != 0 && Random.value < 0.5f)
                        {
                            GL.MultMatrix(Matrix4x4.TRS(new Vector3(TargetSize, 0, 0), Quaternion.Euler(0, 180, 0),
                                Vector3.one));
                        }

                        var randomOffset = Random.Range(-0.5f, 0.5f);
                        var x = tileX * tileWidth + xOffset + xOffset * PositionVariation * randomOffset;
                        var y = tileY * tileHeight;

                        var start = new Vector2(x, y) * TargetSize + new Vector2(Padding * textureAspect, Padding);
                        var size = new Vector2(realWidth, tileHeight) * TargetSize -
                                   new Vector2(2 * Padding * textureAspect, 2 * Padding);

                        if (Overdraw != 0)
                        {
                            var randomScale = Random.Range(SizeVariation, 1);
                            start.y += (1 - randomScale) * tileHeight * TargetSize;
                            size.y *= randomScale;
                        }

                        GL.sRGBWrite = true;
                        Graphics.DrawTexture(new Rect(start, size), texture);
                    }
                }
            }

            if (createTexture)
            {
                //Dilate texture to prevent black borders
                atlasTexture = RenderTextureUtilities.DilateTexture(textureName, atlasTexture, DilationSteps,
                    RenderTextureReadWrite.sRGB);

                //Reset render settings
                GL.PopMatrix();
            }

            //Convert rendertexture to texture2d
            Texture2D target = null;

            if (createTexture)
            {
                RenderTexture.active = atlasTexture;
                target = new Texture2D(TargetSize, TargetSize, TextureFormat.ARGB32, true, false);
                target.ReadPixels(new Rect(0, 0, TargetSize, TargetSize), 0, 0, true);
                target.Apply(true);
                target.name = textureName;
                RenderTexture.ReleaseTemporary(atlasTexture);

#if UNITY_EDITOR
                EditorUtility.DisplayProgressBar("Creating texture atlas", "Saving texture", 1);

                File.WriteAllBytes(path, target.EncodeToPNG());
                DestroyImmediate(target);
                AssetDatabase.ImportAsset(path);
                target = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter) AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = Mathf.NextPowerOfTwo(TargetSize);
                AssetDatabase.ImportAsset(path);
                target = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                EditorUtility.ClearProgressBar();
#endif
            }

            //Return texture and additional info
            return new GrassTextureAtlasResult
            {
                TextureAtlas = target,

                DuplicateTextures = duplicateTextures,
                WastedSpace = wastedSpace,

                AtlasWidth = tilesPerRow,
                AtlasHeight = Rows,
            };
        }
    }

    [Serializable]
    public class GrassTextureAtlasResult
    {
        public Texture2D TextureAtlas;

        public int DuplicateTextures;

        public float WastedSpace;

        public int AtlasWidth;
        public int AtlasHeight;
    }
}