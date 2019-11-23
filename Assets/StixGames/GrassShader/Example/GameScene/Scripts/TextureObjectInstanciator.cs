using System.Collections.Generic;
using StixGames.NatureCore.Utility;
using UnityEngine;

namespace StixGames.GrassShader.Example
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    public class TextureObjectInstanciator : MonoBehaviour
    {
        public int MinDensity = 1;
        public int MaxDensity = 5;
        public float MinScale = 1.0f;
        public float MaxScale = 1.2f;
        public Transform[] Objects;
        public Texture2D DensityTexture;
        public int Seed = 1234;

        public bool DoUpdate = false;

        public void Update()
        {
            if (!DoUpdate)
            {
                return;
            }

            DoUpdate = false;

            var objects = new List<Transform>();
            foreach (Transform child in transform)
            {
                objects.Add(child);
            }

            foreach (var o in objects)
            {
#if UNITY_EDITOR
                DestroyImmediate(o.gameObject);
#else
                Destroy(o.gameObject);
#endif
            }

            var rng = new System.Random(Seed);
            var posSampler = new Sampler2D(new HaltonSampler(2, rng.Range(1, 1000)),
                new HaltonSampler(3, rng.Range(1, 1000)));

            var mesh = GetComponent<MeshFilter>().sharedMesh;
            var triangles = mesh.triangles;
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var t0 = triangles[i];
                var t1 = triangles[i + 1];
                var t2 = triangles[i + 2];
                var v0 = vertices[t0];
                var v1 = vertices[t1];
                var v2 = vertices[t2];
                var uv0 = uvs[t0];
                var uv1 = uvs[t1];
                var uv2 = uvs[t2];

                var triangle = new Triangle(v0, v1, v2, uv0, uv1, uv2);

                var objectCount = rng.Range(MinDensity, MaxDensity);
                for (int j = 0; j < objectCount; j++)
                {
                    var coords = triangle.GetRandomCoordinates(posSampler);
                    var pos = triangle.GetPosition(coords);
                    var uv = triangle.GetUVCoordinates(coords);

                    var density = DensityTexture.GetPixelBilinear(uv.x, uv.y);

                    if (density.r > rng.Range(0.0f, 1.0f))
                    {
                        CreateInstance(pos, rng);
                    }
                }
            }
        }

        private void CreateInstance(Vector3 pos, System.Random rng)
        {
            var scale = rng.Range(MinScale, MaxScale);
            var index = rng.Range(0, Objects.Length);

            var instance = Instantiate(Objects[index], transform.TransformPoint(pos), Quaternion.identity, transform);
            instance.localScale = new Vector3(scale / transform.localScale.x, scale / transform.localScale.y, scale / transform.localScale.z);
        }
    }
}