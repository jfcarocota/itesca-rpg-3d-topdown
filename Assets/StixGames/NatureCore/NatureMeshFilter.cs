using System;
using System.Collections.Generic;
using StixGames.GrassShader;
using StixGames.NatureCore.UnityOctree;
using UnityEngine;

namespace StixGames.NatureCore
{
    /// <summary>
    /// Processes the mesh and passes it to other components.
    /// </summary>
    [ExecuteInEditMode]
    [AddComponentMenu("Stix Games/General/Nature Mesh Filter", 0)]
    public class NatureMeshFilter : MonoBehaviour
    {
        [Tooltip("When enabled, the mesh from your mesh filter is normalized before use, " +
                 "so triangles that are too large get split into smaller ones.\n" +
                 "This does not work for meshes with extremely deformed triangles.")]
        public bool NormalizeTriangleSizes;

        [Delayed]
        [Tooltip("You can use this setting to increase the polygon density of the mesh further than necessary for normalization. " +
                 "This setting is still overridden by the Min Edge Size setting.")]
        public float MeshSubdivision = 1.0f;

        [Delayed]
        [Tooltip("If an edge is larger than the length of the smallest edge times this value, it will be split.")]
        public float EdgeSplitThreshold = 1.5f;

        [Delayed]
        [Tooltip("The max ratio at which edges will be considered equal.")]
        public float EdgeRatioThreshold = 1.1f;

        [Delayed]
        [Tooltip("The smallest edge size, edges below this value will no longer be split. If you are having performance problems, try setting this to a higher value.")]
        public float MinEdgeSize = 0.1f;

        [Delayed]
        [Tooltip("Having a very large amount of triangles will slow down grass fallback significantly. You can use this settings to stop normalization early.")]
        public float FallbackMinEdgeSize = 2.0f;

        [Tooltip("The mesh that will be used to create grass and foliage. " +
                 "If empty, it will try to get a mesh from a mesh filter or terrain.")]
        [SerializeField] 
        private Mesh[] meshes;

        [SerializeField]
        private Mesh[] fallbackMeshes;

        private BoundsOctree<Triangle> octree;

#if UNITY_EDITOR
        private Vector3 lastPosition;

        public void Update()
        {
            //When the object is moved, the octree doesn't automatically update (performance)
            if (Vector3.Distance(lastPosition, transform.position) > Mathf.Epsilon)
            {
                octree = null;
                lastPosition = transform.position;
            }
        }
#endif

        /// <summary>
        /// Get the processed mesh, used for rendering nature elements.
        /// </summary>
        /// <returns></returns>
        public Mesh[] GetMeshes(bool useFallbackMeshes = false)
        {
            if (!useFallbackMeshes && (meshes == null || meshes.Length == 0))
            {
                CreateRawMeshes(false);
            }

            if (useFallbackMeshes && (fallbackMeshes == null || fallbackMeshes.Length == 0))
            {
                CreateRawMeshes(true);
            }

            if (useFallbackMeshes)
            {
                return fallbackMeshes;
            }

            return meshes;
        }

        private void CreateRawMeshes(bool useFallbackMeshes)
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                var mesh = meshFilter.sharedMesh;

                if (NormalizeTriangleSizes)
                {
                    var normalizer = new MeshTriangleNormalizer(mesh);
                    normalizer.MinEdgeMultiplier = Mathf.Max(MeshSubdivision, 1);
                    normalizer.EdgeSplitThreshold = EdgeSplitThreshold;
                    normalizer.EdgeRatioThreshold = EdgeRatioThreshold;
                    normalizer.MinEdgeSize = useFallbackMeshes ? FallbackMinEdgeSize : MinEdgeSize;

                    if (useFallbackMeshes)
                    {
                        fallbackMeshes = normalizer.NormalizeMeshes();
                    }
                    else
                    {
                        meshes = normalizer.NormalizeMeshes();
                    }
                }
                else
                {
                    if (useFallbackMeshes)
                    {
                        fallbackMeshes = new[] { mesh };
                    }
                    else
                    {
                        meshes = new[] { mesh };
                    }
                }

                return;
            }

            //Just convert the terrain to a mesh and pretend everything is fine
            var terrain = GetComponent<Terrain>();
            if (terrain != null)
            {
                ConvertTerrain(terrain, useFallbackMeshes);
                return;
            }

