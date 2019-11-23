Shader "Stix Games/Grass" 
 {
    Properties 
	{
		_CullMode("CullMode", int) = 0

		[PowerSlider(3.0)] _TargetDensity ("Target Density", Range(0.01,1)) = 0.2
		_DensityFalloffStart  ("Density Falloff Start", Float) = 5
		_DensityFalloffScale ("Density Falloff Scale", Range(0, 3)) = 0.25
		_DensityFalloffPower ("Density Falloff Power", Range(0, 3)) = 1.3

		_LODStart("LOD Start", float) = 20
		_LODEnd("LOD End", float) = 100
		[IntRange] _LODMax("LOD Max", Range(1,6)) = 6

		_GrassFadeStart("Grass Fade Start", float) = 50
		_GrassFadeEnd("Grass Fade End", float) = 100

		_Disorder("Disorder", float) = 0.3

		_GrassFloorColor("Grass Floor Color", Color) = (0.35, 0.35, 0.35, 1)
		_GrassFloorColorTexture("Grass Floor Color Texture", 2D) = "white" {}

		_BurnCutoff("Burn Cutoff", Range(0, 1)) = 0.05

		_TextureCutoff("Texture Cutoff", Range(0, 1)) = 0.1

		_WindParams("Wind WaveStrength(X), WaveSpeed(Y), RippleStrength(Z), RippleSpeed(W)", Vector) = (0.3, 1.2, 0.15, 1.3)
		_WindRotation("Wind Rotation", Range(0, 6.28318530718)) = 0

		_ColorMap("Color Texture (RGB), Height(A)", 2D) = "white" {}
		_Displacement("Displacement Texture (RG)", 2D) = "bump" {}
		_DensityTexture("Grass Density 1(R) 2(G) 3(B) 4(A)", 2D) = "red" {}
		_DensityValues("Grass Density Values", Vector) = (1, 1, 1, 1)

		_GrassTex00	 ("Grass Texture", 2D)		= "white" {}
		_Color00	 ("Color", Color)			= (0.5, 0.7, 0.3, 1)
		_SecColor00	 ("Secondary Color", Color) = (0.2, 0.5, 0.15, 1)
		_SpecColor00 ("Specular Color", Color)	= (0.2, 0.2, 0.2, 1)
		_Smoothness00("Smoothness", Range(0,1)) = 0.5
		_Subsurface00("Subsurface Scattering", Range(0,1)) = 0.3
		_Softness00	 ("Softness", Range(0,1))	= 0.5
		_Width00	 ("Width", float)			= 0.1
		_MinHeight00 ("Min Height", float)		= 0.2
		_MaxHeight00("Max Height", float)		= 1.5
		_TextureAtlasWidth00 ("Texture Atlas Width", int) = 1
		_TextureAtlasHeight00("Texture Atlas Height", int) = 1
		_AtlasOffset00("Padding to prevent color leakage", Range(0, 0.5)) = 0.01

		_GrassTex01	 ("Grass Texture", 2D)		= "white" {}
		_Color01	 ("Color", Color)			= (0.5, 0.7, 0.3, 1)
		_SecColor01	 ("Secondary Color", Color) = (0.2, 0.5, 0.15, 1)
		_SpecColor01 ("Specular Color", Color)	= (0.2, 0.2, 0.2, 1)
		_Smoothness01("Smoothness", Range(0,1)) = 0.5
		_Subsurface01("Subsurface Scattering", Range(0,1)) = 0.3
		_Softness01	 ("Softness", Range(0,1))	= 0.5
		_Width01	 ("Width", float)			= 0.1
		_MinHeight01 ("Min Height", float)		= 0.2
		_MaxHeight01 ("Max Height", float)		= 1.5
		_TextureAtlasWidth01("Texture Atlas Width", int) = 1
		_TextureAtlasHeight01("Texture Atlas Height", int) = 1
		_AtlasOffset01("Padding to prevent color leakage", Range(0, 0.5)) = 0.01

		_GrassTex02	 ("Grass Texture", 2D)		= "white" {}
		_Color02	 ("Color", Color)			= (0.5, 0.7, 0.3, 1)
		_SecColor02	 ("Secondary Color", Color) = (0.2, 0.5, 0.15, 1)
		_SpecColor02 ("Specular Color", Color)	= (0.2, 0.2, 0.2, 1)
		_Smoothness02("Smoothness", Range(0,1)) = 0.5
		_Subsurface02("Subsurface Scattering", Range(0,1)) = 0.3
		_Softness02	 ("Softness", Range(0,1))	= 0.5
		_Width02	 ("Width", float)			= 0.1
		_MinHeight02 ("Min Height", float)		= 0.2
		_MaxHeight02 ("Max Height", float)		= 1.5
		_TextureAtlasWidth02("Texture Atlas Width", int) = 1
		_TextureAtlasHeight02("Texture Atlas Height", int) = 1
		_AtlasOffset02("Padding to prevent color leakage", Range(0, 0.5)) = 0.01

		_GrassTex03	 ("Grass Texture", 2D)		= "white" {}
		_Color03	 ("Color", Color)			= (0.5, 0.7, 0.3, 1)
		_SecColor03	 ("Secondary Color", Color) = (0.2, 0.5, 0.15, 1)
		_SpecColor03 ("Specular Color", Color)	= (0.2, 0.2, 0.2, 1)
		_Smoothness03("Smoothness", Range(0,1)) = 0.5
		_Subsurface03("Subsurface Scattering", Range(0,1)) = 0.3
		_Softness03	 ("Softness", Range(0,1))	= 0.5
		_Width03	 ("Width", float)			= 0.1
		_MinHeight03 ("Min Height", float)		= 0.2
		_MaxHeight03 ("Max Height", float)		= 1.5
		_TextureAtlasWidth03("Texture Atlas Width", int) = 1
		_TextureAtlasHeight03("Texture Atlas Height", int) = 1
		_AtlasOffset03("Padding to prevent color leakage", Range(0, 0.5)) = 0.01
    }

	HLSLINCLUDE
	#pragma target 4.6

	#pragma vertex vert
	#pragma hull hullShader
	#pragma domain domainShader
	#pragma geometry geom
	#pragma fragment frag
	ENDHLSL

	SubShader
	{
	    // Built-in Render Pipeline
	    
		Tags{ "Queue" = "AlphaTest" "RenderType" = "StixGamesGrass" }
		LOD 1000

		Pass 
		{
			Name "FORWARD"
			Tags {"LightMode" = "ForwardBase"}
			ColorMask RGB
			AlphaToMask On
			Cull [_CullMode]

			HLSLPROGRAM
			#define GRASS_PASS_FORWARDBASE
			
			#pragma multi_compile_fog
			#pragma multi_compile_fwdbase

			// ================= Shader_feature block start =================
			#pragma shader_feature SIMPLE_GRASS ONE_GRASS_TYPE TWO_GRASS_TYPES THREE_GRASS_TYPES FOUR_GRASS_TYPES
			#pragma shader_feature __ GRASS_UNLIT_LIGHTING GRASS_UNSHADED_LIGHTING GRASS_PBR_LIGHTING
			#pragma shader_feature __ GRASS_HYBRID_NORMAL_LIGHTING GRASS_SURFACE_NORMAL_LIGHTING
			#pragma shader_feature __ UNIFORM_DENSITY VERTEX_DENSITY
			#pragma shader_feature __ GRASS_HEIGHT_SMOOTHING
			#pragma shader_feature __ GRASS_WIDTH_SMOOTHING
			#pragma shader_feature __ GRASS_ALPHA_SMOOTHING
			#pragma shader_feature __ GRASS_OBJECT_MODE
			#pragma shader_feature __ GRASS_TOP_VIEW_COMPENSATION
			#pragma shader_feature __ GRASS_FOLLOW_SURFACE_NORMAL
			#pragma shader_feature __ GRASS_USE_TEXTURE_ATLAS
			#pragma shader_feature __ GRASS_IGNORE_GI_SPECULAR
			#pragma shader_feature __ GRASS_RANDOM_DIR
			#pragma shader_feature __ GRASS_CALC_GLOBAL_WIND
			#pragma multi_compile  __ GRASS_RENDERTEXTURE_INTERACTION
			// ================= Shader_feature block end  =================
			
			#include "GrassIncludes.hlsl"

			ENDHLSL
		}

		Pass 
		{
			Name "FORWARD"
			Tags { "LightMode" = "ForwardAdd" }
			ZWrite Off Blend One One
			ColorMask RGB
			AlphaToMask On
			Cull [_CullMode]

			HLSLPROGRAM
			#define GRASS_PASS_FORWARDADD

			#pragma multi_compile_fog
			#pragma multi_compile_fwdadd_fullshadows

			// ================= Shader_feature block start =================
			#pragma shader_feature SIMPLE_GRASS ONE_GRASS_TYPE TWO_GRASS_TYPES THREE_GRASS_TYPES FOUR_GRASS_TYPES
			#pragma shader_feature __ GRASS_UNLIT_LIGHTING GRASS_UNSHADED_LIGHTING GRASS_PBR_LIGHTING
			#pragma shader_feature __ GRASS_HYBRID_NORMAL_LIGHTING GRASS_SURFACE_NORMAL_LIGHTING
			#pragma shader_feature __ UNIFORM_DENSITY VERTEX_DENSITY
			#pragma shader_feature __ GRASS_HEIGHT_SMOOTHING
			#pragma shader_feature __ GRASS_WIDTH_SMOOTHING
			#pragma shader_feature __ GRASS_ALPHA_SMOOTHING
			#pragma shader_feature __ GRASS_OBJECT_MODE
			#pragma shader_feature __ GRASS_TOP_VIEW_COMPENSATION
			#pragma shader_feature __ GRASS_FOLLOW_SURFACE_NORMAL
			#pragma shader_feature __ GRASS_USE_TEXTURE_ATLAS
			#pragma shader_feature __ GRASS_IGNORE_GI_SPECULAR
			#pragma shader_feature __ GRASS_RANDOM_DIR
			#pragma shader_feature __ GRASS_CALC_GLOBAL_WIND
			#pragma multi_compile  __ GRASS_RENDERTEXTURE_INTERACTION
			// ================= Shader_feature block end  =================

			#include "GrassIncludes.hlsl"

			ENDHLSL
		}

		//Deferred Rendering pass
		Pass
		{
			//Regular unity deferred rendering
			Name "DEFERRED"
			Tags { "LightMode" = "Deferred" }
			Cull[_CullMode]

			HLSLPROGRAM
			#define GRASS_PASS_DEFERRED

			#pragma exclude_renderers nomrt

			// ================= Shader_feature block start =================
			#pragma shader_feature SIMPLE_GRASS ONE_GRASS_TYPE TWO_GRASS_TYPES THREE_GRASS_TYPES FOUR_GRASS_TYPES
			//Lighting features are ignored in deferred, as the lighting is defined by the lighting shader
			//#pragma shader_feature __ GRASS_UNLIT_LIGHTING GRASS_UNSHADED_LIGHTING GRASS_PBR_LIGHTING
			//#pragma shader_feature __ GRASS_HYBRID_NORMAL_LIGHTING GRASS_SURFACE_NORMAL_LIGHTING
			#define GRASS_PBR_LIGHTING
			#pragma shader_feature __ UNIFORM_DENSITY VERTEX_DENSITY
			#pragma shader_feature __ GRASS_HEIGHT_SMOOTHING
			#pragma shader_feature __ GRASS_WIDTH_SMOOTHING
			#pragma shader_feature __ GRASS_ALPHA_SMOOTHING
			#pragma shader_feature __ GRASS_OBJECT_MODE
			#pragma shader_feature __ GRASS_TOP_VIEW_COMPENSATION
			#pragma shader_feature __ GRASS_FOLLOW_SURFACE_NORMAL
			#pragma shader_feature __ GRASS_USE_TEXTURE_ATLAS
			//#pragma shader_feature __ GRASS_IGNORE_GI_SPECULAR
			//Only random dir grass is supported in deferred
			//#pragma shader_feature __ GRASS_RANDOM_DIR
			#define GRASS_RANDOM_DIR
			#pragma shader_feature __ GRASS_CALC_GLOBAL_WIND
			#pragma multi_compile  __ GRASS_RENDERTEXTURE_INTERACTION
			// ================= Shader_feature block end  =================

			#include "GrassIncludes.hlsl"

			ENDHLSL
		}
		//End of Deferred Rendering

		Pass 
		{
			Name "ShadowCaster"
			Tags {"LightMode" = "ShadowCaster" }
			ZWrite On ZTest LEqual
			Cull [_CullMode]
			Offset 1, 0

			ColorMask 0

			HLSLPROGRAM
			#define GRASS_PASS_SHADOWCASTER

			#pragma multi_compile_shadowcaster

			// ================= Shader_feature block start =================
			#pragma shader_feature SIMPLE_GRASS ONE_GRASS_TYPE TWO_GRASS_TYPES THREE_GRASS_TYPES FOUR_GRASS_TYPES
			#pragma shader_feature __ GRASS_UNLIT_LIGHTING GRASS_UNSHADED_LIGHTING GRASS_PBR_LIGHTING
			#pragma shader_feature __ GRASS_HYBRID_NORMAL_LIGHTING GRASS_SURFACE_NORMAL_LIGHTING
			#pragma shader_feature __ UNIFORM_DENSITY VERTEX_DENSITY
			#pragma shader_feature __ GRASS_HEIGHT_SMOOTHING
			#pragma shader_feature __ GRASS_WIDTH_SMOOTHING
			#pragma shader_feature __ GRASS_ALPHA_SMOOTHING
			#pragma shader_feature __ GRASS_OBJECT_MODE
			#pragma shader_feature __ GRASS_TOP_VIEW_COMPENSATION
			#pragma shader_feature __ GRASS_FOLLOW_SURFACE_NORMAL
			#pragma shader_feature __ GRASS_USE_TEXTURE_ATLAS
			#pragma shader_feature __ GRASS_IGNORE_GI_SPECULAR
			#pragma shader_feature __ GRASS_RANDOM_DIR
			#pragma shader_feature __ GRASS_CALC_GLOBAL_WIND
			#pragma multi_compile  __ GRASS_RENDERTEXTURE_INTERACTION
			// ================= Shader_feature block end  =================

			#include "GrassIncludes.hlsl"

			ENDHLSL
		}
	}

	CustomEditor "GrassEditor"
}