#ifndef GRASS_INCLUDES
#define GRASS_INCLUDES

#include "UnityCG.cginc"

#include "AutoLight.cginc"

#if !defined(SHADOW_COORDS)
#define SHADOW_COORDS(idx1) unityShadowCoord4 _ShadowCoord : TEXCOORD##idx1;
#endif

#include "Tessellation.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityShaderVariables.cginc"
#include "UnityStandardCore.cginc"

#include "GrassConfig.hlsl"
#include "GrassDefinitionsAndFunctions.hlsl"

#include "GrassVertex.hlsl"
#include "GrassTessellation.hlsl"
#include "GrassGeom.hlsl"
#include "GrassSurface.hlsl"
#include "GrassLighting.hlsl"
#include "GrassFrag.hlsl"

#endif