using System;

namespace StixGames.GrassShader
{
    [Serializable]
    public struct TextureIndexRange
    {
        /// <summary>
        /// Inclusive minimum
        /// </summary>
        public int Min;

        /// <summary>
        /// Exclusive maximum
        /// </summary>
        public int Max;

        public int Size
        {
            get { return Max - Min; }
        }

        /// <summary>
        /// The probability in percent that the texture will be rendered
        /// </summary>
        public float RenderChance;

        public TextureIndexRange(int min, int max)
        {
            Min = min;
            Max = max;
            RenderChance = 1;
        }

        public TextureIndexRange(int min, int max, float renderChance)
        {
            Min = min;
            Max = max;
            RenderChance = renderChance;
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}), RenderChance: {2}", Min, Max, RenderChance);
        }
    }
}