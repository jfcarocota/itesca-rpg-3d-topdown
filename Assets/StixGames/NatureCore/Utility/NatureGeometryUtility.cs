using UnityEngine;

namespace StixGames.GrassShader
{
    public static class NatureGeometryUtility
    {
        /// <summary>
        /// Calculates the frustum planes, offsets them by <see cref="offset"/>, and sets the far clip plane to <see cref="farClip"/>
        /// </summary>
        /// <param name="current"></param>
        /// <param name="farClip"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Plane[] CalculateFrustumPlanes(Camera current, float farClip, float offset)
        {
            var prevFarClipPlane = current.farClipPlane;
            current.farClipPlane = farClip;
            var planes = GeometryUtility.CalculateFrustumPlanes(current);
            OffsetFrustumPlanes(planes, offset);
            current.farClipPlane = prevFarClipPlane;
            return planes;
        }

        /// <summary>
        /// Offsets the frustum planes, to make the area inside smaller or larger by a constant distance in all directions
        /// </summary>
        /// <param name="planes"></param>
        /// <param name="offset"></param>
        public static void OffsetFrustumPlanes(Plane[] planes, float offset)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                planes[i].distance += offset;
            }
        }
    }
}