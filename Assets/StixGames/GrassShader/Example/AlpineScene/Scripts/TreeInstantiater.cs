using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteInEditMode]
public class TreeInstantiater : MonoBehaviour
{
    public TextAsset PointFile;
    public Transform[] Foliage;

    public float RotationOffset = 0.05f;

    public bool DoUpdate;

	void Update ()
    {
        if (!DoUpdate)
        {
            return;
        }

        DoUpdate = false;

        var text = PointFile.text;
        var lines = text.Split('\n');

        foreach (var child in GetComponentsInChildren<Transform>()) {
            if (child == transform)
            {
                continue;
            }
            DestroyImmediate(child.gameObject);
        }

        foreach (var line in lines)
        {
            var coords = line.Split('\t');
            var pos = new Vector3(-float.Parse(coords[0]), float.Parse(coords[1]), float.Parse(coords[2])) + transform.position;

            var index = Random.Range(0, Foliage.Length);

            var randomOffset2D = Random.insideUnitCircle * RotationOffset;
            var randomOffset = new Vector3(randomOffset2D.x, 0, randomOffset2D.y);
            var obj = GameObject.Instantiate(Foliage[index], pos,
                Quaternion.AngleAxis(Random.Range(0.0f, 360.0f), Vector3.up + randomOffset));
            obj.parent = transform;
        }
    }
}
