#ifndef GRASS_FRAG
#define GRASS_FRAG

FragmentCommonData GrassFragmentSetup(half3 albedo, half3 specular, half smoothness, float3 worldPos, half3 viewDir, half3 worldNormal)
{
	FragmentCommonData s = (FragmentCommonData)0;
	half oneMinusReflectivity;
	s.diffColor = EnergyConservationBetweenDiffuseAndSpecular(albedo, specular, /*out*/ oneMinusReflectivity);
	s.specColor = specular;
	s.oneMinusReflectivity = oneMinusReflectivity;
	s.smoothness = smoothness;
	s.normalWorld = worldNormal;
	s.eyeVec = -viewDir;
	s.posWorld = worldPos;

	return s;
}

#ifdef GRASS_PASS_FORWARDBASE
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
		fixed3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
		UnityLight light = MainLight();
		UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

		FragmentCommonData s = GrassFragmentSetup(o.Albedo, o.Specular, o.Smoothness, worldPos, viewDir, o.Normal);

		#if GRASS_IGNORE_GI_SPECULAR
			bool useReflections = false;
		#else
			bool useReflections = true;
		#endif

		UnityGI gi = FragmentGI(s, o.Occlusion, i.ambientOrLightmapUV, atten, light, useReflections);
		
		s.normalWorld = -s.normalWorld;
		UnityIndirect giSubsurface = FragmentGI(s, o.Occlusion, i.ambientOrLightmapUV, atten, light, useReflections).indirect;
		s.normalWorld = -s.normalWorld;

		#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
			c = GRASS_BRDF(s.diffColor, s.specColor, go.Subsurface, s.oneMinusReflectivity, s.smoothness, diffuseNormal, -s.eyeVec, s.normalWorld, gi.light, gi.indirect, giSubsurface);
		#else
			c = GRASS_BRDF(s.diffColor, s.specColor, go.Subsurface, s.oneMinusReflectivity, s.smoothness, s.normalWorld, -s.eyeVec, gi.light, gi.indirect, giSubsurface);
		#endif
	#endif //End not unlit block

	UNITY_APPLY_FOG(i.fogCoord, c); // apply fog
	//UNITY_OPAQUE_ALPHA(c.a);
	c.a = o.Alpha;
	return c;
}
#endif

#ifdef GRASS_PASS_FORWARDADD
half4 frag(FS_INPUT i) : SV_Target
{
	float3 worldPos = i.worldPos;

	#ifdef UNITY_COMPILER_HLSL
		SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
		GrassSurfaceOutput go = (GrassSurfaceOutput)0;
	#else
		SurfaceOutputStandardSpecular o;
		GrassSurfaceOutput go;
	#endif

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
	o.Smoothness = 0.5;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

	surf(i, o, go);

	fixed4 c = 0;

	#if !defined(GRASS_UNLIT_LIGHTING)
		half3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
		half3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));

		UNITY_LIGHT_ATTENUATION(atten, i, worldPos);
		UnityLight light = AdditiveLight(lightDir, atten);
		UnityIndirect noIndirect = ZeroIndirect();

		FragmentCommonData s = GrassFragmentSetup(o.Albedo, o.Specular, o.Smoothness, worldPos, viewDir, o.Normal);

		#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
			c = GRASS_BRDF(s.diffColor, s.specColor, go.Subsurface, s.oneMinusReflectivity, s.smoothness, diffuseNormal, -s.eyeVec, s.normalWorld, light, noIndirect, noIndirect);
		#else
			c = GRASS_BRDF(s.diffColor, s.specColor, go.Subsurface, s.oneMinusReflectivity, s.smoothness, s.normalWorld, -s.eyeVec, light, noIndirect, noIndirect);
		#endif
	#endif

	c.a = 0.0;

	//UNITY_OPAQUE_ALPHA(c.a);
	c.a = o.Alpha;
	return c;
}
#endif

#ifdef GRASS_PASS_DEFERRED
struct DeferredFramebuffers
{
	half4 diffuseOcclusion : SV_Target0;
	half4 specularSmoothness : SV_Target1;
	half4 worldNormal : SV_Target2;
	half4 lighting : SV_Target3;
#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
	half4 shadowMask : SV_Target4       // RT4: shadowmask (rgba)
#endif
};

