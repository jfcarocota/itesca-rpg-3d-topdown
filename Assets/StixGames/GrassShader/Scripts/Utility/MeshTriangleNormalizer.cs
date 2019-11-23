using StixGames.NatureCore;
using UnityEngine;

namespace StixGames.GrassShader
{
    public class MeshTriangleNormalizer
    {
        /// <summary>
        /// You can use this setting to increase the polygon density of the mesh further than necessary for normalization.
        /// This setting is still overridden by the Min Edge Size setting.
        /// </summary>
        public float MinEdgeMultiplier = 1.0f;

        /// <summary>
        /// If an edge is larger than the length of the smallest edge times this value, it will be split.
        /// </summary>
        public float EdgeSplitThreshold = 1.5f;

        /// <summary>
        /// The max ratio at which edges will be considered equal
        /// </summary>
        public float EdgeRatioThreshold = 1.1f;

        /// <summary>
        /// The smallest edge size, edges below this value will no longer be split
        /// </summary>
        public float MinEdgeSize = 0.1f;

        public int MaxDepth = 9;

        private readonly Vector3[] vertices;
        private readonly Vector3[] normals;
        private readonly Vector2[] uvs;
        private readonly Color[] colors;
        private readonly int[] triangles;

        private readonly Mesh mesh;

        private float minEdge;

        private MeshBuilder meshBuilder;

        public MeshTriangleNormalizer(Mesh mesh)
        {
            vertices = mesh.vertices;
            normals = mesh.normals;
            uvs = mesh.uv;
            colors = mesh.colors;
            triangles = mesh.triangles;
            this.mesh = mesh;
        }

        public Mesh[] NormalizeMeshes()
        {
            meshBuilder = new MeshBuilder();
            meshBuilder.AddVertices(vertices);
            meshBuilder.AddNormals(normals);
            meshBuilder.AddUvs(uvs);
            meshBuilder.AddColors(colors);

            minEdge = GetSmallestTriangleEdge();

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (minEdge == 0.0f)
            {
                Debug.LogError("Your mesh contains edges that have a length of 0. Mesh normalization can't be used in this case.", mesh);
                return new[] { mesh };
            }

            //Modifies the min edge to continue subdividing the mesh further than necessary
            minEdge /= MinEdgeMultiplier;

            //Make sure we don't create edges smaller than MinEdgeSize (more or less, anyways)
            minEdge = Mathf.Max(minEdge, MinEdgeSize);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var i0 = triangles[i];
                var i1 = triangles[i + 1];
                var i2 = triangles[i + 2];

                RecursiveSplitTriangle(i0, i1, i2);
            }

            return meshBuilder.CreateMeshes();
        }

