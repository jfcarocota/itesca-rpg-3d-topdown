using StixGames.NatureCore.Utility;
using UnityEngine;

namespace StixGames.GrassShader
{
    public class Triangle
    {
        public int Index;
        public readonly Vector3 V0, V1, V2;
        public readonly Color C0, C1, C2;
        public readonly Vector2 UV0, UV1, UV2;

        public Triangle(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
        }

        public Triangle(Vector3 v0, Vector3 v1, Vector3 v2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            UV0 = uv0;
            UV1 = uv1;
            UV2 = uv2;
        }

        public Triangle(Vector3 v0, Vector3 v1, Vector3 v2, Color c0, Color c1, Color c2, Vector2 uv0, Vector2 uv1,
            Vector2 uv2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            C0 = c0;
            C1 = c1;
            C2 = c2;
            UV0 = uv0;
            UV1 = uv1;
            UV2 = uv2;
        }

        public Vector3 Center
        {
            get { return (V0 + V1 + V2) / 3.0f; }
        }

        public Vector3 Normal
        {
            get
            {
                var dir = Vector3.Cross(V1 - V0, V2 - V0);
                return Vector3.Normalize(dir);
            }
        }

        public Bounds Bounds
        {
            get { return GeometryUtility.CalculateBounds(GetVertices(), Matrix4x4.identity); }
        }

        public float Area
        {
            get { return Vector3.Cross(V1 - V0, V2 - V0).magnitude * 0.5f; }
        }

        public Vector3[] GetVertices()
        {
            return new[] {V0, V1, V2};
        }

        public float GetMaxEdgeLength()
        {
            return Mathf.Max(Vector3.Distance(V0, V1), Vector3.Distance(V0, V2), Vector3.Distance(V1, V2));
        }

        /// <summary>
        /// Returns a random point on the triangle in barycentric coordinates
        /// </summary>
        /// <param name="rng"></param>
        /// <returns></returns>
        public Vector3 GetRandomCoordinates(Sampler2D rng)
        {
            var r = rng.NextVector2D();

            if (r.x + r.y > 1)
            {
                r.x = 1 - r.x;
                r.y = 1 - r.y;
            }

            var a = 1 - r.x - r.y;
            var b = r.x;
            var c = r.y;

            return new Vector3(a, b, c);
        }

        /// <summary>
        /// Returns the point at the specified barycentric coordinates
        /// </summary>
        /// <param name="coords"></param>
        /// <returns></returns>
        public Vector3 GetPosition(Vector3 coords)
        {
            return V0 * coords.x + V1 * coords.y + V2 * coords.z;
        }

        /// <summary>
        /// Returns the uv coordinates at the specified barycentric coordinates
        /// </summary>
        /// <param name="coords"></param>
        /// <returns></returns>
        public Vector2 GetUVCoordinates(Vector3 coords)
        {
            return UV0 * coords.x + UV1 * coords.y + UV2 * coords.z;
        }

        /// <summary>
        /// Returns the vertex color at the specified barycentric coordinates
        /// </summary>
        /// <param name="coords"></param>
        /// <returns></returns>
        public Color GetVertexColor(Vector3 coords)
        {
            return C0 * coords.x + C1 * coords.y + C2 * coords.z;
        }

        protected bool Equals(Triangle other)
        {
            return V0.Equals(other.V0) && V1.Equals(other.V1) && V2.Equals(other.V2);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Triangle) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = V0.GetHashCode();
                hashCode = (hashCode * 397) ^ V1.GetHashCode();
                hashCode = (hashCode * 397) ^ V2.GetHashCode();
                return hashCode;
            }
        }
    }
}