DeferredFramebuffers frag(FS_INPUT i)
{
	float3 worldPos = i.worldPos;

	#ifdef UNITY_COMPILER_HLSL
		SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
		GrassSurfaceOutput go = (GrassSurfaceOutput)0;
		DeferredFramebuffers def = (DeferredFramebuffers)0;
	#else
		SurfaceOutputStandardSpecular o;
		GrassSurfaceOutput go;
		DeferredFramebuffers def;
	#endif

	o.Albedo = 0.0;
	o.Normal = normalize(i.normal);
	o.Emission = 0.0;
	o.Specular = 0;
	o.Smoothness = 1.0;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

	surf(i, o, go);

#if defined(GRASS_UNSHADED_LIGHTING)

#endif

	half3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
	FragmentCommonData s = GrassFragmentSetup(o.Albedo, o.Specular, o.Smoothness, worldPos, viewDir, o.Normal);
	UnityLight dummyLight = DummyLight();
	half atten = 1;

#if UNITY_ENABLE_REFLECTION_BUFFERS
	bool sampleReflectionsInDeferred = false;
#else
	bool sampleReflectionsInDeferred = true;
#endif

	UnityGI gi = FragmentGI(s, o.Occlusion, i.ambientOrLightmapUV, atten, dummyLight, sampleReflectionsInDeferred);

	half3 emissiveColor = UNITY_BRDF_PBS(s.diffColor, s.specColor, s.oneMinusReflectivity, s.smoothness, s.normalWorld, -s.eyeVec, gi.light, gi.indirect).rgb;

	//Unity Standard shader does something different here, this line simply doesn't work here...
	/*#ifndef UNITY_HDR_ON
		emissiveColor.rgb = exp2(-emissiveColor.rgb);
	#endif*/

	UnityStandardData data;
	data.diffuseColor = s.diffColor;
	data.occlusion = o.Occlusion;
	data.specularColor = s.specColor;
	data.smoothness = s.smoothness;
	data.normalWorld = s.normalWorld;

	UnityStandardDataToGbuffer(data, def.diffuseOcclusion, def.specularSmoothness, def.worldNormal);

	// Emissive lighting buffer
	def.lighting = half4(emissiveColor, 1);

	// Baked direct lighting occlusion if any
	#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
		def.shadowMask = UnityGetRawBakedOcclusions(i.ambientOrLightmapUV.xy, worldPos);
	#endif

	return def;
}
#endif

#ifdef GRASS_PASS_SHADOWCASTER
half4 frag(FS_INPUT i) : SV_Target
{
	// prepare and unpack data
	#ifdef UNITY_COMPILER_HLSL
		SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
		GrassSurfaceOutput go = (GrassSurfaceOutput)0;
	#else
		SurfaceOutputStandardSpecular o;
		GrassSurfaceOutput go;
	#endif
	fixed3 normalWorldVertex = fixed3(0, 0, 1);
	o.Albedo = 0.0;
	o.Normal = normalWorldVertex;
	o.Emission = 0.0;
	o.Specular = 0;
	o.Smoothness = 1;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

	// call surface function
	surf(i, o, go);

	SHADOW_CASTER_FRAGMENT(i)
}
#endif

#ifdef RENDER_NORMAL_DEPTH
half4 frag(FS_INPUT i) : SV_Target
{
	// prepare and unpack data
	#ifdef UNITY_COMPILER_HLSL
		SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
		GrassSurfaceOutput go = (GrassSurfaceOutput)0;
	#else
		SurfaceOutputStandardSpecular o;
		GrassSurfaceOutput go;
	#endif
	fixed3 normalWorldVertex = fixed3(0, 0, 1);
	o.Albedo = 0.0;
	o.Normal = normalWorldVertex;
	o.Emission = 0.0;
	o.Specular = 0;
	o.Smoothness = 1;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

	// call surface function, here it handles the cutoff
	surf(i, o, go);

	float depth = -(mul(UNITY_MATRIX_V, float4(i.worldPos, 1)).z * _ProjectionParams.w);
	float3 normal = normalize(i.normal);;
	normal.b = 0;
	return EncodeDepthNormal(depth, normal);
}
#endif

#ifdef GRASS_FALLBACK_RENDERER
struct FallbackTextures 
{
	float4 Albedo : SV_Target0;
	float4 MetallicSmooth : SV_Target1;
	float4 Depth : SV_Target2;
	float4 Normal : SV_Target3;
};

FallbackTextures frag(FS_INPUT i)
{
	// prepare and unpack data
	#ifdef UNITY_COMPILER_HLSL
		SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
		GrassSurfaceOutput go = (GrassSurfaceOutput)0;
		FallbackTextures fallback = (FallbackTextures)0;
	#else
		SurfaceOutputStandardSpecular o;
		GrassSurfaceOutput go;
		FallbackTextures fallback;
	#endif
	fixed3 normalWorldVertex = fixed3(0, 0, 1);
	o.Albedo = 0.0;
	o.Normal = normalWorldVertex;
	o.Emission = 0.0;
	o.Specular = 0;
	o.Smoothness = 1;
	o.Occlusion = 1.0;
	o.Alpha = 0.0;
	go.Subsurface = 0.0;

	// call surface function, here it handles the cutoff
	surf(i, o, go);

	//Calculates the view 01 depth for the current position, then encodes it into the RGBA channels
	float4 encodedDepth = EncodeFloatRGBA(-(mul(UNITY_MATRIX_V, float4(i.worldPos, 1)).z * _ProjectionParams.w));
	
	//Calculate the view space normal
	float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, i.normal));
	float3 packedNormal = (viewNormal + 1) * 0.5f;
	
	fallback.Albedo = float4(o.Albedo, o.Alpha);
	fallback.MetallicSmooth = float4(o.Specular, o.Smoothness);
	fallback.Depth = float4(encodedDepth);
	fallback.Normal = float4(packedNormal, 1);
	return fallback;
}
#endif

#endif