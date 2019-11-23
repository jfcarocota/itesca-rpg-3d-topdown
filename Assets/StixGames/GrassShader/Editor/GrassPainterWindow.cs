using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StixGames.NatureCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace StixGames.GrassShader
{
    public class GrassPainterWindow : EditorWindow
    {
        private static readonly GUIContent[] TargetTextureLabels =
        {
            new GUIContent("Color / Height"),
            new GUIContent("Density")
        };

        private static readonly string[] ColorHeightChannelLabels =
        {
            "Color",
            "Height",
            "Both"
        };

        private static readonly string[] DensityChannelLabels =
        {
            "1",
            "2",
            "3",
            "4"
        };

        private static readonly string[] PaintModeLabels =
        {
            "Blend",
            "Add",
            "Remove",
        };

        private static bool useUndo = true;
        private static bool searchRoot = true;

        private static GrassPainterWindow window;

        private GameObject grassObject;
        private Material grassMaterial;

        private List<Collider> tempColliders = new List<Collider>();

        private int textureSize = 512;
        private Color defaultColor = Color.white;

        private bool mouseDown;
        private Vector3 lastPaintPos;
        private bool didDraw;
        private bool showCloseMessage;
        private bool showTargetSwitchMessage;
        public GrassPainter grassPainter = new GrassPainter();

        [MenuItem("Window/Stix Games/Grass Painter")]
        public static void OpenWindow()
        {
            window = GetWindow<GrassPainterWindow>();
            window.Show();
        }

        [MenuItem("Window/Stix Games/Reset Grass Painter Position")]
        public static void ResetPosition()
        {
            window = GetWindow<GrassPainterWindow>();
            window.Show();
            window.position = Rect.zero;
        }

        private void RecreateWindow()
        {
            window = Instantiate(this);
            window.Show();
        }

        private void OnGUI()
        {
            TextureSettings();

            BrushSettings();

            PainterSettings();
        }

        private void TextureSettings()
        {
            if (grassObject == null)
            {
                EditorGUILayout.LabelField("No grass object selected. Select a grass object in the scene Hierarchy.");
                return;
            }

            EditorGUILayout.LabelField("Current object: " + grassObject.name);
            EditorGUILayout.Space();

            if (GrassUtility.GetDensityMode(grassMaterial) != DensityMode.Texture)
            {
                EditorGUILayout.LabelField("The grass material is not in texture density mode.",
                    EditorStyles.boldLabel);
                if (GUILayout.Button("Change material to texture density"))
                {
                    GrassUtility.SetDensityMode(grassMaterial, DensityMode.Texture);
                }

                EditorGUILayout.Space();
            }

            if (grassMaterial.GetTexture("_ColorMap") == null || grassMaterial.GetTexture("_DensityTexture") == null)
            {
                EditorGUILayout.LabelField("Create new texture", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                textureSize = EditorGUILayout.IntField("Texture Size", textureSize);
                defaultColor = EditorGUILayout.ColorField(new GUIContent("Default Color",
                    "The color the texture will start with. " +
                    "For a density texture, each color channel (RGBA) represents a grass types density. " +
                    "For color textures it's the color (RGB) and height (A) of the grass."), defaultColor);

                if (grassMaterial.GetTexture("_ColorMap") == null && GUILayout.Button("Create color height texture"))
                {
                    //Select the path and return on cancel
                    string path = EditorUtility.SaveFilePanelInProject("Create color height texture",
                        "newColorHeightTexture", "png", "Choose where to save the new color height texture");

                    if (string.IsNullOrEmpty(path))
                    {
                        return;
                    }

                    CreateTexture(path, TextureTarget.ColorHeight, defaultColor);
                }

                if (grassMaterial.GetTexture("_DensityTexture") == null && GUILayout.Button("Create density texture"))
                {
                    //Select the path and return on cancel
                    string path = EditorUtility.SaveFilePanelInProject("Create density texture", "newDensityTexture",
                        "png", "Choose where to save the new density texture");

                    if (string.IsNullOrEmpty(path))
                    {
                        return;
                    }

                    CreateTexture(path, TextureTarget.Density, defaultColor);
                }

                EditorGUILayout.Space();
            }
        }

        private void BrushSettings()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Brush settings", EditorStyles.boldLabel);

            //Target texture
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Target", "Select the target texture you wish to paint, " +
                                                                "either Color / Height texture or Density texture."));
            grassPainter.Target =
                (TextureTarget)GUILayout.SelectionGrid((int)grassPainter.Target, TargetTextureLabels, 2);
            EditorGUILayout.EndHorizontal();

            //Channel
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Channel", "Select the channel you want to modify. " +
                                                                 "Depending on the paint target, this can be color, height, or one of the grass type densities."));
            switch (grassPainter.Target)
            {
                case TextureTarget.ColorHeight:
                    grassPainter.ColorHeightChannel =
                        (ColorHeightChannel)GUILayout.SelectionGrid((int)grassPainter.ColorHeightChannel,
                            ColorHeightChannelLabels, 3);
                    break;
                case TextureTarget.Density:
                    grassPainter.DensityChannel =
                        (DensityChannel)GUILayout.SelectionGrid((int)grassPainter.DensityChannel,
                            DensityChannelLabels, 4);
                    break;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            //Paint mode
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Brush Mode", "Change the mode of your paint brush. " +
                                                              "You can either Add, or Remove a value, or set the desired value directly."));
            grassPainter.Brush = (BrushMode)GUILayout.SelectionGrid((int)grassPainter.Brush, PaintModeLabels, 3);
            EditorGUILayout.EndHorizontal();

            //TODO: Make slider max value dynamically changeable by writing into field.
            //For now, just change the right value if you want more or less size
            if (grassPainter.Target == TextureTarget.ColorHeight)
            {
                grassPainter.PaintColor = EditorGUILayout.ColorField(new GUIContent("Color (RGB) Height(A)",
                        "The color that will be used for painting. The alpha channel (Transparency) represents the height."),
                    grassPainter.PaintColor);
            }

            if (grassPainter.Target == TextureTarget.Density)
            {
                grassPainter.PaintDensity = EditorGUILayout.Slider(new GUIContent("Blend Density",
                        "The target density used when using the blend mode. In Add and Subtract, only the strength is used"),
                    grassPainter.PaintDensity, 0.0f, 1.0f);
            }

            grassPainter.Strength = EditorGUILayout.Slider(new GUIContent("Strength",
                "The brush strength with which color, " +
                "height, or density will be modified."), grassPainter.Strength, 0, 1);
            grassPainter.Size =
                EditorGUILayout.Slider(new GUIContent("Size", "The size of the brush in meters / Unity units."),
                    grassPainter.Size, 0.01f, 50);
            grassPainter.Softness = EditorGUILayout.Slider(new GUIContent("Softness",
                "With low smoothness the brush will have hard edges, " +
                "with high smoothness the borders of the brush will be smoothed out."), grassPainter.Softness, 0, 1);
            grassPainter.Spacing = EditorGUILayout.Slider(new GUIContent("Spacing",
                    "The spacing between each time the brush is applied. " +
                    "The lower this is, the smoother the drawn line will be, higher values will use less performance."),
                grassPainter.Spacing, 0, 2);
            //rotation = EditorGUILayout.Slider("Rotation", rotation, 0, 360);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt("StixGames.Painter.Target", (int)grassPainter.Target);
                EditorPrefs.SetInt("StixGames.Painter.ColorHeightChannel", (int)grassPainter.ColorHeightChannel);
                EditorPrefs.SetInt("StixGames.Painter.DensityChannel", (int)grassPainter.DensityChannel);
                EditorPrefs.SetInt("StixGames.Painter.BrushMode", (int)grassPainter.Brush);
                EditorPrefs.SetFloat("StixGames.Painter.BrushMode.R", grassPainter.PaintColor.r);
                EditorPrefs.SetFloat("StixGames.Painter.BrushMode.G", grassPainter.PaintColor.g);
                EditorPrefs.SetFloat("StixGames.Painter.BrushMode.B", grassPainter.PaintColor.b);
                EditorPrefs.SetFloat("StixGames.Painter.BrushMode.A", grassPainter.PaintColor.a);
                EditorPrefs.SetFloat("StixGames.Painter.PaintDensity", grassPainter.PaintDensity);
                EditorPrefs.SetFloat("StixGames.Painter.Strength", grassPainter.Strength);
                EditorPrefs.SetFloat("StixGames.Painter.Size", grassPainter.Size);
                EditorPrefs.SetFloat("StixGames.Painter.Softness", grassPainter.Softness);
                EditorPrefs.SetFloat("StixGames.Painter.Spacing", grassPainter.Spacing);
                EditorPrefs.SetFloat("StixGames.Painter.Rotation", grassPainter.Rotation);
            }

            EditorGUILayout.Space();
        }

        private void PainterSettings()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Painter settings", EditorStyles.boldLabel);

            useUndo = EditorGUILayout.ToggleLeft("Save Undo/Redo data (may cause lag)", useUndo);
            searchRoot = EditorGUILayout.ToggleLeft(new GUIContent("Search root for grass",
                    "Searches all children of the root object for grass objects. " +
                    "This can take a while, but will make it possible to paint on tiled objects with the same material."),
                searchRoot);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("StixGames.Painter.UseUndo", useUndo);
                EditorPrefs.SetBool("StixGames.Painter.SearchRoot", searchRoot);

                if (!useUndo && grassPainter.DensityTexture != null)
                {
                    Undo.ClearUndo(grassPainter.DensityTexture);
                }
            }
        }

        void OnFocus()
        {
            grassPainter.Target = (TextureTarget)EditorPrefs.GetInt("StixGames.Painter.Target",
                (int)grassPainter.Target);
            grassPainter.ColorHeightChannel = (ColorHeightChannel)EditorPrefs.GetInt(
                "StixGames.Painter.ColorHeightChannel",
                (int)grassPainter.ColorHeightChannel);
            grassPainter.DensityChannel = (DensityChannel)EditorPrefs.GetInt("StixGames.Painter.DensityChannel",
                (int)grassPainter.DensityChannel);
            grassPainter.Brush =
                (BrushMode)EditorPrefs.GetInt("StixGames.Painter.BrushMode", (int)grassPainter.Brush);
            grassPainter.PaintColor.r =
                EditorPrefs.GetFloat("StixGames.Painter.BrushMode.R", grassPainter.PaintColor.r);
            grassPainter.PaintColor.g =
                EditorPrefs.GetFloat("StixGames.Painter.BrushMode.G", grassPainter.PaintColor.g);
            grassPainter.PaintColor.b =
                EditorPrefs.GetFloat("StixGames.Painter.BrushMode.B", grassPainter.PaintColor.b);
            grassPainter.PaintColor.a =
                EditorPrefs.GetFloat("StixGames.Painter.BrushMode.A", grassPainter.PaintColor.a);
            grassPainter.PaintDensity =
                EditorPrefs.GetFloat("StixGames.Painter.PaintDensity", grassPainter.PaintDensity);
            grassPainter.Strength = EditorPrefs.GetFloat("StixGames.Painter.Strength", grassPainter.Strength);
            grassPainter.Size = EditorPrefs.GetFloat("StixGames.Painter.Size", grassPainter.Size);
            grassPainter.Softness = EditorPrefs.GetFloat("StixGames.Painter.Softness", grassPainter.Softness);
            grassPainter.Spacing = EditorPrefs.GetFloat("StixGames.Painter.Spacing", grassPainter.Spacing);
            grassPainter.Rotation = EditorPrefs.GetFloat("StixGames.Painter.Rotation", grassPainter.Rotation);
            showCloseMessage = EditorPrefs.GetBool("StixGames.Painter.ShowCloseMessage", true);
            showTargetSwitchMessage = EditorPrefs.GetBool("StixGames.Painter.ShowTargetSwitchMessage", true);
            useUndo = EditorPrefs.GetBool("StixGames.Painter.UseUndo", true);
            searchRoot = EditorPrefs.GetBool("StixGames.Painter.SearchRoot", true);

#if UNITY_2019_2_OR_NEWER
            SceneView.duringSceneGui -= DrawSceneGUI;
            SceneView.duringSceneGui += DrawSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= DrawSceneGUI;
            SceneView.onSceneGUIDelegate += DrawSceneGUI;
#endif

            Undo.undoRedoPerformed -= SaveTexture;
            Undo.undoRedoPerformed += SaveTexture;
        }

        void OnDestroy()
        {
#if UNITY_2019_2_OR_NEWER
            SceneView.duringSceneGui -= DrawSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= DrawSceneGUI;
#endif
            
            Undo.undoRedoPerformed -= SaveTexture;
            ResetRenderer(true);

            if (ShowUndoLossWarning(true))
            {
                Undo.ClearUndo(grassPainter.DensityTexture);
            }
            else
            {
                RecreateWindow();
            }
        }

        private bool ShowUndoLossWarning(bool isWindowClose)
        {
            if (isWindowClose && showCloseMessage || !isWindowClose && showTargetSwitchMessage)
            {
                string message = isWindowClose
                    ? "After closing the painter changes will be permanent, undo will no longer be possible."
                    : "After switching grass object changes will be permanent, undo will no longer be possible.";

                int result = EditorUtility.DisplayDialogComplex("Make changes permanent?",
                    message, isWindowClose ? "Close" : "Switch", "Cancel",
                    isWindowClose ? "Close and don't show again" : "Switch and don't show again");

                if (result == 1)
                {
                    return false;
                }

                if (result == 2)
                {
                    if (showCloseMessage)
                    {
                        showCloseMessage = false;
                        EditorPrefs.SetBool("StixGames.Painter.ShowCloseMessage", false);
                    }
                    else
                    {
                        showTargetSwitchMessage = false;
                        EditorPrefs.SetBool("StixGames.Painter.ShowTargetSwitchMessage", false);
                    }
                }
            }

            //Always accept if message is hidden
            return true;
        }

        private void CreateTexture(string path, TextureTarget target, Color initColor)
        {
            //Create the new texture and save it at the selected path
            var workingTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true);

            Color[] colors = new Color[textureSize * textureSize];

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = initColor;
            }

            workingTex.SetPixels(colors);
            workingTex.Apply();

            var texture = GrassTextureUtility.SaveTextureToFile(path, workingTex, isReadable: true);
            DestroyImmediate(workingTex);

            switch (target)
            {
                case TextureTarget.ColorHeight:
                    grassPainter.ColorHeightTexture = texture;
                    grassMaterial.SetTexture("_ColorMap", texture);
                    break;
                case TextureTarget.Density:
                    grassPainter.DensityTexture = texture;
                    grassMaterial.SetTexture("_DensityTexture", texture);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("target", target, null);
            }
        }

        void DrawSceneGUI(SceneView sceneView)
        {
            BlockSceneSelection();

            UpdateInput();

            // Update grass renderer
            UpdateSelectedRenderer();

            if (grassObject == null)
            {
                return;
            }

            // Calculate ray from mouse cursor
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            // Check if grass was hit
            RaycastHit hit = new RaycastHit();
            if (!grassPainter.RaycastColliders(ray, out hit))
            {
                return;
            }

            Handles.color = new Color(1, 0, 0, 1);
            Handles.CircleHandleCap(0, hit.point, Quaternion.LookRotation(Vector3.up), grassPainter.Size,
                EventType.Repaint);

            // Paint, only if alt is not pressed
            if (mouseDown && !Event.current.alt)
            {
                float newDist = Vector3.Distance(lastPaintPos, hit.point);

                //Check draw spacing
                if (!didDraw || newDist > grassPainter.Spacing * grassPainter.Size)
                {
                    //Draw brush
                    grassPainter.ApplyBrush(hit.point);

                    lastPaintPos = hit.point;
                }

                didDraw = true;
            }

            SceneView.RepaintAll();
        }

        private void BlockSceneSelection()
        {
            //Only block when a grass object is selected
            if (grassObject == null)
            {
                return;
            }

            //Disable selection in editor view, only painting will be accepted as input
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        private void UpdateInput()
        {
            if (grassPainter.CurrentTexture != null && Event.current.type == EventType.MouseDown &&
                Event.current.button == 0)
            {
                mouseDown = true;

                if (useUndo)
                {
                    Undo.RegisterCompleteObjectUndo(grassPainter.CurrentTexture, "Texture paint");
                }
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                mouseDown = false;

                if (didDraw)
                {
                    SaveTexture();
                    didDraw = false;
                }
            }
        }

        private void UpdateSelectedRenderer()
        {
            //Return if no new object was selected
            if (Selection.activeGameObject == null)
            {
                return;
            }

            //Return if the new object is not a grass object
            var newGrassObject = Selection.activeGameObject;
            Selection.activeGameObject = null;

            if (newGrassObject == grassObject)
            {
                return;
            }

            //Get the grass material, if non was found, return.
            var newGrassMaterial = GetGrassMaterial(newGrassObject);
            if (newGrassMaterial == null)
            {
                return;
            }

            //A new object was selected. If another object was selected before, tell the user that this will make changes permanent.
            if (grassObject != null && !ShowUndoLossWarning(false))
            {
                return;
            }

            //Clear undo history for current texture
            Undo.ClearUndo(grassPainter.DensityTexture);

            //Reset the previously selected grass object
            ResetRenderer(false);

            //Assign new grass object
            grassObject = newGrassObject;
            grassMaterial = newGrassMaterial;
            grassPainter.ColorHeightTexture = grassMaterial.GetTexture("_ColorMap") as Texture2D;
            grassPainter.DensityTexture = grassMaterial.GetTexture("_DensityTexture") as Texture2D;

            if (searchRoot)
            {
                grassPainter.GrassColliders = GetGrassObjectsInChildren(grassObject.transform.root.gameObject)
                    .SelectMany(GetColliderOrAddTemp)
                    .ToArray();
            }
            else
            {
                var colliders = GetColliderOrAddTemp(grassObject);

                grassPainter.GrassColliders = colliders;
            }
        }

        private Collider[] GetColliderOrAddTemp(GameObject x)
        {
            var colliders = x.GetComponents<Collider>();

            if (colliders == null || colliders.Length == 0)
            {
                colliders = AddTempCollider(x);
            }

            return colliders;
        }

        private Collider[] AddTempCollider(GameObject obj)
        {
            var meshFilter = obj.GetComponent<NatureMeshFilter>();
            Assert.IsNotNull(meshFilter);

            var colliders = new List<Collider>();
            foreach (var mesh in meshFilter.GetMeshes())
            {
                var c = obj.AddComponent<MeshCollider>();
                c.sharedMesh = mesh;
                tempColliders.Add(c);
                colliders.Add(c);
            }

            return colliders.ToArray();
        }

        private void ResetRenderer(bool reselectPrevious)
        {
            if (reselectPrevious && grassObject != null)
            {
                Selection.activeGameObject = grassObject;
            }

            foreach (var collider in tempColliders)
            {
                DestroyImmediate(collider);
            }

            tempColliders.Clear();

            grassObject = null;
            grassMaterial = null;
            grassPainter.GrassColliders = new Collider[0];
        }

        private GameObject[] GetGrassObjectsInChildren(GameObject newGrassObject)
        {
            var renderers = newGrassObject.GetComponentsInChildren<GrassRenderer>()
                .Where(x => x.Material != null && GrassUtility.IsGrassMaterial(x.Material))
                .Select(x => x.gameObject);

            return renderers.ToArray();
        }

        private Material GetGrassMaterial(GameObject newGrassObject)
        {
            var renderer = newGrassObject.GetComponent<GrassRenderer>();

            if (renderer != null && renderer.Material != null && GrassUtility.IsGrassMaterial(renderer.Material))
            {
                return renderer.Material;
            }

            return null;
        }

        private void SaveTexture()
        {
            if (grassPainter.CurrentTexture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(grassPainter.CurrentTexture);
            File.WriteAllBytes(path, grassPainter.CurrentTexture.EncodeToPNG());
        }
    }
}