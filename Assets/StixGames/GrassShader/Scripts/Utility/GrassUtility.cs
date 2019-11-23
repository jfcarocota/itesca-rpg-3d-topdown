using System;
using UnityEngine;

namespace StixGames.GrassShader
{
    public static class GrassUtility
    {
        /// <summary>
        /// Calculates the tessellation factor, exactly like it is done in the shader, except for some optimization
        /// </summary>
        public static float CalcTessFactor(Vector3 wpos0, Vector3 wpos1, Vector3 cameraPos,
            float targetDensity, float falloffStart, float falloffScale, float falloffPower)
        {
            //Length of the edge
            var len = Vector3.Distance(wpos0, wpos1);

            //Distance to the center of the edge
            var dist = Vector3.Distance(0.5f * (wpos0 + wpos1), cameraPos);

            //Limit distance and scale
            dist = Mathf.Pow(Mathf.Max(dist - falloffStart, 0.0f) * falloffScale, falloffPower);

            //The target distance between blades of grass
            var targetLen = targetDensity * Mathf.Max(dist, 1.0f);

            return len / targetLen;
        }

        public static float CalcMaxTessFactor(Vector3 wpos0, Vector3 wpos1,
            float targetDensity)
        {
            //Length of the edge
            var len = Vector3.Distance(wpos0, wpos1);

            //The target distance between blades of grass
            var targetLen = targetDensity;

            return len / targetLen;
        }

        public static float CalcMaxTessFactor(Triangle triangle,
            float targetDensity)
        {
            var tess0 = CalcMaxTessFactor(triangle.V0, triangle.V1, targetDensity);
            var tess1 = CalcMaxTessFactor(triangle.V1, triangle.V2, targetDensity);
            var tess2 = CalcMaxTessFactor(triangle.V0, triangle.V2, targetDensity);

            return (tess0 + tess1 + tess2) * 0.33333f;
        }

        public static float CalcTessFactor(Triangle triangle, Vector3 cameraPos,
            float targetDensity, float falloffStart, float falloffScale, float falloffPower)
        {
            var tess0 = CalcTessFactor(triangle.V0, triangle.V1, cameraPos, targetDensity, falloffStart, falloffScale, falloffPower);
            var tess1 = CalcTessFactor(triangle.V1, triangle.V2, cameraPos, targetDensity, falloffStart, falloffScale, falloffPower);
            var tess2 = CalcTessFactor(triangle.V0, triangle.V2, cameraPos, targetDensity, falloffStart, falloffScale, falloffPower);

            return (tess0 + tess1 + tess2) * 0.33333f;
        }

        public static float CalcTessLevel(float tessFactor)
        {
            return Mathf.Max(Mathf.Log(tessFactor, 2), 0);
        }

        private static readonly int[] BillboardCounts = {0, 1, 7, 37, 169, 496, 2977};
        public static int TessLevelToBillboardCount(int tessLevel)
        {
            //Grass tessellation has the following amounts of blades of grass, per triangle: 1, 7, 37, 169, 496, 2977
            //The tessellation on one side of the triangle increases with 2^n-1, if you start counting the amount of blades per row,
            //starting from the opposing side, they increase in odd numbers (1, 3, 7, ...) until 2^n-1 is reached.
            //This number is then repeated until the total amount of rows is once again 2^n-1. Sum all rows and you get the total amount of blades of grass.
            //Here n is the tessellation factor used in the shader, tessfactor is already 2^n
            //Yes, this was fairly unscientific, but I haven't found any specific resources how tessellation is done and this is not too bad.
            //If anybody has a better way of doing this, please send man an email...

            if (tessLevel < 0 || tessLevel >= BillboardCounts.Length)
            {
                throw new ArgumentOutOfRangeException("tessLevel");
            }

            return BillboardCounts[tessLevel];
        }

        public static int GetTessLevelFromBillboardCount(int billboardCount)
        {
            for (int i = 0; i < BillboardCounts.Length; i++)
            {
                if (BillboardCounts[i] >= billboardCount)
                {
                    return i;
                }
            }

            throw new ArgumentOutOfRangeException("billboardCount");
        }

