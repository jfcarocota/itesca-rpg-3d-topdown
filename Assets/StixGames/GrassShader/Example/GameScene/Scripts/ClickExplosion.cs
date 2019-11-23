using UnityEngine;

namespace StixGames.GrassShader.Example
{
    public class ClickExplosion : MonoBehaviour
    {
        public Transform Prefab;

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    Instantiate(Prefab, hit.point, Quaternion.identity, null);
                }
            }
        }
    }
}