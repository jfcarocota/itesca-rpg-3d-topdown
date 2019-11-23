Shader "Hidden/TextureDilate"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_PixelSize("Texture Size", Float) = 0.001
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			sampler2D _MainTex;
			float _PixelSize;

			#define DIRECTIONS_SIZE 8
			static float2 directions[DIRECTIONS_SIZE] =
			{
				float2( 1,  0),
				float2(-1,  0),
				float2( 0,  1),
				float2( 0, -1),
				float2( 1,  1),
				float2( 1, -1),
				float2(-1,  1),
				float2(-1, -1)
			};

			float4 frag (v2f input) : SV_Target
			{
				float4 col = tex2D(_MainTex, input.uv);

				for(int i = 0; i < DIRECTIONS_SIZE; i++)
				{
					float4 newCol = tex2D(_MainTex, input.uv + directions[i] * _PixelSize);

					if (col.a < newCol.a)
					{
						col.rgb = newCol.rgb;

						break;
					}
				}

				return col;
			}
			ENDCG
		}
	}
}
