#ifndef GRASS_VERTEX
#define GRASS_VERTEX

tess_appdata vert(appdata v)
{	
	tess_appdata o;
	UNITY_INITIALIZE_OUTPUT(tess_appdata, o);

	#ifdef GRASS_OBJECT_MODE
		o.objectSpacePos = v.vertex.xyz;
	#endif

	o.vertex = mul(unity_ObjectToWorld, v.vertex);
	o.uv = TRANSFORM_TEX(v.uv, _DensityTexture);

	#ifdef VERTEX_DENSITY
		o.color = v.color;
	#endif

	#if defined(GRASS_FOLLOW_SURFACE_NORMAL) || defined(GRASS_SURFACE_NORMAL_LIGHTING) || defined(GRASS_HYBRID_NORMAL_LIGHTING)
		o.normal = UnityObjectToWorldNormal(v.normal);
	#endif

	//Camera, or rather renderer pos
	o.cameraPos = getCameraPos();

	return o;
}
#endif