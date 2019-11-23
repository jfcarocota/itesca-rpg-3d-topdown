using System;
using System.Collections.Generic;
using StixGames.NatureCore.Utility;
using UnityEngine;

namespace StixGames.GrassShader
{
    [Serializable]
    public class TextureLookupTable
    {
        public int LookupTableEntries;
        public int Rows;
        public int TextureAtlasWidth;

        [SerializeField] private TextureIndexRange[] table;

        public TextureLookupTable(int lookupTableEntries, int rows, int textureAtlasWidth)
        {
            LookupTableEntries = lookupTableEntries;
            Rows = rows;
            TextureAtlasWidth = textureAtlasWidth;

            table = new TextureIndexRange[LookupTableEntries * Rows];
        }

        public void SetRow(int row, IList<TextureIndexRange> values)
        {
            if (values.Count != LookupTableEntries)
            {
                throw new ArgumentException("row must have the same length as LookupTableEntries");
            }

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                table[i + row * LookupTableEntries] = value;
            }
        }

        public TextureIndexRange GetTextureRange(float density, int row)
        {
            return table[(int)(density * (LookupTableEntries-1) + row * LookupTableEntries)];
        }

        public int GetRandomTexture(float density, int row, ISampler rng)
        {
            var range = GetTextureRange(density, row);

            if (range.Min == range.Max)
            {
                return -1;
            }

            if (rng.NextFloat() > range.RenderChance)
            {
                return -1;
            }

            return rng.Range(range.Min, range.Max);
        }
    }
}