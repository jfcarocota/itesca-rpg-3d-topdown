// Upgrade NOTE: upgraded instancing buffer 'InstancedProps' to new syntax.

Shader "Stix Games/GrassFallback" 
{
	Properties 
	{
		_TextureAtlas ("Albedo (RGB)", 2D) = "white" {}
		_SpecularSmooth ("Specular (RGB) Smoothness (A)", 2D) = "black" {}
		_Depth ("Depth", 2D) = "white" {}
		_Normal ("Normal", 2D) = "bump" {}
		_Subsurface("Subsurface Scattering", Range(0, 1)) = 0.0
		_WindParams("Wind WaveStrength(X), WaveSpeed(Y), RippleStrength(Z), RippleSpeed(W)", Vector) = (0.3, 1.2, 0.15, 1.3)
		_WindRotation("Wind Rotation", Range(0, 6.28318530718)) = 0
		_SoftnessFactor("SoftnessFactor", Float) = 1
		_Cutoff("Cutoff", Range(0, 1)) = 0.2
		_AtlasSize("Texture Atlas Size", Int) = 0
		_AtlasOffset("Padding to prevent color leakage", Range(0, 0.5)) = 0.01
		_InstanceAtlasIndices("Atlas Index for Instanced Rendering", Vector) = (0,0,0,0)
		_InstancePosSmoothing("Center Position and Smoothing for Instanced Rendering", Vector) = (0,0,0,0)

		_WindWobble("Wind Wobble", Range(0, 2)) = 0.5

		_FinalAlphaCutoff("Internal alpha cutoff", Float) = 0.5
	}
	SubShader 
	{
		Tags { "Queue"="AlphaTest" }
		//LOD 200
		
		//This setting isn't allowed, as it removes shadow casting in surface shadows
		//AlphaToMask On
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf FallbackGrass fullforwardshadows vertex:vert addshadow //alphatest:_FinalAlphaCutoff

		#pragma multi_compile_instancing

		#pragma multi_compile __ SMOOTH_TEXTURE
		#pragma multi_compile __ SMOOTH_WIDTH
		#pragma multi_compile __ SMOOTH_HEIGHT
		
		#pragma multi_compile  __ GRASS_RENDERTEXTURE_INTERACTION

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		#include "UnityPBSLighting.cginc"

		sampler2D _TextureAtlas;
		sampler2D _SpecularSmooth;
		sampler2D _Depth;
		sampler2D _Normal;
		fixed _Subsurface;

		fixed4 _WindParams;
		fixed _WindRotation;

		fixed _Cutoff;
		fixed _SoftnessFactor;
		half _WindWobble;

		float _AtlasSize;
		float _AtlasOffset;

		#ifdef GRASS_RENDERTEXTURE_INTERACTION
			sampler2D _GrassRenderTextureInteraction;
			sampler2D _GrassRenderTextureBurn;

			//This is the area the interaction camera is currently rendering. xy is left bottom, wz is width and height.
			float4 _GrassRenderTextureArea;
		#endif

		UNITY_INSTANCING_BUFFER_START(InstancedProps)
			UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceAtlasIndices)	
#define _InstanceAtlasIndices_arr InstancedProps
			UNITY_DEFINE_INSTANCED_PROP(float4, _InstancePosSmoothing)	
#define _InstancePosSmoothing_arr InstancedProps
		UNITY_INSTANCING_BUFFER_END(InstancedProps)

		#include "GrassWind.hlsl"

		struct vert_inout
		{
			float4 vertex    : POSITION;  // The vertex position in model space.
			float3 normal    : NORMAL;    // The vertex normal in model space.
			float4 texcoord  : TEXCOORD0; // The first UV coordinate.
			float2 texcoord1  : TEXCOORD1; // The second UV coordinate.
			float2 texcoord2  : TEXCOORD2; // The third UV coordinate.
			float4 tangent   : TANGENT;   // The tangent vector in Model Space (used for normal mapping).
			float4 color  : COLOR;     // World position (XZ) Grass Height (Y) Smoothing (A)
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
			#define GRASS_USE_INSTANCING
		#endif

		inline half4 LightingFallbackGrass(SurfaceOutputStandardSpecular s, half3 viewDir, UnityGI gi)
		{
			half4 c = LightingStandardSpecular(s, viewDir, gi);
			
			half oneMinusReflectivity;
			half3 diffColor = EnergyConservationBetweenDiffuseAndSpecular(s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);
			half3 subsurfaceScattering = diffColor * _Subsurface * (saturate(1 - LambertTerm(s.Normal, gi.light.dir)) * gi.light.color);
			c.rgb += subsurfaceScattering;

			return c;
		}

		inline void LightingFallbackGrass_GI(SurfaceOutputStandardSpecular s, UnityGIInput data, inout UnityGI gi)
		{
			//Regular GI
			LightingStandardSpecular_GI(s, data, gi);

			//Subsurface GI
			s.Normal = -s.Normal;
			UnityGI subsurfGi = (UnityGI) 0;
			LightingStandardSpecular_GI(s, data, subsurfGi);

			half oneMinusReflectivity;
			half3 diffColor = EnergyConservationBetweenDiffuseAndSpecular(s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);
			gi.indirect.diffuse += diffColor * _Subsurface * subsurfGi.indirect.diffuse;

			s.Normal = -s.Normal;
		}

		struct Input 
		{
			float2 uv_TextureAtlas;

			//I am misusing the UV coordinates to contain the indices for each grass type
			float2 uv2_SpecularSmooth;
			float2 uv3_Depth;

			//The fallback script saves the world position (unchanged by smoothing) in the vertex color
			float4 color : COLOR;

			float3 burnColor;
		};

		void vert (inout vert_inout v, out Input o) 
		{
			UNITY_SETUP_INSTANCE_ID(v);

			UNITY_INITIALIZE_OUTPUT(Input,o);

			//Calculate smoothing for instancing
			#if defined(GRASS_USE_INSTANCING)
				float3 originalPos = UNITY_ACCESS_INSTANCED_PROP(_InstancePosSmoothing_arr, _InstancePosSmoothing).xyz;
				originalPos.y = v.color.y;
				float smoothing = UNITY_ACCESS_INSTANCED_PROP(_InstancePosSmoothing_arr, _InstancePosSmoothing).w;
				#if defined(SMOOTH_HEIGHT)
					v.vertex.y *= smoothing;
				#endif
				#if defined(SMOOTH_WIDTH)
					v.vertex.xz *= smoothing;
				#endif
			#else
				float3 originalPos = v.color.xyz;
			#endif

			//Calculate interaction
			#if defined(GRASS_RENDERTEXTURE_INTERACTION)
				float2 coords = (originalPos.xz - _GrassRenderTextureArea.xy) / _GrassRenderTextureArea.zw;
				fixed4 interactionTexture = tex2Dlod(_GrassRenderTextureInteraction, float4(coords, 0, 0));
				fixed3 interaction = normalize(interactionTexture.rgb * 2 - float3(1, 1, 1));
				fixed4 burnFactor = tex2Dlod(_GrassRenderTextureBurn, float4(coords, 0, 0));
			#else
				fixed3 interaction = fixed3(0, 0, 1);
				fixed4 burnFactor = fixed4(1, 1, 1, 1);
			#endif

			//Apply burn
			o.burnColor = burnFactor.rgb;
			v.vertex.y *= burnFactor.a;

			//Calculations for wind and interaction
			half2 windDir = wind(originalPos, _WindRotation);
			half segment = originalPos.y;
			fixed sqrSegment = segment * segment;

			//Wind wobble
			#if defined(GRASS_USE_INSTANCING)
				float2 offset = float2(0,0);
			#else
				float2 offset = mul(unity_WorldToObject, float4(originalPos, 1)).xz;
			#endif
			v.vertex.xz -= offset;
			#if defined(GRASS_RENDERTEXTURE_INTERACTION)
				float interactionHeight = pow(interaction.z, 2);

				v.vertex.xz *= 1 + segment * length(windDir.xy) * interactionHeight * _WindWobble;
			#else
				v.vertex.xz *= 1 + segment * length(windDir.xy) * _WindWobble;
			#endif
			v.vertex.xz += offset;

			float3 pos = mul(unity_ObjectToWorld, v.vertex).xyz;

			//Apply interaction
			#if defined(GRASS_RENDERTEXTURE_INTERACTION)
				pos.xz += (windDir.xy * interactionHeight + interaction.xy) * sqrSegment * _SoftnessFactor;
				pos.y -= length(windDir.xy + interaction.xy) * sqrSegment * 0.5f * _SoftnessFactor;
				pos.y *= max(interactionHeight, 0.01);
			#else
				pos.xz += windDir.xy * sqrSegment * _SoftnessFactor;
				pos.y  -= length(windDir) * sqrSegment * 0.5f * _SoftnessFactor;
			#endif

			//Use original grass height, to prevent vertices at the floor from moving
			v.vertex.xyz = mul(unity_WorldToObject, float4(pos, 1)).xyz;

			//Discard the triangle if it's burned away
			if (burnFactor.a < 0.1f)
			{
				v.vertex = float4(0, 0, 0, 0);
			}
		}

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) 
		{
			_AtlasSize = max(_AtlasSize, 1);

			float size = 1.0 / _AtlasSize;
			float tilesPerGrassType = round(_AtlasSize * (_AtlasSize / 4));

			//Apply the color leakage preventing _AtlasOffset
			float2 offset = float2(_AtlasOffset, _AtlasOffset);
			float sizeOffset = 1 - 2 * _AtlasOffset;

			#if defined(GRASS_USE_INSTANCING)
				float4 indices = UNITY_ACCESS_INSTANCED_PROP(_InstanceAtlasIndices_arr, _InstanceAtlasIndices);
			#else
				float4 indices = float4(IN.uv2_SpecularSmooth, IN.uv3_Depth);
			#endif

			//Calculate texture coordinates for each grass type
			float index0 = round(indices.x);
			float2 texCoord0 = (offset + float2(fmod(index0, _AtlasSize), floor(index0 / _AtlasSize)) + IN.uv_TextureAtlas * sizeOffset) * size;
			float index1 = round(indices.y);
			float2 texCoord1 = (offset + float2(fmod(index1, _AtlasSize), floor(index1 / _AtlasSize)) + IN.uv_TextureAtlas * sizeOffset) * size;
			float index2 = round(indices.z);
			float2 texCoord2 = (offset + float2(fmod(index2, _AtlasSize), floor(index2 / _AtlasSize)) + IN.uv_TextureAtlas * sizeOffset) * size;
			float index3 = round(indices.w);
			float2 texCoord3 = (offset + float2(fmod(index3, _AtlasSize), floor(index3 / _AtlasSize)) + IN.uv_TextureAtlas * sizeOffset) * size;

			float isVisible0 = step(0, index0);
			float isVisible1 = step(0, index1);
			float isVisible2 = step(0, index2);
			float isVisible3 = step(0, index3);

			//Depth
			float depth0, depth1, depth2, depth3;
			depth0 = DecodeFloatRGBA(tex2D(_Depth, texCoord0)) + (1-isVisible0);
			depth1 = DecodeFloatRGBA(tex2D(_Depth, texCoord1)) + (1-isVisible1);
			depth2 = DecodeFloatRGBA(tex2D(_Depth, texCoord2)) + (1-isVisible2);
			depth3 = DecodeFloatRGBA(tex2D(_Depth, texCoord3)) + (1-isVisible3);

			float minDepth = min(depth0, min(depth1, min(depth2, depth3)));

			//Get the color of the texture that is closest to the camera at this point
			fixed4 c0 = tex2D (_TextureAtlas, texCoord0) * isVisible0;
			fixed4 c1 = tex2D (_TextureAtlas, texCoord1) * isVisible1;
			fixed4 c2 = tex2D (_TextureAtlas, texCoord2) * isVisible2;
			fixed4 c3 = tex2D (_TextureAtlas, texCoord3) * isVisible3;
			fixed4 c = c0 * step(depth0, minDepth) + c1 * step(depth1, minDepth) + c2 * step(depth2, minDepth) + c3 * step(depth3, minDepth);
			o.Albedo = c.rgb * IN.burnColor;
			
			//Alpha cutoff
			#if defined(GRASS_USE_INSTANCING)
				float smoothing = UNITY_ACCESS_INSTANCED_PROP(_InstancePosSmoothing_arr, _InstancePosSmoothing).a;
			#else
				float smoothing = IN.color.a;
			#endif
			
			#if defined(SMOOTH_TEXTURE)	
				c.a *= smoothing;
			#endif

			clip(c.a - _Cutoff);
			o.Alpha = 1;
			//o.Alpha = (c.a - _Cutoff) / max(fwidth(c.a) * 2, 0.0001) + 0.5f;

			//Specular and smoothness
			fixed4 ss0 = tex2D (_SpecularSmooth, texCoord0);
			fixed4 ss1 = tex2D (_SpecularSmooth, texCoord1);
			fixed4 ss2 = tex2D (_SpecularSmooth, texCoord2);
			fixed4 ss3 = tex2D (_SpecularSmooth, texCoord3);
			fixed4 ss = ss0 * step(depth0, minDepth) + ss1 * step(depth1, minDepth) + ss2 * step(depth2, minDepth) + ss3 * step(depth3, minDepth);
			o.Specular = ss.rgb;
			o.Smoothness = ss.a;

			//Normal
			float4 normal0 = tex2D(_Normal, texCoord0);
			float4 normal1 = tex2D(_Normal, texCoord1);
			float4 normal2 = tex2D(_Normal, texCoord2);
			float4 normal3 = tex2D(_Normal, texCoord3);

			//Normal
			float4 normal = normal0 * step(depth0, minDepth) + normal1 * step(depth1, minDepth) + normal2 * step(depth2, minDepth) + normal3 * step(depth3, minDepth);

			o.Normal = UnpackNormal(normal);
		}
		ENDCG
	} 
}
