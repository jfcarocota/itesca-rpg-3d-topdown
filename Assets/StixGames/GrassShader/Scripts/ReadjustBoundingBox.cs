using UnityEngine;

namespace StixGames.GrassShader
{
	[RequireComponent(typeof(MeshFilter))]
	[AddComponentMenu("Stix Games/Deprecated/Readjust Bounding Box")]
    public class ReadjustBoundingBox : MonoBehaviour
	{
		public float maxGrassHeight = 2;

		void Start ()
		{
			var mesh = GetComponent<MeshFilter>().mesh;
			var bounds = mesh.bounds;
			bounds.Expand(maxGrassHeight * 2);
			mesh.bounds = bounds;
			Destroy(this);
		}
	}
}
