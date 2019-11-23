Shader "Stix Games/Grass Interaction/Interaction"
{
	Properties
	{
		_InfluenceStrength("Influence Strength", float) = 1

		_NormalMap("NormalMap", 2D) = "bump" {}
		_InteractionStrength("Interaction Strength", Range(0.01, 1)) = 1

		_Mask("Mask", 2D) = "white"  {}

		_BurnColor("Burn Color", Color) = (1, 1, 1, 1)
		_BurnMap("Burn Map", 2D) = "white"  {}
		_BurnColorStrength("Burn Color Strength", Range(0, 1)) = 0
		_BurnStrength("Burn Strength", Range(0, 1)) = 0
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "DisableBatching" = "True" }
		LOD 100

		Cull Off

		ZWrite Off
		ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "InteractionFunctions.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 pos : TEXCOORD0;
				half3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
				half3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
				half3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z
				float2 uv_normal : TEXCOORD4;
				float2 uv_mask : TEXCOORD5;
				float2 uv_burn : TEXCOORD6;
				float2 screenuv : TEXCOORD7;
				float4 interaction_strength: COLOR;
			};

			sampler2D _GrabInteraction;
			sampler2D _GrabBurn;

			float _InfluenceStrength;

			half4 _BurnColor;
			half _InteractionStrength;

			sampler2D _NormalMap;
			float4 _NormalMap_ST;
			sampler2D _Mask;
			float4 _Mask_ST;
			sampler2D _BurnMap;
			float4 _BurnMap_ST;

			float _BurnColorStrength;
			float _BurnStrength;

			v2f vert(appdata v)
			{
				v2f o;

				//Position
				o.pos = mul(unity_ObjectToWorld, v.vertex);
				o.vertex = UnityObjectToClipPos(v.vertex);

				//Vertex color
				o.interaction_strength = v.color;

				//Texture coordinates
				o.uv_normal = TRANSFORM_TEX(v.uv, _NormalMap);
				o.uv_mask = TRANSFORM_TEX(v.uv, _Mask);
				o.uv_burn = TRANSFORM_TEX(v.uv, _BurnMap);
				o.screenuv = ComputeGrabScreenPos(o.vertex);

				//Tangent space
				half3 wNormal = UnityObjectToWorldNormal(v.normal);
				half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
				// compute bitangent from cross product of normal and tangent
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
				// output the tangent space matrix
				o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
				o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
				o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);

				return o;
			}

			struct InteractionValues
			{
				float4 Interaction : SV_Target0;
				float4 Burn : SV_Target1;
			};

			InteractionValues frag(v2f i)
			{
				// sample the normal map, and decode from the Unity encoding
				half3 tNormal = UnpackNormal(tex2D(_NormalMap, i.uv_normal));

				tNormal.y *= -1;

				tNormal.xy *= _InteractionStrength;

				// transform normal from tangent to world space
				half3 worldNormal;
				worldNormal.x = dot(i.tspace0, tNormal);
				worldNormal.y = dot(i.tspace1, tNormal);
				worldNormal.z = dot(i.tspace2, tNormal);
				worldNormal = normalize(worldNormal);

				//Smooth out the border of the interaction. 
				float borderSmoothing = CalculateInteractionBorder(i.pos);

				// Get the mask
				float mask = tex2D(_Mask, i.uv_mask).r;

				//Interaction
				float interactionStrength = i.interaction_strength * borderSmoothing * mask;
				float3 dir = normalize(worldNormal.xzy * interactionStrength + float3(0, 0, 1) * (1.0f - interactionStrength));
				float newIntensity = max(_InfluenceStrength, 0) * (1.0f - dot(dir, float3(0, 0, 1)));

				float4 prevCol = tex2D(_GrabInteraction, i.screenuv);
				float3 prevNormal = prevCol.xyz * 2 - float3(1, 1, 1);
				float prevIntensity = InverseMapIntensity(prevCol.a);

				dir = normalize(dir * newIntensity + prevNormal * prevIntensity + 0.0001f * float3(0,0,1));

				float newMappedIntensity = MapIntensity(prevIntensity + newIntensity);

				//Calculate burn color
				float burnColorStrength = _BurnColorStrength * borderSmoothing * mask;
				float burnStrength = _BurnStrength * borderSmoothing * mask;

				float4 burnTexture = tex2D(_BurnMap, i.uv_burn);
				float3 burnColor = lerp(float3(1, 1, 1), burnTexture.rgb * _BurnColor, burnColorStrength);
				float burnAmount = 1 - burnStrength * burnTexture.a;

				float4 prevBurn = tex2D(_GrabBurn, i.screenuv);

				InteractionValues val;
				val.Interaction = float4(dir * float3(0.5f, 0.5f, 0.5f) + float3(0.5f, 0.5f, 0.5f), newMappedIntensity);
				val.Burn = float4(burnColor, burnAmount) * prevBurn;

				return val;
			}
			ENDCG
		}
	}
}
