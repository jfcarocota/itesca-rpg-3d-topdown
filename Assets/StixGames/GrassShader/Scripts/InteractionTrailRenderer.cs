using System;
using System.Collections.Generic;
using UnityEngine;

namespace StixGames.GrassShader
{
    [ExecuteInEditMode]
    [AddComponentMenu("Stix Games/Interaction/Interaction Trail Renderer", 2)]
    public class InteractionTrailRenderer : MonoBehaviour, IInteractionMesh
    {
        [Header("Performance")]
        [Tooltip("The minimum distance between two vertices in the trail.\n" +
                 "A lower number will create a more detailed trail, but will increase performance cost.")]
        public float MinVertexDistance = 0.5f;

        [Header("Behaviour")]
        [Tooltip(
            "When enabled, the script will automatically try to detect if the character is jumping.\n" +
            "You could also do this manually by setting the IsJumping variable.")]
        public bool UseJumpDetection = false;

        [Tooltip("The distance from the floor, at which the trail will stop interacting with the grass.")]
        public float JumpDetectionDistance = 1.0f;

        [Tooltip("The layers that will be used to detect character jumping. " +
                 "Jump detection will only work if your floor object has a collider and is on one of these layers.")]
        public LayerMask FloorLayers;

        [Header("Visuals")]
        [Tooltip("The lifetime of your trail in seconds. After this time, the trail will vanish.")]
        public float Lifetime = 5;

        [Tooltip(
            "The base width of your trail, which will be multiplied by the current value of your animation curve.")]
        public float BaseWidth = 1;

        [Tooltip("Defines how the width of your trail will change with its lifetime.\n" +
                 "Time 0 is the start of the trail, time 1 is the end of its lifetime.\n" +
                 "The value at the current time will be multiplied with Base Width.\n" +
                 "\n" +
                 "For example a trail vertex with half of its lifetime will multiply the value of 0.5 with Base Width.")]
        public AnimationCurve Width = AnimationCurve.Linear(0, 1, 1, 0);

        [Range(0, 1)]
        [Tooltip(
            "The base strength of your trail, which will be multiplied by the current value of your animation curve. " +
            "A lower value will cause less interaction.")] public float BaseStrength = 1;

        [Tooltip("Defines how the strength of your trail will change with its lifetime.\n" +
                 "Time 0 is the start of the trail, time 1 is the end of its lifetime.\n" +
                 "The value at the current time will be multiplied with Base Strength.\n" +
                 "\n" +
                 "For example a trail vertex with half of its lifetime will multiply the value of 0.5 with Base Strength.")]
        public AnimationCurve Strength = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Tooltip("The interaction material, " +
                 "which must use the Mesh Normal Renderer shader and must have a normal map to work as intended.")]
        public Material Material;

        [Tooltip("If enabled, the trail is rendered for the main camera, making it easier to debug.")]
        public bool DebugDraw;

        private Vector3 upDir = Vector3.up;

        private List<TrailPoint> points = new List<TrailPoint>();
        private Mesh mesh;

        private List<Vector3> vertices = new List<Vector3>();
        private List<int> triangles = new List<int>();
        private List<Vector2> uvs = new List<Vector2>();
        private List<Vector3> normals = new List<Vector3>();
        private List<Vector4> tangents = new List<Vector4>();
        private List<Color> colors = new List<Color>();

        public bool IsJumping { get; set; }

        void Start()
        {
            mesh = new Mesh();
        }

