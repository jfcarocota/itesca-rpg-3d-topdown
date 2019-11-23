#ifndef GRASS_LIGHTING
#define GRASS_LIGHTING

// Unshaded lighting
#if defined(GRASS_UNSHADED_LIGHTING)
inline half4 GRASS_BRDF(half3 diffColor, half3 specColor, half subsurface, half oneMinusReflectivity, half smoothness,
	half3 normal, half3 viewDir,
#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
	half3 specularNormal,
#endif
	UnityLight light, UnityIndirect gi, UnityIndirect giSubsurface)
{
	return half4(diffColor * (light.color + gi.diffuse), 1);
}

// Unity PBR lighting
#elif defined(GRASS_PBR_LIGHTING) && !defined(GRASS_HYBRID_NORMAL_LIGHTING)
inline half4 GRASS_BRDF(half3 diffColor, half3 specColor, half subsurface, half oneMinusReflectivity, half smoothness,
	half3 normal, half3 viewDir,
#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
	half3 specularNormal,
#endif
	UnityLight light, UnityIndirect gi, UnityIndirect giSubsurface)
{
	half4 c = UNITY_BRDF_PBS(diffColor, specColor, oneMinusReflectivity, smoothness, normal, viewDir, light, gi);

	#if defined(GRASS_RANDOM_DIR)
		//Cheap, unrealistic subsurface scattering
		//I am actually counteracting the main lambert term here, if subsurface is one, all shadows should be removed from the grass
		//This could be seen as the grass being perfectly lit through subsurface scattering
		half3 subsurfaceScattering = diffColor * subsurface * (saturate(1 - LambertTerm(normal, light.dir)) * light.color + giSubsurface.diffuse);
		c.rgb += subsurfaceScattering;
	#endif

	return c;
}
#else //Use fake lighting or hybrid normal
inline half4 GRASS_BRDF(half3 diffColor, half3 specColor, half subsurface, half oneMinusReflectivity, half smoothness,
	half3 normal, half3 viewDir,
#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
	half3 specularNormal,
#endif
	UnityLight light, UnityIndirect gi, UnityIndirect giSubsurface)
{
	half perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);

	// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
	// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
	// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
	// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
	// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
	// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
	#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

	#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
		// The amount we shift the normal toward the view vector is defined by the dot product.
		half shiftAmount = dot(normal, viewDir);
		normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
		// A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
		//normal = normalize(normal);

		half nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
	#else
		half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
	#endif

	half3 halfDir = Unity_SafeNormalize(light.dir + viewDir);
	half nl = saturate(dot(normal, light.dir));
	half lh = saturate(dot(light.dir, halfDir));

	// Diffuse term
	half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

	#if defined(GRASS_RANDOM_DIR)
		//Subsurface
		half3 subsurfaceScattering = diffColor * subsurface * (saturate(1 - LambertTerm(normal, light.dir)) * light.color + giSubsurface.diffuse);
	#endif

	#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
		normal = specularNormal;
	#endif 

	#if !defined(GRASS_PBR_LIGHTING)
		//Mirror light dir and normal to get specular lighting on the same side as the sun/light. 
		//Just because it's not realistic, doesn't mean it doesn't look nice!
		light.dir = half3(-light.dir.x, light.dir.y, -light.dir.z);

		#if !defined(GRASS_RANDOM_DIR)
			//Randomly rotated grass has correct normals (because backfaces are rendered) so the normal doesn't have to be changed
			normal = half3(-normal.x, normal.y, -normal.z);
		#endif
	#endif

	//Recalculate these variables for specular light
	halfDir = Unity_SafeNormalize(light.dir + viewDir);
	nl = saturate(dot(normal, light.dir));
	lh = saturate(dot(light.dir, halfDir));

	half nh = saturate(dot(normal, halfDir));
	half lv = saturate(dot(light.dir, viewDir));

	// Specular term
	// HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
	// BUT 1) that will make shader look significantly darker than Legacy ones
	// and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
	half roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
	#if UNITY_BRDF_GGX
		half V = SmithJointGGXVisibilityTerm(nl, nv, roughness);
		half D = GGXTerm(nh, roughness);
	#else
		// Legacy
		half V = SmithBeckmannVisibilityTerm(nl, nv, roughness);
		half D = NDFBlinnPhongNormalizedTerm(nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
	#endif

	half specularTerm = V * D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

	#ifdef UNITY_COLORSPACE_GAMMA
		specularTerm = sqrt(max(1e-4h, specularTerm));
	#endif

	// specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
	specularTerm = max(0, specularTerm * nl);
	#if defined(_SPECULARHIGHLIGHTS_OFF)
		specularTerm = 0.0;
	#endif

	// surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
	half surfaceReduction;
	#ifdef UNITY_COLORSPACE_GAMMA
		surfaceReduction = 1.0 - 0.28*roughness*perceptualRoughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
	#else
		surfaceReduction = 1.0 / (roughness*roughness + 1.0);           // fade \in [0.5;1]
	#endif

	// To provide true Lambert lighting, we need to be able to kill specular completely.
	specularTerm *= any(specColor) ? 1.0 : 0.0;

	half grazingTerm = saturate(smoothness + (1 - oneMinusReflectivity));
	half3 color = diffColor * (gi.diffuse + light.color * diffuseTerm)
		+ specularTerm * light.color * FresnelTerm(specColor, lh)
		+ surfaceReduction * gi.specular * FresnelLerp(specColor, grazingTerm, nv);

	#if defined(GRASS_RANDOM_DIR)
		//Cheap, unrealistic subsurface scattering
		color.rgb += subsurfaceScattering;
	#endif

	return half4(color, 1);
}
#endif

#endif //GRASS_LIGHTING