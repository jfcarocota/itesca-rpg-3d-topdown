using System;
using System.Collections.Generic;
using System.Linq;
using StixGames.GrassShader;
using StixGames.NatureCore.Utility;
using StixGames.NatureCore.Utility.Localization;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
public class GrassEditor : MaterialEditor
{
    // Shader variants
    private static readonly string[] grassTypeLabels =
    {
        "Grass.GrassType.Simple", "Grass.GrassType.OneTex", "Grass.GrassType.TwoTex", "Grass.GrassType.ThreeTex",
        "Grass.GrassType.FourTex"
    };

    private static readonly string[] grassTypeString =
    {
        "SIMPLE_GRASS", "ONE_GRASS_TYPE", "TWO_GRASS_TYPES", "THREE_GRASS_TYPES", "FOUR_GRASS_TYPES"
    };

    private static readonly string[] densityModeLabels =
        {"Grass.DensityMode.Value", "Grass.DensityMode.Vertex", "Grass.DensityMode.Texture"};

    private static readonly string[] lightingModes =
        {"GRASS_UNLIT_LIGHTING", "GRASS_UNSHADED_LIGHTING", "", "GRASS_PBR_LIGHTING"};

    private static readonly string[] lightingModeLabels =
    {
        "Grass.LightingMode.Unlit", "Grass.LightingMode.Unshaded",
        "Grass.LightingMode.InvertedSpecularPBR", "Grass.LightingMode.DefaultPBR"
    };

    private static readonly string[] lightingNormalModes =
    {
        "", "GRASS_HYBRID_NORMAL_LIGHTING", "GRASS_SURFACE_NORMAL_LIGHTING",
    };

    private static readonly string[] lightingNormalLabels =
    {
        "Grass.LightingNormal.Regular", "Grass.LightingNormal.Hybrid", "Grass.LightingNormal.Surface"
    };

    // Defaults
    private static readonly string[] defaultKeywords =
        {"SIMPLE_GRASS", "GRASS_HEIGHT_SMOOTHING", "GRASS_CALC_GLOBAL_WIND", "GRASS_IGNORE_GI_SPECULAR"};

    private const int defaultGrassType = 0;

    private const int defaultLightingMode = 2;

    private const int defaultLightingNormalMode = 0;

    public override void OnInspectorGUI()
    {
        Profiler.BeginSample("GrassEditor.OnInspectorGUI");

        LocalizationManager.LoadLocalizedText(
            FileAnchor.GetFilePath("GrassShaderDataAnchor", "GrassShaderStrings.json"));

        if (!isVisible)
        {
            return;
        }

        bool forceUpdate = false;
        Material targetMat = target as Material;
        string[] originalKeywords = targetMat.shaderKeywords;

        //Set default values when the material is newly created
        if (originalKeywords.Length == 0)
        {
            originalKeywords = defaultKeywords;

            targetMat.shaderKeywords = defaultKeywords;
            EditorUtility.SetDirty(targetMat);
        }

        //Grass type
        int grassType = -1;
        for (int i = 0; i < grassTypeLabels.Length; i++)
        {
            if (originalKeywords.Contains(grassTypeString[i]))
            {
                grassType = i;
                break;
            }
        }

        if (grassType < 0)
        {
            grassType = defaultGrassType;
            forceUpdate = true;
        }

        //Lighting mode
        int lightingMode = -1;
        for (int i = 0; i < lightingModeLabels.Length; i++)
        {
            if (originalKeywords.Contains(lightingModes[i]))
            {
                lightingMode = i;
                break;
            }
        }

        if (lightingMode < 0)
        {
            lightingMode = defaultLightingMode;
            forceUpdate = true;
        }

        int lightingNormalMode = -1;
        for (int i = 0; i < lightingNormalLabels.Length; i++)
        {
            if (originalKeywords.Contains(lightingNormalModes[i]))
            {
                lightingNormalMode = i;
                break;
            }
        }

        if (lightingNormalMode < 0)
        {
            lightingNormalMode = defaultLightingNormalMode;
            forceUpdate = true;
        }

        //Density mode
        var densityMode = GrassUtility.GetDensityMode(targetMat);

        bool deferredMode = Camera.main.renderingPath == RenderingPath.DeferredLighting ||
                            Camera.main.renderingPath == RenderingPath.DeferredShading;

        if (Camera.main.renderingPath == RenderingPath.UsePlayerSettings)
        {
            var renderingPath = EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.Standalone, GraphicsTier.Tier3)
                .renderingPath;

            if (renderingPath == RenderingPath.DeferredLighting || renderingPath == RenderingPath.DeferredShading)
            {
                deferredMode = true;
            }
        }

