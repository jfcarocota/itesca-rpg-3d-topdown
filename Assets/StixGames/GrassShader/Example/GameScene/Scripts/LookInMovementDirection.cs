using UnityEngine;

namespace StixGames.GrassShader.Example
{
    [ExecuteInEditMode]
    public class LookInMovementDirection : MonoBehaviour
    {
        public float Tolerance = 0.1f;

        private Vector3 lastPos;

        void Start()
        {
            lastPos = transform.position;
        }

        void Update()
        {
            var newPos = transform.position;
            var dir = newPos - lastPos;

            if (dir.sqrMagnitude > Tolerance)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }

            lastPos = newPos;
        }
    }
}