        void LateUpdate()
        {
            //Detect jumping
            if (UseJumpDetection)
            {
                Ray ray = new Ray(transform.position, Vector3.down);
                IsJumping = !Physics.Raycast(ray, JumpDetectionDistance, FloorLayers);
            }
            else
            {
                IsJumping = false;
            }

            //Remove points that are older than lifetime
            while (points.Count > 0)
            {
                if (Time.time - points[0].creationTime > Lifetime)
                {
                    points.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }

            Vector3 pos = transform.position;
            bool addedPoint = false;

            //Add new point if list is empty or distance to last point is bigger than minVertexDistance
            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1].pos, pos) > MinVertexDistance)
            {
                points.Add(new TrailPoint(pos, Time.time, IsJumping));
                addedPoint = true;
            }

            List<TrailPoint> renderPoints = new List<TrailPoint>(points);

            //If no point was added this frame, use the current position as point
            if (!addedPoint)
            {
                renderPoints.Add(new TrailPoint(pos, Time.time, IsJumping));
            }

            //If there are less than 2 points, don't render the trail.
            if (renderPoints.Count < 2)
            {
                return;
            }

            UpdateMesh(renderPoints);

            if (DebugDraw)
            {
                Graphics.DrawMesh(mesh, Matrix4x4.identity, Material, 0);
            }

            if (Material == null)
            {
                Debug.LogWarning("Trail renderer requires a material to work.");
            }
            else
            {
                RenderTextureInteraction.AddInteractionObject(this);
            }
        }

        private void UpdateMesh(List<TrailPoint> renderPoints)
        {
            mesh.Clear();
            if (renderPoints.Count < 2)
            {
                return;
            }

            //Clear lists
            vertices.Clear();
            triangles.Clear();
            uvs.Clear();
            normals.Clear();
            tangents.Clear();
            colors.Clear();

            float uvFactor = 1.0f / (renderPoints.Count - 1);

            //Iterate though all previous points
            for (int i = 0; i < renderPoints.Count; i++)
            {
                //First point
                TrailPoint point = renderPoints[i];
                if (i == 0)
                {
                    AddPoint(point, renderPoints[i + 1].pos - point.pos, 0);
                    continue;
                }

                //Last point
                TrailPoint lastPoint = renderPoints[i - 1];
                if (i == renderPoints.Count - 1)
                {
                    AddPoint(point, point.pos - lastPoint.pos, 1);
                    break;
                }

                //In-between points
                TrailPoint nextPoint = renderPoints[i + 1];

                AddPoint(point, nextPoint.pos - lastPoint.pos, i * uvFactor);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetColors(colors);
        }

        private void AddPoint(TrailPoint point, Vector3 direction, float uv)
        {
            float lifePercent = (Time.time - point.creationTime) / Lifetime;
            float halfWidth = BaseWidth * Width.Evaluate(lifePercent);
            float isJumpingValue = point.isJumping ? 0.0f : 1.0f;
            float normalStrength =
                Mathf.Clamp(isJumpingValue * BaseStrength * Strength.Evaluate(lifePercent), 0.001f, 1);
            Color normalStrengthColor = new Color(normalStrength, normalStrength, normalStrength, normalStrength);

            //Direction shouldn't be influenced by the height the character is moving at, so we set it to 0
            direction.y = 0;
            direction.Normalize();
            Vector3 pos = point.pos;
            Vector3 right = Vector3.Cross(upDir, direction);

            vertices.Add(pos - right * halfWidth);
            vertices.Add(pos + right * halfWidth);
            uvs.Add(new Vector2(0, uv));
            uvs.Add(new Vector2(1, uv));
            normals.Add(upDir);
            normals.Add(upDir);
            tangents.Add(new Vector4(right.x, right.y, right.z, 1));
            tangents.Add(new Vector4(right.x, right.y, right.z, 1));
            colors.Add(normalStrengthColor);
            colors.Add(normalStrengthColor);

            int lastVert = vertices.Count - 1;
            if (lastVert >= 3)
            {
                triangles.Add(lastVert - 1);
                triangles.Add(lastVert);
                triangles.Add(lastVert - 2);

                triangles.Add(lastVert - 2);
                triangles.Add(lastVert - 3);
                triangles.Add(lastVert - 1);
            }
        }

        public void ClearPath()
        {
            points.Clear();
            mesh.Clear();
        }

        public Mesh GetMesh()
        {
            return mesh;
        }

        public Matrix4x4 GetMatrix()
        {
            return Matrix4x4.identity;
        }

        public Material GetMaterial()
        {
            return Material;
        }
    }

    [Serializable]
    public struct TrailPoint
    {
        public Vector3 pos;
        public float creationTime;
        public bool isJumping;

        public TrailPoint(Vector3 pos, float creationTime, bool isJumping)
        {
            this.pos = pos;
            this.creationTime = creationTime;
            this.isJumping = isJumping;
        }
    }
}