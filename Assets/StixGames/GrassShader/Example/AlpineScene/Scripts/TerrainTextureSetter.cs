using UnityEngine;

[ExecuteInEditMode]
public class TerrainTextureSetter : MonoBehaviour
{
    public Texture2D[] Textures;

    public bool DoUpdate = false;

    void Update()
    {
        if (!DoUpdate)
        {
            return;
        }

        DoUpdate = false;

        var terrain = GetComponent<Terrain>();
        var terrainData = terrain.terrainData;

        var alphas = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, Textures.Length];

        for (int x = 0; x < terrainData.alphamapWidth; x++)
        {
            for (int y = 0; y < terrainData.alphamapHeight; y++)
            {
                for (int i = 0; i < Textures.Length; i++)
                {
                    alphas[x, y, i] = Textures[i].GetPixel(x, terrainData.alphamapHeight-y).r;
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, alphas);
    }
}