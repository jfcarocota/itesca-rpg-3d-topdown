using System.IO;
using UnityEngine;
using UnityEditor;

namespace StixGames.GrassShader
{
#if UNITY_EDITOR
    public static class GrassTextureUtility
	{
        public static Texture2D SaveTextureToFile(string path, Texture2D texture, bool isReadable = false, bool isLinear = false, bool isNormal = false)
		{
			File.WriteAllBytes(path, texture.EncodeToPNG());

			//Import and load the new texture
			AssetDatabase.ImportAsset(path);
			var importer = (TextureImporter) AssetImporter.GetAtPath(path);
		    importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.isReadable = isReadable;
			importer.maxTextureSize = Mathf.Max(texture.width, texture.height);
			importer.sRGBTexture = !isLinear;
			importer.alphaSource = TextureImporterAlphaSource.FromInput;
			importer.filterMode = FilterMode.Trilinear;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
			{
				name = "Standalone",
				format = TextureImporterFormat.RGBA32,
				overridden = true,
			});
			AssetDatabase.ImportAsset(path);

			return AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
		}
	}
#endif
}