        private void RecursiveSplitTriangle(int i0, int i1, int i2, int depth = 0)
        {
            var v0 = meshBuilder.GetVertex(i0);
            var v1 = meshBuilder.GetVertex(i1);
            var v2 = meshBuilder.GetVertex(i2);
            var e0 = Vector3.Distance(v0, v1);
            var e1 = Vector3.Distance(v1, v2);
            var e2 = Vector3.Distance(v0, v2);
            var maxEdge = Mathf.Max(e0, e1, e2);

            if (maxEdge > minEdge * EdgeSplitThreshold && depth < MaxDepth)
            {
                if (AreEdgesEqual(e0, e1, e2))
                {
                    //All edges have approximately the same size, so split all equally
                    //Add one vertex per side
                    var i3 = AddAverageVertex(i0, i1);
                    var i4 = AddAverageVertex(i1, i2);
                    var i5 = AddAverageVertex(i2, i0);

                    RecursiveSplitTriangle(i0, i3, i5, depth + 1);
                    RecursiveSplitTriangle(i3, i1, i4, depth + 1);
                    RecursiveSplitTriangle(i5, i4, i2, depth + 1);
                    RecursiveSplitTriangle(i3, i4, i5, depth + 1);
                }
                else if (AreEdgesEqual(e0, e1) && e0 > e2 && e1 > e2)
                {
                    //Two edges have approximately the same size, split those edges
                    var i3 = AddAverageVertex(i0, i1);
                    var i4 = AddAverageVertex(i1, i2);

                    RecursiveSplitTriangle(i3, i1, i4, depth + 1);
                    RecursiveSplitTriangle(i0, i3, i2, depth + 1);
                    RecursiveSplitTriangle(i3, i4, i2, depth + 1);
                }
                else if (AreEdgesEqual(e1, e2) && e1 > e0 && e2 > e0)
                {
                    var i3 = AddAverageVertex(i0, i2);
                    var i4 = AddAverageVertex(i1, i2);

                    RecursiveSplitTriangle(i3, i4, i2, depth + 1);
                    RecursiveSplitTriangle(i0, i1, i4, depth + 1);
                    RecursiveSplitTriangle(i3, i0, i4, depth + 1);
                }
                else if (AreEdgesEqual(e0, e2) && e0 > e1 && e2 > e1)
                {
                    var i3 = AddAverageVertex(i0, i1);
                    var i4 = AddAverageVertex(i0, i2);

                    RecursiveSplitTriangle(i0, i3, i4, depth + 1);
                    RecursiveSplitTriangle(i3, i1, i2, depth + 1);
                    RecursiveSplitTriangle(i4, i3, i2, depth + 1);
                }
                else
                {
                    //Add one vertex to the smallest side
                    if (e0 >= e1 && e0 >= e2)
                    {
                        var i3 = AddAverageVertex(i0, i1);

                        RecursiveSplitTriangle(i0, i3, i2, depth + 1);
                        RecursiveSplitTriangle(i3, i1, i2, depth + 1);
                    }
                    else if (e1 >= e0 && e1 >= e2)
                    {
                        var i3 = AddAverageVertex(i1, i2);

                        RecursiveSplitTriangle(i0, i1, i3, depth + 1);
                        RecursiveSplitTriangle(i0, i3, i2, depth + 1);
                    }
                    else if (e2 >= e0 && e2 >= e1)
                    {
                        var i3 = AddAverageVertex(i0, i2);

                        RecursiveSplitTriangle(i0, i1, i3, depth + 1);
                        RecursiveSplitTriangle(i3, i1, i2, depth + 1);
                    }
                }
            }
            else
            {
                meshBuilder.AddTriangle(i0, i1, i2);
            }
        }

        private bool AreEdgesEqual(params float[] edges)
        {
            for (int i = 0; i < edges.Length - 1; i++)
            {
                for (int j = i + 1; j < edges.Length; j++)
                {
                    var e0 = edges[i];
                    var e1 = edges[j];

                    var ratio = Mathf.Max(e0, e1) / Mathf.Min(e0, e1);
                    if (ratio > EdgeRatioThreshold)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private int AddAverageVertex(params int[] indices)
        {
            var vertex = Vector3.zero;
            var normal = Vector3.zero;
            var uv = Vector2.zero;
            var color = Color.black;
            var size = 1.0f / indices.Length;

            foreach (var index in indices)
            {
                vertex += meshBuilder.GetVertex(index) * size;
                normal += meshBuilder.GetNormal(index) * size;

                if (uvs.Length != 0)
                {
                    uv += meshBuilder.GetUvs(index) * size;
                }

                if (colors.Length != 0)
                {
                    color += meshBuilder.GetColor(index) * size;
                }
            }

            var newIndex = meshBuilder.AddVertex(vertex);
            meshBuilder.AddNormal(normal.normalized);

            if (uvs.Length != 0)
            {
                meshBuilder.AddUv(uv);
            }

            if (colors.Length != 0)
            {
                meshBuilder.AddColor(color);
            }

            return newIndex;
        }

        private float GetSmallestTriangleArea()
        {
            var minArea = float.MaxValue;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var i0 = triangles[i];
                var i1 = triangles[i + 1];
                var i2 = triangles[i + 2];

                var v0 = vertices[i0];
                var v1 = vertices[i1];
                var v2 = vertices[i2];

                var area = CalcTriangleArea(v0, v1, v2);

                if (area < minArea)
                {
                    minArea = area;
                }
            }

            return minArea;
        }

        private float GetSmallestTriangleEdge()
        {
            var minEdge = float.MaxValue;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var i0 = triangles[i];
                var i1 = triangles[i + 1];
                var i2 = triangles[i + 2];

                var v0 = vertices[i0];
                var v1 = vertices[i1];
                var v2 = vertices[i2];

                var e0 = Vector3.Distance(v0, v1);
                var e1 = Vector3.Distance(v1, v2);
                var e2 = Vector3.Distance(v0, v2);

                var edge = Mathf.Min(e0, e1, e2);

                if (edge < minEdge)
                {
                    minEdge = edge;
                }
            }

            return minEdge;
        }

        private float CalcTriangleArea(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            var area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            return area;
        }
    }
}