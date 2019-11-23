#ifndef INTERACTION_FUNCTIONS_HLSL
#define INTERACTION_FUNCTIONS_HLSL

float _GrassInteractionBorderArea;
float4 _GrassRenderTextureArea;

float CalculateInteractionBorder(float3 pos)
{
	float borderSmoothing = smoothstep(_GrassRenderTextureArea.x, _GrassRenderTextureArea.x + _GrassInteractionBorderArea, pos.x);
	borderSmoothing *= smoothstep(_GrassRenderTextureArea.y, _GrassRenderTextureArea.y + _GrassInteractionBorderArea, pos.z);

	float xBorder = _GrassRenderTextureArea.x + _GrassRenderTextureArea.z;
	borderSmoothing *= smoothstep(xBorder, xBorder - _GrassInteractionBorderArea, pos.x);

	float yBorder = _GrassRenderTextureArea.y + _GrassRenderTextureArea.w;
	borderSmoothing *= smoothstep(yBorder, yBorder - _GrassInteractionBorderArea, pos.z);

	return borderSmoothing;
}

//This variable defines how the intensities for grass interaction will be mapped onto (0,1) from (0,infinity(
//Intuitively, it is the number that will be mapped to 0.5, so if INTENSITY_CONVERGENCE is 5, 0-5 will be mapped 
//to 0-0.5 and all values above will be mapped to 0.5-1
//If you wish to use integer intensities, it would be most precise to use 128
#define INTENSITY_CONVERGENCE 5

//y = x / (x + a)
float MapIntensity(float intensity)
{
	return intensity / (intensity + INTENSITY_CONVERGENCE);
}

//x = ay / (1-y)
float InverseMapIntensity(float mappedIntensity)
{
	return (INTENSITY_CONVERGENCE * mappedIntensity) / (1 - mappedIntensity);
}
#endif