        private static readonly string[] GrassTypeString =
        {
            "SIMPLE_GRASS", "ONE_GRASS_TYPE", "TWO_GRASS_TYPES", "THREE_GRASS_TYPES", "FOUR_GRASS_TYPES"
        };

        public static int GetGrassTypeCount(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            if (mat.IsKeywordEnabled(GrassTypeString[0]))
            {
                return 0;
            }

            if (mat.IsKeywordEnabled(GrassTypeString[1]))
            {
                return 1;
            }

            if (mat.IsKeywordEnabled(GrassTypeString[2]))
            {
                return 2;
            }

            if (mat.IsKeywordEnabled(GrassTypeString[3]))
            {
                return 3;
            }

            if (mat.IsKeywordEnabled(GrassTypeString[4]))
            {
                return 4;
            }

            throw new ArgumentException("No valid grass type");
        }

        public static readonly string[] DensityModes = { "UNIFORM_DENSITY", "VERTEX_DENSITY", "" };

        public static DensityMode GetDensityMode(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            if (mat.IsKeywordEnabled(DensityModes[0]))
            {
                return DensityMode.Value;
            }

            if (mat.IsKeywordEnabled(DensityModes[1]))
            {
                return DensityMode.Vertex;
            }

            return DensityMode.Texture;
        }

        public static void SetDensityMode(Material mat, DensityMode target)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            var density = GetDensityMode(mat);

            switch (density)
            {
                case DensityMode.Value:
                    mat.DisableKeyword(DensityModes[0]);
                    break;
                case DensityMode.Vertex:
                    mat.DisableKeyword(DensityModes[1]);
                    break;
            }

            mat.EnableKeyword(DensityModes[(int)target]);
        }

        public static Vector4 GetValueDensity(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            return mat.GetVector("_DensityValues");
        }