        //HDRP Warning
        if (GrassUtility.IsHDRPGrassMaterial(targetMat))
        {
            GUILayout.Label(new GUIContent("The High Definition Render Pipeline is not yet supported.\n" +
                                           "It is still a very undocumented feature, so until Unity releases a guide or at least\n" +
                                           "some documentation for custom shader support, it would be extremely difficult to\n" +
                                           "add support, which might break at each new version of the HDRP."));
        }

        //Force forward mode
        if (GrassUtility.IsForwardOnlyGrassMaterial(targetMat))
        {
            GUILayout.Label(new GUIContent("This version of the shader is equivalent to the regular\n" +
                                           "Grass shader, except that it disables deferred rendering.\n" +
                                           "Use it if you require the additional lighting options.\n" +
                                           "If your grass is affected by many lights, you should consider\n" +
                                           "using the deferred version instead."));

            deferredMode = false;
        }

        bool ignoreGISpecular = originalKeywords.Contains("GRASS_IGNORE_GI_SPECULAR");
        bool widthSmoothing = originalKeywords.Contains("GRASS_WIDTH_SMOOTHING");
        bool heightSmoothing = originalKeywords.Contains("GRASS_HEIGHT_SMOOTHING");
        bool alphaSmoothing = originalKeywords.Contains("GRASS_ALPHA_SMOOTHING");
        bool objectMode = originalKeywords.Contains("GRASS_OBJECT_MODE");
        bool topViewCompensation = originalKeywords.Contains("GRASS_TOP_VIEW_COMPENSATION");
        bool surfaceNormal = originalKeywords.Contains("GRASS_FOLLOW_SURFACE_NORMAL");
        bool useTextureAtlas = originalKeywords.Contains("GRASS_USE_TEXTURE_ATLAS");
        bool grassRandomDirection = deferredMode || originalKeywords.Contains("GRASS_RANDOM_DIR");
        bool calcGlobalWind = originalKeywords.Contains("GRASS_CALC_GLOBAL_WIND");

