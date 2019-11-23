using UnityEngine;

namespace StixGames.NatureCore.Utility
{
    public class Sampler2D
    {
        public readonly ISampler X, Y;

        public Sampler2D(ISampler x, ISampler y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Generates a vector with x and y between 0 and 1
        /// </summary>
        /// <returns></returns>
        public Vector2 NextVector2D()
        {
            return new Vector2(X.NextFloat(), Y.NextFloat());
        }

        public void Skip(int amount = 1)
        {
            X.Skip(amount);
            Y.Skip(amount);
        }

        public void Reset()
        {
            X.Reset();
            Y.Reset();
        }
    }

    public class HaltonSampler : ISampler
    {
        public readonly int Base;
        public readonly int StartIndex;

        private int index;

        public HaltonSampler(int @base, int startIndex)
        {
            Base = @base;
            StartIndex = startIndex;
            index = StartIndex;
        }

        public float NextFloat()
        {
            int i = index;
            double f = 1;
            double r = 0;

            while (i > 0)
            {
                f = f / Base;
                r = r + f * (i % Base);
                i = i / Base;
            }

            index++;

            return (float) r;
        }

        public void Skip(int amount = 1)
        {
            index += amount;
        }

        public void Reset()
        {
            index = StartIndex;
        }
    }

    public class RandomSampler : ISampler
    {
        public readonly int Seed;

        private System.Random rng;

        public RandomSampler(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        public float NextFloat()
        {
            return rng.NextFloat();
        }

        public void Skip(int amount = 1)
        {
            for (int i = 0; i < amount; i++)
            {
                rng.NextFloat();
            }
        }

        public void Reset()
        {
            rng = new System.Random(Seed);
        }
    }

    public interface ISampler
    {
        /// <summary>
        /// Generate a float between 0 and 1
        /// </summary>
        /// <returns></returns>
        float NextFloat();

        /// <summary>
        /// Skip one value
        /// </summary>
        /// <param name="amount"></param>
        void Skip(int amount = 1);

        /// <summary>
        /// Reset to the start seed
        /// </summary>
        void Reset();
    }

    public static class SamplerExtensions
    {
        public static float Range(this ISampler sampler, float start, float end)
        {
            return sampler.NextFloat() * (end - start) + start;
        }

        public static int Range(this ISampler sampler, int start, int end)
        {
            return (int)(sampler.NextFloat() * (end - start) + start);
        }
    }
}
