using System;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StixGames.NatureCore.Utility
{
    public static class RenderTextureUtilities
    {
        /// <summary>
        /// Dilates a texture to prevent black borders. A different texture might be returned.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="texture">A temporary rendertexture</param>
        /// <param name="steps"></param>
        /// <param name="colorMode"></param>
        /// <returns></returns>
        public static RenderTexture DilateTexture(string name, RenderTexture texture, int steps, RenderTextureReadWrite colorMode)
        {
            if (texture.width != texture.height)
            {
                throw new ArgumentException("Texture has to be square.");
            }
            
            var dilateMat = new Material(Shader.Find("Hidden/TextureDilate"));
            var backBuffer = RenderTexture.GetTemporary(texture.width, texture.height, texture.depth, texture.format,
                colorMode);

            dilateMat.SetFloat("_PixelSize", 1.0f / texture.width);
            for (int i = 0; i < steps; i++)
            {
#if UNITY_EDITOR
                var title = string.Format("Texture dilation: {0}", name);
                var info = string.Format("Dilating texture, step {0}", i);
                var dilationStepSize = 1.0f / steps;
                EditorUtility.DisplayProgressBar(title, info, dilationStepSize * i);
#endif

                Graphics.Blit(texture, backBuffer, dilateMat);

                var tempTexture = texture;
                texture = backBuffer;
                backBuffer = tempTexture;
            }

#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
#endif

            try
            {
                RenderTexture.ReleaseTemporary(backBuffer);
            }
            catch (Exception)
            {
#if UNITY_EDITOR
                Object.DestroyImmediate(backBuffer);
#else
                Object.Destroy(backBuffer);
#endif
                throw;
            }

#if UNITY_EDITOR
            Object.DestroyImmediate(dilateMat);
#else
            Object.Destroy(dilateMat);
#endif

            return texture;
        }
    }
}
