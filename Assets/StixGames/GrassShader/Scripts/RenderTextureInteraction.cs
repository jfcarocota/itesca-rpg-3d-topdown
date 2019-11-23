using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StixGames.GrassShader
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Stix Games/Interaction/Render Texture Interaction", 0)]
    public class RenderTextureInteraction : MonoBehaviour
    {
        [Tooltip("The size your interaction texture will have. Higher texture sizes will use more graphics memory and increase the performance cost, but will allow more detailed interaction.")]
        public int TextureSize = 1024;

        [Tooltip("Outside of the interaction cameras render area no interaction information is available for the shader. This setting creates a smooth transition between the interaction area and the outside area.")]
        public float BorderSmoothingArea = 2;

        private Camera cam;
        private RenderTexture backInteraction, interaction, backBurn, burn;
        private CommandBuffer commandBuffer;

        private static HashSet<IInteractionMesh> interactionMeshes = new HashSet<IInteractionMesh>();
        private static HashSet<IInteractionRenderer> interactionRenderers = new HashSet<IInteractionRenderer>();
        public static RenderTextureInteraction Instance { get; private set; }

        public static void AddInteractionObject(IInteractionMesh obj)
        {
            interactionMeshes.Add(obj);
        }

        public static void RemoveInteractionObject(IInteractionMesh obj)
        {
            interactionMeshes.Remove(obj);
        }

        public static void AddInteractionObject(IInteractionRenderer obj)
        {
            interactionRenderers.Add(obj);
        }

        public static void RemoveInteractionObject(IInteractionRenderer obj)
        {
            interactionRenderers.Remove(obj);
        }

        public void Awake()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("There may only be one RenderTextureinteraction camera");
            }

            Instance = this;

            cam = GetComponent<Camera>();
        }

        // Use this for initialization
        private void OnEnable()
        {
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.Nothing;
            cam.aspect = 1;
            cam.enabled = false;

            //Create render textures
            backInteraction = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);
            interaction = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);
            backBurn = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);
            burn = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);

            commandBuffer = new CommandBuffer();
            commandBuffer.name = "Grass interaction command buffer";
            
            //Set shader values
            Shader.EnableKeyword("GRASS_RENDERTEXTURE_INTERACTION");
        }

        private void OnDisable()
        {
            Instance = null;

            //Set shader values
            Shader.DisableKeyword("GRASS_RENDERTEXTURE_INTERACTION");

            //Destroy render textures
            RenderTexture.ReleaseTemporary(backInteraction);
            RenderTexture.ReleaseTemporary(interaction);
            RenderTexture.ReleaseTemporary(backBurn);
            RenderTexture.ReleaseTemporary(burn);
        }

        private void LateUpdate()
        {
            Shader.SetGlobalFloat("_GrassInteractionBorderArea", BorderSmoothingArea);

            //Save original position and set camera to be pixel perfect
            Vector3 realPosition = transform.position;

            float pixelSize = (2 * cam.orthographicSize) / TextureSize;

            Vector3 pos = realPosition;
            pos.x -= pos.x % pixelSize;
            pos.z -= pos.z % pixelSize;
            transform.position = pos;

            //Update camera rotation
            transform.rotation = Quaternion.Euler(90, 0, 0);

            Vector3 bottomLeft = cam.ScreenToWorldPoint(Vector3.zero);
            float size = 2 * cam.orthographicSize;

            Vector4 renderArea = new Vector4(bottomLeft.x, bottomLeft.z, size, size);

            Shader.SetGlobalVector("_GrassRenderTextureArea", renderArea);

            //Render Textures
            RenderInteractionTextures();
        }

        private void RenderInteractionTextures()
        {
            interactionMeshes.RemoveWhere(x => x == null || x.GetMesh() == null);
            interactionRenderers.RemoveWhere(x => x == null || x.GetRenderer() == null);

            //Create command buffer
            commandBuffer.Clear();
            commandBuffer.BeginSample("Grass Interaction Rendering");
            commandBuffer.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
            commandBuffer.SetViewport(new Rect(0, 0, TextureSize, TextureSize));

            //Clear textures
            commandBuffer.SetRenderTarget(backInteraction);
            commandBuffer.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 1.0f, 0.0f));
            commandBuffer.SetRenderTarget(interaction);
            commandBuffer.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 1.0f, 0.0f));
            commandBuffer.SetRenderTarget(backBurn);
            commandBuffer.ClearRenderTarget(false, true, Color.white);
            commandBuffer.SetRenderTarget(burn);
            commandBuffer.ClearRenderTarget(false, true, Color.white);

            //Set back buffer
            commandBuffer.SetGlobalTexture("_GrabInteraction", backInteraction);
            commandBuffer.SetGlobalTexture("_GrabBurn", backBurn);

            //Set render target
            var buffers = new RenderTargetIdentifier[]
            {
                interaction,
                burn
            };

            //var backBuffers = new RenderTargetIdentifier[]
            //{
            //    backInteraction,
            //    backBurn
            //};

            var backInteractionIdentifier = new RenderTargetIdentifier(backInteraction);
            var backBurnIdentifier = new RenderTargetIdentifier(backBurn);
            var interactionIdentifier = new RenderTargetIdentifier(interaction);
            var burnIdentifier = new RenderTargetIdentifier(burn);

            //Render all meshes
            foreach (var o in interactionMeshes)
            {
                //Blit back
                commandBuffer.Blit(interactionIdentifier, backInteractionIdentifier);
                commandBuffer.Blit(burnIdentifier, backBurnIdentifier);

                //Reset back buffer
                commandBuffer.SetGlobalTexture("_GrabInteraction", backInteractionIdentifier);
                commandBuffer.SetGlobalTexture("_GrabBurn", backBurnIdentifier);
                commandBuffer.SetRenderTarget(buffers, interactionIdentifier);

                //Render
                commandBuffer.DrawMesh(o.GetMesh(), o.GetMatrix(), o.GetMaterial());

                //TODO: Try to get ping pong working
                ////Ping pong
                //var tempBuffer = backBuffers;
                //backBuffers = buffers;
                //buffers = tempBuffer;

                //var tempIdentifier = backInteractionIdentifier;
                //backInteractionIdentifier = interactionIdentifier;
                //interactionIdentifier = tempIdentifier;

                //tempIdentifier = backBurnIdentifier;
                //backBurnIdentifier = burnIdentifier;
                //burnIdentifier = tempIdentifier;
            }

            //Render all renderers
            foreach (var o in interactionRenderers)
            {
                //Blit back
                commandBuffer.Blit(interactionIdentifier, backInteractionIdentifier);
                commandBuffer.Blit(burnIdentifier, backBurnIdentifier);

                //Reset back buffer
                commandBuffer.SetGlobalTexture("_GrabInteraction", backInteractionIdentifier);
                commandBuffer.SetGlobalTexture("_GrabBurn", backBurnIdentifier);
                commandBuffer.SetRenderTarget(buffers, interactionIdentifier);

                //Render
                var r = o.GetRenderer();
                commandBuffer.DrawRenderer(r, r.sharedMaterial);

                ////Ping pong
                //var tempBuffer = backBuffers;
                //backBuffers = buffers;
                //buffers = tempBuffer;

                //var tempIdentifier = backInteractionIdentifier;
                //backInteractionIdentifier = interactionIdentifier;
                //interactionIdentifier = tempIdentifier;

                //tempIdentifier = backBurnIdentifier;
                //backBurnIdentifier = burnIdentifier;
                //burnIdentifier = tempIdentifier;
            }

            commandBuffer.EndSample("Grass Interaction Rendering");

            //Not sure if a command buffer actually helps here, but it won't hurt either way
            Graphics.ExecuteCommandBuffer(commandBuffer);

            Shader.SetGlobalTexture("_GrassRenderTextureInteraction", interaction);
            Shader.SetGlobalTexture("_GrassRenderTextureBurn", burn);
        }
    }
}