        //Grass painter
        if (Selection.activeObject == null || AssetDatabase.Contains(Selection.activeObject))
        {
            EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.HowToOpenGrassPainter"));
        }
        else
        {
            if (GUILayout.Button(LocalizationManager.GetGUIContent("Grass.OpenGrassPainter")))
            {
                GrassPainterWindow.OpenWindow();
            }
        }

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        // Shader variants
        EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.ShaderVariantsHeading"),
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.GrassModesHeading"),
            EditorStyles.boldLabel);
        grassType = EditorGUILayout.Popup(LocalizationManager.GetGUIContent("Grass.GrassType.Label"), grassType,
            LocalizationManager.GetGUIContents(grassTypeLabels));
        densityMode = (DensityMode) EditorGUILayout.Popup(LocalizationManager.GetGUIContent("Grass.DensityMode.Label"),
            (int) densityMode, LocalizationManager.GetGUIContents(densityModeLabels));
        using (new EditorGUI.DisabledGroupScope(deferredMode))
        {
            grassRandomDirection = GUILayout.Toggle(grassRandomDirection,
                LocalizationManager.GetGUIContent("Grass.RandomGrass"));
        }

        EditorGUILayout.Separator();

        // Lighting modes
        using (new EditorGUI.DisabledGroupScope(deferredMode))
        {
            EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.LightingHeading"),
                EditorStyles.boldLabel);
            lightingMode = EditorGUILayout.Popup(LocalizationManager.GetGUIContent("Grass.LightingMode.Label"),
                lightingMode, LocalizationManager.GetGUIContents(lightingModeLabels));

            ignoreGISpecular = GUILayout.Toggle(ignoreGISpecular,
                LocalizationManager.GetGUIContent("Grass.IgnoreSpecularGI"));
            EditorGUILayout.Separator();
        }

        lightingNormalMode = EditorGUILayout.Popup(LocalizationManager.GetGUIContent("Grass.LightingNormal.Label"),
            lightingNormalMode, LocalizationManager.GetGUIContents(lightingNormalLabels));

        EditorGUILayout.Separator();

        // Tessellation smoothing
        EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.LODSmoothingHeading"),
            EditorStyles.boldLabel);
        widthSmoothing = GUILayout.Toggle(widthSmoothing, LocalizationManager.GetGUIContent("Grass.SmoothWidth"));
        heightSmoothing = GUILayout.Toggle(heightSmoothing, LocalizationManager.GetGUIContent("Grass.SmoothHeight"));
        using (new EditorGUI.DisabledGroupScope(IsSimpleGrass(grassType)))
        {
            alphaSmoothing = GUILayout.Toggle(alphaSmoothing, LocalizationManager.GetGUIContent("Grass.SmoothAlpha"));
        }

        EditorGUILayout.Separator();

        EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.ObjectWorldSpaceHeading"),
            EditorStyles.boldLabel);
        objectMode = GUILayout.Toggle(objectMode, LocalizationManager.GetGUIContent("Grass.ObjectSpace"));
        surfaceNormal = GUILayout.Toggle(surfaceNormal, LocalizationManager.GetGUIContent("Grass.FollowSurfaceNormal"));
        EditorGUILayout.Separator();

        EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.OtherVariantsHeading"),
            EditorStyles.boldLabel);
        topViewCompensation = GUILayout.Toggle(topViewCompensation,
            LocalizationManager.GetGUIContent("Grass.ImproveViewingFromAbove"));
        calcGlobalWind = GUILayout.Toggle(calcGlobalWind, LocalizationManager.GetGUIContent("Grass.GlobalWind"));
        EditorGUILayout.Separator();

        using (new EditorGUI.DisabledGroupScope(grassType == 0))
        {
            useTextureAtlas =
                GUILayout.Toggle(useTextureAtlas, LocalizationManager.GetGUIContent("Grass.TextureAtlas"));
        }

        EditorGUILayout.Separator();

        if (EditorGUI.EndChangeCheck() || forceUpdate)
        {
            Undo.RecordObject(targetMat, "Changed grass shader keywords");

            var keywords = new List<string>();

            keywords.Add(grassTypeString[grassType]);
            keywords.Add(lightingModes[lightingMode]);
            keywords.Add(lightingNormalModes[lightingNormalMode]);
            keywords.Add(GrassUtility.DensityModes[(int) densityMode]);

            if (ignoreGISpecular)
            {
                keywords.Add("GRASS_IGNORE_GI_SPECULAR");
            }

            if (widthSmoothing)
            {
                keywords.Add("GRASS_WIDTH_SMOOTHING");
            }

            if (heightSmoothing)
            {
                keywords.Add("GRASS_HEIGHT_SMOOTHING");
            }

            if (alphaSmoothing)
            {
                keywords.Add("GRASS_ALPHA_SMOOTHING");
            }

            if (objectMode)
            {
                keywords.Add("GRASS_OBJECT_MODE");
            }

            if (topViewCompensation)
            {
                keywords.Add("GRASS_TOP_VIEW_COMPENSATION");
            }

            if (surfaceNormal)
            {
                keywords.Add("GRASS_FOLLOW_SURFACE_NORMAL");
            }

            if (useTextureAtlas)
            {
                keywords.Add("GRASS_USE_TEXTURE_ATLAS");
            }

            if (grassRandomDirection)
            {
                keywords.Add("GRASS_RANDOM_DIR");
            }

            if (calcGlobalWind)
            {
                keywords.Add("GRASS_CALC_GLOBAL_WIND");
            }

            targetMat.shaderKeywords = keywords.ToArray();
            EditorUtility.SetDirty(targetMat);
        }

        EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.ShaderSettingsHeading"),
            EditorStyles.boldLabel);
        serializedObject.Update();
        var theShader = serializedObject.FindProperty("m_Shader");
        if (isVisible && !theShader.hasMultipleDifferentValues && theShader.objectReferenceValue != null)
        {
            EditorGUIUtility.fieldWidth = 64;
            EditorGUI.BeginChangeCheck();

            int grassLabel = 1;
            var properties = GetMaterialProperties(new Object[] {targetMat});
            foreach (var property in properties)
            {
                //Ignore unused grass types
                if ((grassType < 2 && property.name.Contains("01")) ||
                    (grassType < 3 && property.name.Contains("02")) ||
                    (grassType < 4 && property.name.Contains("03")))
                {
                    DisableFollowingProperties();
                }

                // =========== Labels ==================
                if (property.name == "_TargetDensity")
                {
                    EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.PerformanceHeading"),
                        EditorStyles.boldLabel);
                }

                if (property.name == "_Disorder")
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.VisualsHeading"),
                        EditorStyles.boldLabel);
                }

                if (property.name == "_ColorMap")
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.BaseTexturesHeading"),
                        EditorStyles.boldLabel);
                }

                if (property.name == "_WindParams")
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(LocalizationManager.GetGUIContent("Grass.WindSettingsHeading"),
                        EditorStyles.boldLabel);
                }

                //Grass type label
                if (property.name.Contains("_GrassTex"))
                {
                    EditorGUILayout.Space();
                    var localizedText =
                        LocalizationManager.GetLocalizedValue("Grass.GrassTypeNrHeading.Text", "Grass Type Nr. {0}");
                    var localizedTooltip = LocalizationManager.GetLocalizedValue("Grass.GrassTypeNrHeading.Tooltip",
                        LocalizationManager.MissingTooltipMessage);
                    EditorGUILayout.LabelField(
                        new GUIContent(string.Format(localizedText, grassLabel), localizedTooltip),
                        EditorStyles.boldLabel);
                    grassLabel++;
                }

                // ============ Non standard settings =============
                if (property.name == "_CullMode")
                {
                    //Cull Mode is only set by the editor, don't show it in the inspector
                    property.floatValue = grassRandomDirection ? 2 : 0;
                    continue;
                }

                //Hide texture atlas settings if not active
                if (property.name.Contains("_TextureAtlas"))
                {
                    using (new EditorGUI.DisabledScope(!useTextureAtlas))
                    {
                        property.floatValue =
                            Math.Max(EditorGUILayout.IntField(GetGUIContent(property), (int) property.floatValue), 1);
                    }

                    continue;
                }

                //Wind should only be active when activated
                if (property.name.StartsWith("_Wind"))
                {
                    using (new EditorGUI.DisabledScope(!calcGlobalWind))
                    {
                        ShaderProperty(property, GetGUIContent(property));
                    }

                    continue;
                }

                // ============ Standard settings =============
                //Density mode
                if (property.name == "_DensityValues" || property.name == "_DensityTexture")
                {
                    //Vertex density
                    if (densityMode == DensityMode.Vertex)
                    {
                        DisableSingleProperty();
                    }

                    //Texture density
                    if (densityMode == DensityMode.Texture && property.name == "_DensityValues")
                    {
                        DisableSingleProperty();
                    }

                    //Density value
                    if (densityMode == DensityMode.Value && property.name == "_DensityTexture")
                    {
                        DisableSingleProperty();
                    }
                }

                //In simple grass, ignore grass texture
                if (IsSimpleGrass(grassType) && (property.name.Contains("_GrassTex") || property.name.StartsWith("_AtlasOffset")))
                {
                    DisableSingleProperty();
                }

                //For billboard grass (not randomized direction grass) the subsurface scattering setting is not in use (yet?)
                if (!grassRandomDirection && property.name.Contains("Subsurface"))
                {
                    DisableSingleProperty();
                }

                ShaderProperty(property, GetGUIContent(property));

                EnableSingleProperty();
            }

            EnableFollowingProperties();

            if (EditorGUI.EndChangeCheck())
            {
                PropertiesChanged();
            }
        }

        Profiler.EndSample();
    }

    private bool disableSingle = false;
    private bool disableFollowing = false;

    private void DisableSingleProperty()
    {
        if (disableSingle || disableFollowing)
        {
            return;
        }

        disableSingle = true;
        EditorGUI.BeginDisabledGroup(true);
    }

    private void DisableFollowingProperties()
    {
        if (disableFollowing)
        {
            return;
        }

        if (!disableSingle)
        {
            EditorGUI.BeginDisabledGroup(true);
        }

        disableSingle = false;
        disableFollowing = true;
    }

    private void EnableSingleProperty()
    {
        if (disableSingle)
        {
            disableSingle = false;
            EditorGUI.EndDisabledGroup();
        }
    }

    private void EnableFollowingProperties()
    {
        if (disableFollowing)
        {
            disableFollowing = false;
            EditorGUI.EndDisabledGroup();
        }
    }

    private static GUIContent GetGUIContent(MaterialProperty property)
    {
        return LocalizationManager.GetGUIContent(string.Format("Grass.{0}", property.name), property.displayName,
            "No tooltip found.");
    }

    private static bool IsSimpleGrass(int grassType)
    {
        return grassType == 0;
    }
}