        public static void SetValueDensity(Material mat, Vector4 value)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            mat.SetVector("_DensityValues", value);
        }

        public static void SetValueDensity(MaterialPropertyBlock block, Vector4 value)
        {
            block.SetVector("_DensityValues", value);
        }

        public static Texture2D GetDensityTexture(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            return mat.GetTexture("_DensityTexture") as Texture2D;
        }

        public static float GetDisorderValue(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            return mat.GetFloat("_Disorder");
        }

        public static float GetMaxSoftnessValue(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            var count = GetGrassTypeCount(mat);

            var soft0 = mat.GetFloat("_Softness00");
            var soft1 = mat.GetFloat("_Softness01") * (count >= 2 ? 1 : 0);
            var soft2 = mat.GetFloat("_Softness02") * (count >= 3 ? 1 : 0);
            var soft3 = mat.GetFloat("_Softness03") * (count >= 4 ? 1 : 0);
            return Mathf.Max(soft0, soft1, soft2, soft3);
        }

        public static float GetMinSoftnessValue(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            var count = GetGrassTypeCount(mat);

            var soft0 = mat.GetFloat("_Softness00");
            var soft1 = mat.GetFloat("_Softness01") * (count >= 2 ? 1 : Mathf.Infinity);
            var soft2 = mat.GetFloat("_Softness02") * (count >= 3 ? 1 : Mathf.Infinity);
            var soft3 = mat.GetFloat("_Softness03") * (count >= 4 ? 1 : Mathf.Infinity);
            return Mathf.Min(soft0, soft1, soft2, soft3);
        }

        /// <summary>
        /// Returns the maximum height for one grass type
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="grassType"></param>
        /// <returns></returns>
        public static float GetMaxGrassHeight(Material mat, int grassType)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            if (grassType < 0 || grassType >= 4)
            {
                throw new ArgumentOutOfRangeException("grassType");
            }

            return mat.GetFloat(string.Format("_MaxHeight0{0}", grassType));
        }

        /// <summary>
        /// Returns the maximum grass height of all types combined
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public static float GetMaxGrassHeight(Material mat)
        {
            var count = GetGrassTypeCount(mat);

            var maxGrassHeight0 = GetMaxGrassHeight(mat, 0);
            var maxGrassHeight1 = GetMaxGrassHeight(mat, 1) * (count >= 2 ? 1 : 0);
            var maxGrassHeight2 = GetMaxGrassHeight(mat, 2) * (count >= 3 ? 1 : 0);
            var maxGrassHeight3 = GetMaxGrassHeight(mat, 3) * (count >= 4 ? 1 : 0);
            return Mathf.Max(maxGrassHeight0, maxGrassHeight1, maxGrassHeight2, maxGrassHeight3);
        }

        public static void SetWindParams(Material mat, Vector4 value)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            mat.SetVector("_WindParams", value);
        }

        public static void SetEdgeLength(Material mat, float value)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            mat.SetFloat("_TargetDensity", value);
        }

        public static void SetTargetTessellation(Material mat, float value)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }
            mat.SetFloat("_TargetTessellation", value);
        }

        public static bool IsForwardOnlyGrassMaterial(Material material)
        {
            if (material == null)
            {
                throw new ArgumentNullException("material");
            }

            return material.shader.name == "Stix Games/Grass Forward Only";
        }

        public static bool IsHDRPGrassMaterial(Material material)
        {
            if (material == null)
            {
                throw new ArgumentNullException("material");
            }

            return material.shader.name == "Stix Games/Grass HDRP";
        }
        
        public static bool IsURPrassMaterial(Material material)
        {
            if (material == null)
            {
                throw new ArgumentNullException("material");
            }

            return material.shader.name == "Stix Games/Grass URP";
        }

        public static bool IsGrassMaterial(Material material)
        {
            if (material == null)
            {
                throw new ArgumentNullException("material");
            }

            return material.shader.name == "Stix Games/Grass" 
                || material.shader.name == "Stix Games/Grass Forward Only" 
                || material.shader.name == "Stix Games/Grass HDRP"
                || material.shader.name == "Stix Games/Grass URP"
                || material.shader.name == "Hidden/Stix Games/Grass Fallback Renderer";
        }

        public static void SetRandomizedDirectionMode(Material material, bool enable)
        {
            if (enable)
            {
                material.EnableKeyword("GRASS_RANDOM_DIR");
                material.SetInt("_CullMode", 2);
            }
            else
            {
                material.DisableKeyword("GRASS_RANDOM_DIR");
                material.SetInt("_CullMode", 0);
            }
        }

        public static float GetGrassFadeStart(Material mat)
        {
            if(!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            return mat.GetFloat("_GrassFadeStart");
        }

        public static float GetGrassFadeEnd(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            return mat.GetFloat("_GrassFadeEnd");
        }

        public static void SetFloorColor(Material mat, Color value)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            mat.SetColor("_GrassFloorColor", value);
        }

        public static void SetFloorColorTexture(Material mat, Texture value, Vector2 offset, Vector2 scale)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            mat.SetTexture("_GrassFloorColorTexture", value);
            mat.SetTextureOffset("_GrassFloorColorTexture", offset);
            mat.SetTextureScale("_GrassFloorColorTexture", scale);
        }

        public static void SetColorHeightTexture(Material mat, Texture value)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            mat.SetTexture("_ColorMap", value);
        }

        private static float GetSubsurface(Material mat, int grassType)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            if (grassType < 0 || grassType >= 4)
            {
                throw new ArgumentOutOfRangeException("grassType");
            }

            return mat.GetFloat(string.Format("_Subsurface0{0}", grassType));
        }

        public static float GetMaxSubsurface(Material mat)
        {
            if (!IsGrassMaterial(mat))
            {
                throw new ArgumentException("mat does not use the grass shader");
            }

            var count = GetGrassTypeCount(mat);

            var subsurface0 = GetSubsurface(mat, 0);
            var subsurface1 = GetSubsurface(mat, 1) * (count >= 2 ? 1 : 0);
            var subsurface2 = GetSubsurface(mat, 2) * (count >= 3 ? 1 : 0);
            var subsurface3 = GetSubsurface(mat, 3) * (count >= 4 ? 1 : 0);
            return Mathf.Max(subsurface0, subsurface1, subsurface2, subsurface3);
        }
    }

    public enum DensityMode
    {
        Value,
        Vertex,
        Texture
    }
}