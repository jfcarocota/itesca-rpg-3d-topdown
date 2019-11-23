Shader "Stix Games/Grass Interaction/Wind"
{
	Properties
	{
		_InfluenceStrength("Influence Strength", float) = 1

		_WindParams("Wind WaveStrength(X), WaveSpeed(Y), RippleStrength(Z), RippleSpeed(W)", Vector) = (0.3, 1.2, 0.15, 1.3)
		_Scale("Wind Scale", float) = 10

		_Mask("Mask", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
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

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 pos : TEXCOORD0;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD1;
				float2 screenuv : TEXCOORD2;
			};

			sampler2D _GrabInteraction;
			sampler2D _GrabBurn;

			float _InfluenceStrength;

			fixed4 _WindParams;
			float _Scale;

			sampler2D _Mask;
			float4 _Mask_ST;

			#include "InteractionFunctions.hlsl"
			#include "../GrassWind.hlsl"

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.pos = mul(unity_ObjectToWorld, v.vertex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.screenuv = ComputeGrabScreenPos(o.vertex);
				o.uv = v.uv;
				return o;
			}

			struct InteractionValues
			{
				float4 Interaction : SV_Target0;
				float4 Burn : SV_Target1;
			};
			
			InteractionValues frag (v2f i) : SV_Target
			{
				//Smooth out the border of the interaction. 
				//If the interaction area too big to see the border, you should probably remove that for performance.
				float borderSmoothing = CalculateInteractionBorder(i.pos);

				half2 windDir = wind(float3(i.uv.x, 0, i.uv.y) * _Scale);
				half3 worldNormal = normalize(half3(windDir.x, 1, windDir.y));

				half mask = tex2D(_Mask, TRANSFORM_TEX(i.uv, _Mask)).r;
				worldNormal = normalize(worldNormal * mask + float3(0, 1, 0) * (1 - mask));

				float3 dir = normalize(worldNormal.xzy * borderSmoothing + float3(0, 0, 1) * (1.0f - borderSmoothing));
				float newIntensity = max(_InfluenceStrength, 0) * (1.0f - dot(dir, float3(0, 0, 1)));

				float4 prevCol = tex2D(_GrabInteraction, i.screenuv);
				float3 prevNormal = prevCol.xyz * 2 - float3(1, 1, 1);
				float prevIntensity = InverseMapIntensity(prevCol.a);

				dir = normalize(dir * newIntensity + prevNormal * prevIntensity + 0.0001f * float3(0, 0, 1));

				float newMappedIntensity = MapIntensity(prevIntensity + newIntensity);

				float4 prevBurn = tex2D(_GrabBurn, i.screenuv);

				InteractionValues val;
				val.Interaction = float4(dir * float3(0.5f, 0.5f, 0.5f) + float3(0.5f, 0.5f, 0.5f), newMappedIntensity);
				val.Burn = prevBurn;

				return val;
			}
			ENDCG
		}
	}
}
