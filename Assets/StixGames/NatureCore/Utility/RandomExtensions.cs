using System;
using UnityEngine;

namespace StixGames.NatureCore.Utility
{
    public static class RandomExtensions
    {
        public static float NextFloat(this System.Random random)
        {
            return (float) random.NextDouble();
        }

        /// <summary>
        /// Random in range of start (inclusive) and end (exclusive)
        /// </summary>
        /// <param name="random"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static int Range(this System.Random random, int start, int end)
        {
            if (start > end)
            {
                throw new ArgumentException("Start must be smaller or equal than end");
            }

            return (int)(random.NextDouble() * (end - start) + start);
        }

        /// <summary>
        /// Random in range of start (inclusive) and end (exclusive)
        /// </summary>
        /// <param name="random"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static float Range(this System.Random random, float start, float end)
        {
            if (start > end)
            {
                throw new ArgumentException("Start must be smaller or equal than end");
            }

            return random.NextFloat() * (end - start) + start;
        }

        public static Vector3 RandomUnitCircle(this System.Random random)
        {
            var rot = random.Range(0, 2 * Mathf.PI);
            return new Vector3(Mathf.Sin(rot), 0, Mathf.Cos(rot));
        }
    }
}