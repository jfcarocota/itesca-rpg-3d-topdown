using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StixGames.NatureCore
{
    public class MeshBuilder
    {
        private const int MaxVertices = 65535;

        private readonly List<Mesh> meshes = new List<Mesh>();

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        private readonly List<Vector3> tempVertices = new List<Vector3>();
        private readonly List<Vector3> tempNormals = new List<Vector3>();
        private readonly List<Vector2> tempUvs = new List<Vector2>();
        private readonly List<Color> tempColors = new List<Color>();
        private readonly List<int> tempTriangles = new List<int>();

        private Dictionary<int, int> vertexMap = new Dictionary<int, int>();
        private int vertexCount = 0;

        public Mesh[] CreateMeshes()
        {
            Clear();

            for (int i = 0; i < triangles.Count; i += 3)
            {
                //If the triangle doesn't fit into the current mesh, start a new submesh
                if (vertexCount + NewVertices(i) >= MaxVertices)
                {
                    CreateSubmesh();
                }

                int i0 = AddSubmeshVertex(triangles[i]);
                int i1 = AddSubmeshVertex(triangles[i+1]);
                int i2 = AddSubmeshVertex(triangles[i+2]);

                tempTriangles.Add(i0);
                tempTriangles.Add(i1);
                tempTriangles.Add(i2);
            }

            CreateSubmesh();

            return meshes.ToArray();
        }

        private int AddSubmeshVertex(int i)
        {
            if (vertexMap.ContainsKey(i))
            {
                return vertexMap[i];
            }

            int index = vertexCount;
            vertexMap[i] = index;

            tempVertices.Add(vertices[i]);
            tempNormals.Add(normals[i]);

            if (uvs.Count > 0)
            {
                tempUvs.Add(uvs[i]);
            }

            if (colors.Count > 0)
            {
                tempColors.Add(colors[i]);
            }

            vertexCount++;
            return index;
        }

        private void CreateSubmesh()
        {
            var mesh = new Mesh();

            mesh.SetVertices(tempVertices);
            mesh.SetNormals(tempNormals);
            mesh.SetUVs(0, tempUvs);
            mesh.SetColors(tempColors);
            mesh.SetTriangles(tempTriangles, 0);

            tempVertices.Clear();
            tempNormals.Clear();
            tempUvs.Clear();
            tempColors.Clear();
            tempTriangles.Clear();

            vertexMap.Clear();
            vertexCount = 0;

            meshes.Add(mesh);
        }

        private int NewVertices(int i)
        {
            int count = 0;

            if (!vertexMap.ContainsKey(i))
            {
                count++;
            }

            if (!vertexMap.ContainsKey(i + 1))
            {
                count++;
            }

            if (!vertexMap.ContainsKey(i + 2))
            {
                count++;
            }

            return count;
        }

        public void Clear()
        {
            foreach (var mesh in meshes)
            {
                Object.Destroy(mesh);
            }
        }

        public int AddVertex(Vector3 vertex)
        {
            var index = vertices.Count;
            vertices.Add(vertex);

            return index;
        }

        public void AddNormal(Vector3 normal)
        {
            normals.Add(normal);
        }

        public void AddUv(Vector2 uv)
        {
            uvs.Add(uv);
        }

        public void AddColor(Color color)
        {
            colors.Add(color);
        }

        public void AddTriangle(int i0, int i1, int i2)
        {
            triangles.Add(i0);
            triangles.Add(i1);
            triangles.Add(i2);
        }

        public void AddVertices(IEnumerable<Vector3> newVertices)
        {
            vertices.AddRange(newVertices);
        }

        public void AddNormals(IEnumerable<Vector3> newNormals)
        {
            normals.AddRange(newNormals);
        }

        public void AddUvs(IEnumerable<Vector2> newUvs)
        {
            uvs.AddRange(newUvs);
        }

        public void AddColors(IEnumerable<Color> newColors)
        {
            colors.AddRange(newColors);
        }

        public Vector3 GetVertex(int index)
        {
            return vertices[index];
        }

        public Vector3 GetNormal(int index)
        {
            return normals[index];
        }

        public Vector2 GetUvs(int index)
        {
            return uvs[index];
        }

        public Color GetColor(int index)
        {
            return colors[index];
        }
    }
}