            throw new InvalidOperationException("The object does not have a MeshFilter or a Terrain.");
        }

        private void ConvertTerrain(Terrain terrain, bool useFallbackMeshes)
        {
            if (terrain.transform.lossyScale != Vector3.one || terrain.transform.rotation != Quaternion.identity)
            {
                Debug.LogWarning("Your terrain is either rotated, or scaled. While the terrain isn't affected by this, the grass renderer is. Please set scale to (1,1,1) and rotation to (0,0,0), your terrain doesn't change either way.");
            }

            var terrainConverter = new TerrainRemesher(terrain);
            terrainConverter.SampleMultiplier = Mathf.Max(MeshSubdivision, 1);

            var newMeshes = terrainConverter.GenerateTerrainMesh();

            //TODO: Different version for fallback?
            meshes = newMeshes;
            fallbackMeshes = newMeshes;
        }

        /// <summary>
        /// Get the generated octree, used for placing objects in the scene.
        /// </summary>
        /// <returns></returns>
        public BoundsOctree<Triangle> GetTriangleOctree(bool useFallbackMeshes = true)
        {
            if (octree == null)
            {
                CreateTriangleOctree(useFallbackMeshes);
            }

            return octree;
        }

        private void CreateTriangleOctree(bool useFallbackMeshes)
        {
            octree = new BoundsOctree<Triangle>(1, transform.position, 1, 1.2f);

            var targetMeshes = GetMeshes(useFallbackMeshes);

            int index = 0;
            foreach (var mesh in targetMeshes)
            {
                //Cache the arrays here, all of them are actually properties that add high overhead
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                var colors = mesh.colors;
                var triangles = mesh.triangles;

                //Process the mesh into the octree
                var t = transform;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    int t0 = triangles[i];
                    int t1 = triangles[i + 1];
                    int t2 = triangles[i + 2];

                    var v0 = t.TransformPoint(vertices[t0]);
                    var v1 = t.TransformPoint(vertices[t1]);
                    var v2 = t.TransformPoint(vertices[t2]);

                    Vector2 uv0;
                    Vector2 uv1;
                    Vector2 uv2;
                    if (uv.Length > 0)
                    {
                        uv0 = uv[t0];
                        uv1 = uv[t1];
                        uv2 = uv[t2];
                    }
                    else
                    {
                        uv0 = Vector2.zero;
                        uv1 = Vector2.zero;
                        uv2 = Vector2.zero;
                    }

                    Color c0;
                    Color c1;
                    Color c2;
                    if (colors.Length > 0)
                    {
                        c0 = colors[t0];
                        c1 = colors[t1];
                        c2 = colors[t2];
                    }
                    else
                    {
                        c0 = Color.white;
                        c1 = Color.white;
                        c2 = Color.white;
                    }

                    var triangle = new Triangle(v0, v1, v2, c0, c1, c2, uv0, uv1, uv2);
                    triangle.Index = index;

                    octree.Add(triangle, triangle.Bounds);
                    index++;
                }
            }
        }

        /// <summary>
        /// Returns the meshes in the form of a triangle list. This is not cached, so don't call it too frequently.
        /// </summary>
        public List<Triangle> GetTriangleList(bool useFallbackMeshes = true)
        {
            var triangleList = new List<Triangle>();

            var targetMeshes = GetMeshes(useFallbackMeshes);

            int index = 0;
            foreach (var mesh in targetMeshes)
            {
                //Cache the arrays here, all of them are actually properties that add high overhead
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                var colors = mesh.colors;
                var triangles = mesh.triangles;

                //Process the mesh into the octree
                var t = transform;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    int t0 = triangles[i];
                    int t1 = triangles[i + 1];
                    int t2 = triangles[i + 2];

                    var v0 = t.TransformPoint(vertices[t0]);
                    var v1 = t.TransformPoint(vertices[t1]);
                    var v2 = t.TransformPoint(vertices[t2]);

                    Vector2 uv0;
                    Vector2 uv1;
                    Vector2 uv2;
                    if (uv.Length > 0)
                    {
                        uv0 = uv[t0];
                        uv1 = uv[t1];
                        uv2 = uv[t2];
                    }
                    else
                    {
                        uv0 = Vector2.zero;
                        uv1 = Vector2.zero;
                        uv2 = Vector2.zero;
                    }

                    Color c0;
                    Color c1;
                    Color c2;
                    if (colors.Length > 0)
                    {
                        c0 = colors[t0];
                        c1 = colors[t1];
                        c2 = colors[t2];
                    }
                    else
                    {
                        c0 = Color.white;
                        c1 = Color.white;
                        c2 = Color.white;
                    }

                    var triangle = new Triangle(v0, v1, v2, c0, c1, c2, uv0, uv1, uv2);
                    triangle.Index = index;

                    triangleList.Add(triangle);
                    index++;
                }
            }

            return triangleList;
        }

        public void ResetCache()
        {
            meshes = null;
            fallbackMeshes = null;
            octree = null;
        }
    }
}