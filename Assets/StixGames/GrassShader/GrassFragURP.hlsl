#ifndef GRASS_FRAG_URP
#define GRASS_FRAG_URP

// ========================== Universal render pipeline ========================
void InitializeInputData(FS_INPUT input, half3 viewDirWS, out InputData inputData)
{
    inputData = (InputData)0;

#ifdef _ADDITIONAL_LIGHTS
    inputData.positionWS = input.worldPos;
#endif

    // No normal map
    inputData.normalWS = input.normal;

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    viewDirWS = SafeNormalize(viewDirWS);

    inputData.viewDirectionWS = viewDirWS;

#if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
    inputData.shadowCoord = input.shadowCoord;
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = input.fogCoord.x;
    inputData.bakedGI = float3(0,0,0);
}

#if defined(GRASS_PASS_UNIVERSALFORWARD)
half4 frag(FS_INPUT i) : SV_Target
{
	float3 worldPos = i.worldPos;

	SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
	GrassSurfaceOutput go = (GrassSurfaceOutput)0;

	#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
		half3 normal = normalize(i.specularNormal);
		half3 diffuseNormal = normalize(i.normal);
	#else
		half3 normal = normalize(i.normal);
	#endif

	o.Albedo = 0.0;
	o.Normal = normal;
	o.Emission = 0.0;
	o.Specular = 0;
	o.Smoothness = 1.0;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

	surf(i, o, go);
	
	half4 c = 0;

	#if defined(GRASS_UNLIT_LIGHTING)
		c = half4(o.Albedo, 1);
	#else //Not unlit
		half3 viewDirWS = GetCameraPositionWS() - worldPos;
		
		InputData inputData;
        InitializeInputData(i, viewDirWS, inputData);
		
		c = UniversalFragmentPBR(inputData, o.Albedo, 1.0h, o.Specular.rgb, o.Smoothness, o.Occlusion, o.Emission, o.Alpha);

        c.rgb = MixFog(c.rgb, inputData.fogCoord);
	#endif //End not unlit block

	c.a = o.Alpha;
	return c;
}
#endif // GRASS_PASS_UNIVERSALFORWARD

#if defined(GRASS_PASS_DEPTHONLY)
half4 frag(FS_INPUT i) : SV_TARGET
{
    float3 worldPos = i.worldPos;

	SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
	GrassSurfaceOutput go = (GrassSurfaceOutput)0;

	#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
		half3 normal = normalize(i.specularNormal);
		half3 diffuseNormal = normalize(i.normal);
	#else
		half3 normal = normalize(i.normal);
	#endif

	o.Albedo = 0.0;
	o.Normal = normal;
	o.Emission = 0.0;
	o.Specular = 0;
	o.Smoothness = 1.0;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

    // Perform cutoff
	surf(i, o, go);
	
	return 0;
}
#endif

#endif