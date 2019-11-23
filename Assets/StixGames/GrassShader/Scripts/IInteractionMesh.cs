using UnityEngine;

namespace StixGames.GrassShader
{
    public interface IInteractionMesh
    {
        Mesh GetMesh();

        Matrix4x4 GetMatrix();

        Material GetMaterial();
    }

    public interface IInteractionRenderer
    {
        Renderer GetRenderer();
    }
}