#ifndef GRASS_GEOM
#define GRASS_GEOM

inline FS_INPUT geomToFrag(GS_OUTPUT v)
{
	FS_INPUT o = (FS_INPUT) 0;
	UNITY_INITIALIZE_OUTPUT(FS_INPUT, o)

	float3 worldPos = v.vertex.xyz;

	//This is necessary for shadow calculation
	v.vertex = mul(unity_WorldToObject, v.vertex);

	#ifdef GRASS_CURVED_WORLD
		V_CW_TransformPoint(v.vertex);
	#endif

	o.worldPos = worldPos;

	#if !defined(SIMPLE_GRASS)
		o.uv = v.uv;
		o.texIndex = v.texIndex;

		#ifdef GRASS_TEXTURE_ATLAS
			o.textureAtlasIndex = v.textureAtlasIndex;
		#endif
	#endif

	o.color = v.color;
	o.floorColor = v.floorColor;

	#ifndef SHADOWPASS
	    #if defined(GRASS_URP)
	        VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
		    o.pos = vertexInput.positionCS;
        #else
            o.pos = UnityObjectToClipPos(v.vertex);
        #endif
		o.normal = v.normal;

		#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
			o.specularNormal = v.specularNormal;
		#endif

        #if defined(GRASS_URP)
            o.shadowCoord = GetShadowCoord(vertexInput);
            o.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
        #else
            TRANSFER_SHADOW(o); // pass shadow coordinates to pixel shader
            UNITY_TRANSFER_FOG(o, o.pos); // pass fog coordinates to pixel shader
		#endif

		//TODO: Lightmap
		//o.ambientOrLightmapUV = VertexGIForward(v, worldPos, v.normal);
	#elif defined(GRASS_PASS_SHADOWCASTER)
		TRANSFER_SHADOW_CASTER(o)
	#endif

	return o;
}

inline void generateBladeOfGrass(inout TriangleStream<FS_INPUT> triStream, GS_OUTPUT pIn, int lod,
	float3 rendererPos, float3 oPos, fixed3 up, fixed3 groundRight, fixed realHeight,
	fixed width, fixed4 color, fixed4 floorColor,
	half2 windDir, fixed3 interaction, fixed softnessFactor)
{
	//Get lightDir
	#if defined(GRASS_URP)
	    fixed3 lightDir = GetMainLight().direction;
	#else
        #if defined(USING_DIRECTIONAL_LIGHT)
            fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
        #else
            fixed3 lightDir = normalize(UnityWorldSpaceLightDir(oPos));
        #endif
    #endif

	float3 lastPos = oPos - up * 0.01f;
	fixed invLod = 1.0f / lod;

	for(fixed i = 0; i <= lod; i++)
	{
		fixed segment = i*invLod;
		fixed sqrSegment = segment*segment;

		float3 pos = float3(up*segment*realHeight);

		#if defined(GRASS_INTERACTION)
			float interactionHeight = pow(interaction.z, 2);
			
			pos.xz += (windDir.xy * interactionHeight + interaction.xy * realHeight * 3) * sqrSegment * softnessFactor;
			pos.y -= length(windDir.xy + interaction.xy) * sqrSegment * 0.5f * softnessFactor;
			pos.y *= max(interactionHeight, 0.01);
		#else
			pos.xz += windDir.xy * sqrSegment * softnessFactor;
			pos.y  -= length(windDir) * sqrSegment * 0.5f * softnessFactor;
		#endif

		pos += oPos;

		fixed uvHeight = segment;

        // TODO: Custom lighting effects for additional lights in URP
		#if !defined(GRASS_URP) && !defined(USING_DIRECTIONAL_LIGHT)
			lightDir = normalize(UnityWorldSpaceLightDir(pos));
		#endif

		fixed3 currentUp = pos - lastPos;

		fixed3 right = groundRight;

		//Vertex definition
		#if defined(SIMPLE_GRASS)
			//Simple grass has no texture, so the mesh has to look like a blade of grass
			pIn.vertex =  float4((pos - width * right * (1 - sqrSegment)).xyz, 1);
		#else
			pIn.vertex =  float4((pos - width * right).xyz, 1);
		#endif
		
		#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
			// Use the hybrid normal mode
			pIn.specularNormal = getNormal(currentUp, right, lightDir);
		#elif !defined(GRASS_SURFACE_NORMAL_LIGHTING)
			// The regular lighting mode is used, there is no normal information in pIn yet
			pIn.normal = getNormal(currentUp, right, lightDir);
		#endif

		#if !defined(SIMPLE_GRASS)
			pIn.uv = fixed2(0.0f, uvHeight);
		#endif

		pIn.color = color;
		pIn.floorColor = fixed4(floorColor.rgb, 1-(1-sqrt(segment)) * floorColor.a);
		triStream.Append(geomToFrag(pIn));

		//Vertex definition
		#if defined(SIMPLE_GRASS)
			//Simple grass has no texture, so the mesh has to look like a blade of grass
			pIn.vertex =  float4((pos + width * right * (1 - sqrSegment)).xyz, 1);
		#else
			pIn.vertex =  float4((pos + width * right).xyz, 1);
		#endif
		
		#if defined(GRASS_HYBRID_NORMAL_LIGHTING)
			// Use the hybrid normal mode
			pIn.specularNormal = getNormal(currentUp, right, lightDir);
		#elif !defined(GRASS_SURFACE_NORMAL_LIGHTING)
			// The regular lighting mode is used, there is no normal information in pIn yet
			pIn.normal = getNormal(currentUp, right, lightDir);
		#endif

		#if !defined(SIMPLE_GRASS)
			pIn.uv = fixed2(1.0f, uvHeight);
		#endif
		
		triStream.Append(geomToFrag(pIn));

		lastPos = pos;
	}
				
	triStream.RestartStrip();
}

inline void pointGeometryShader(GS_INPUT p, float3 randCalcOffset, inout TriangleStream<FS_INPUT> triStream)
{
	float3 rendererPos = p.cameraPos;
	float3 cameraPos = _WorldSpaceCameraPos;

	//Init pos, uv
	float3 oPos = p.position.xyz;
	fixed4 uv = fixed4(p.uv, 0, 0);

	//This variable is used for calculating random values. If you have a better name for it, I'm all ears!
	#ifdef GRASS_OBJECT_MODE
		float3 randCalcPos = p.objectSpacePos;
	#else
		float3 randCalcPos = oPos;
	#endif

	randCalcPos += randCalcOffset;

	//Calculate viewDir and groundRight vector
	#ifdef GRASS_FOLLOW_SURFACE_NORMAL
		fixed3 up = normalize(p.normal);
	#else
		fixed3 up = fixed3(0, 1, 0);
	#endif
	
	fixed3 viewDir = normalize(rendererPos - oPos);
	fixed3 cameraForward = UNITY_MATRIX_V[2].xyz;

	//Set grass orientation
	#if defined(GRASS_RANDOM_DIR)
		fixed3 orientationDir = fixed3(rand(randCalcPos.xz + float2(-5888, 7751)), 0, rand(randCalcPos.xz + float2(5485, -123))) * 2 - fixed3(1, 0, 1);
	#else
		fixed3 orientationDir = viewDir;
	#endif

	//Grass variable declaration
	fixed maxHeight = 0;
	fixed minHeight = 0;
	fixed width = 0;
	fixed softness = 0;
	#ifndef SHADOWPASS
		fixed4 mainColor = fixed4(0, 0, 0, 0);
		fixed4 secColor = fixed4(0, 0, 0, 0);
	#endif
	#ifdef GRASS_TEXTURE_ATLAS
		int textureAtlasIndex = 0;
	#endif

	#if !defined(UNIFORM_DENSITY)
		#ifdef VERTEX_DENSITY
			//Vertex density
			fixed4 density = p.color;
		#else
			//Texture density
			fixed4 density = tex2Dlod(_DensityTexture, uv);
		#endif
	#endif

	#if defined(SIMPLE_GRASS)
		float randVal = rand(randCalcPos.xz + float2(1789, -2360));

		if(randVal < DENSITY00)
		{
			maxHeight = _MaxHeight00;
			minHeight = _MinHeight00;
			width = _Width00;
			softness = _Softness00;
			#ifndef SHADOWPASS
				mainColor = _Color00;
				secColor = _SecColor00;
			#endif
		}
		else
		{
            GS_OUTPUT pIn = (GS_OUTPUT) 0;

			triStream.Append(geomToFrag(pIn));
			return;
		}
	#else //If textured grass
		//Grass Type
		//Selects a random type of grass. If the probability is over 1, it will be scaled down.
		#ifdef FOUR_GRASS_TYPES
			float randVal = rand(randCalcPos.xz + float2(1789, -2360)) * max(DENSITY00 + DENSITY01 + DENSITY02 + DENSITY03, 1);
		#elif defined(THREE_GRASS_TYPES)
			float randVal = rand(randCalcPos.xz + float2(1789, -2360)) * max(DENSITY00 + DENSITY01 + DENSITY02, 1);
		#elif defined(TWO_GRASS_TYPES)
			float randVal = rand(randCalcPos.xz + float2(1789, -2360)) * max(DENSITY00 + DENSITY01, 1);
		#else
			float randVal = rand(randCalcPos.xz + float2(1789, -2360));
		#endif
		int texIndex = 0;
		
		#ifdef GRASS_TEXTURE_ATLAS
			float textureAtlasRandVal = rand(randCalcPos.xz + float2(-2346, 1246));
		#endif

		if(randVal < DENSITY00)
		{
			texIndex = 0;
			maxHeight = _MaxHeight00;
			minHeight = _MinHeight00;
			width = _Width00;
			softness = _Softness00;
			#ifndef SHADOWPASS
				mainColor = _Color00;
				secColor = _SecColor00;
			#endif
			#ifdef GRASS_TEXTURE_ATLAS
				textureAtlasIndex = (int)(textureAtlasRandVal * _TextureAtlasWidth00 * _TextureAtlasHeight00);
			#endif
		}
		#if !defined(ONE_GRASS_TYPE)
		else if(randVal < (DENSITY00 + DENSITY01))
		{
			texIndex = 1;
			maxHeight = _MaxHeight01;
			minHeight = _MinHeight01;
			width = _Width01;
			softness = _Softness01;
			#ifndef SHADOWPASS
				mainColor = _Color01;
				secColor = _SecColor01;
			#endif
			#ifdef GRASS_TEXTURE_ATLAS
				textureAtlasIndex = (int)(textureAtlasRandVal * _TextureAtlasWidth01 * _TextureAtlasHeight01);
			#endif
		}
		#if !defined(TWO_GRASS_TYPES)
		else if(randVal < (DENSITY00 + DENSITY01 + DENSITY02))
		{
			texIndex = 2;
			maxHeight = _MaxHeight02;
			minHeight = _MinHeight02;
			width = _Width02;
			softness = _Softness02;
			#ifndef SHADOWPASS
				mainColor = _Color02;
				secColor = _SecColor02;
			#endif
			#ifdef GRASS_TEXTURE_ATLAS
				textureAtlasIndex = (int)(textureAtlasRandVal * _TextureAtlasWidth02 * _TextureAtlasHeight02);
			#endif
		}
		#if !defined(THREE_GRASS_TYPES)
		else if(randVal < (DENSITY00 + DENSITY01 + DENSITY02 + DENSITY03))
		{
			texIndex = 3;
			maxHeight = _MaxHeight03;
			minHeight = _MinHeight03;
			width = _Width03;
			softness = _Softness03;
			#ifndef SHADOWPASS
				mainColor = _Color03;
				secColor = _SecColor03;
			#endif
			#ifdef GRASS_TEXTURE_ATLAS
				textureAtlasIndex = (int)(textureAtlasRandVal * _TextureAtlasWidth03 * _TextureAtlasHeight03);
			#endif
		}
		#endif
		#endif
		#endif
		else
		{
			//If no grass type was randomized, return a single vertex, so no blade of grass will be rendered.
            GS_OUTPUT pIn = (GS_OUTPUT)0;

			pIn.texIndex = -1;

			triStream.Append(geomToFrag(pIn));
			return;
		}
	#endif

	//Calculate wind
	#if defined(GRASS_CALC_GLOBAL_WIND)
		half2 windDir = wind(randCalcPos, _WindRotation);
	#else
		half2 windDir = half2(0, 0);
	#endif

	//Add disorder offset
	half randX = (rand(randCalcPos.xz + 1000) * 2 - 1) * _Disorder;
	half randZ = (rand(randCalcPos.xz - 1000) * 2 - 1) * _Disorder;

	//If grass is looked at from the top, it should still look like grass
	#if defined(GRASS_TOP_VIEW_COMPENSATION)
		fixed topViewCompensation = 1 + pow(max(0, dot(viewDir, up)), 20) * 0.8;
		width *= topViewCompensation;
		
		windDir += half2(randX, randZ) * topViewCompensation;
	#else
		windDir += half2(randX, randZ);
	#endif

	//Grass height from color map
	fixed4 tex = tex2Dlod(_ColorMap, uv);

	//Grass height from distance falloff
	fixed dist = distance(oPos, cameraPos);

	fixed grassHeightMod = tex.a * smoothstep(_GrassFadeEnd, _GrassFadeStart, dist);
	
	//Tessellation smoothing by height
	#if defined(GRASS_HEIGHT_SMOOTHING)
		grassHeightMod *= p.smoothing;
	#endif

	//Tessellation smoothing by width
	#if defined(GRASS_WIDTH_SMOOTHING)
		width *= p.smoothing;
	#endif

	//Calculate real height
	fixed realHeight = (rand(randCalcPos.xz) * (maxHeight - minHeight) + minHeight) * grassHeightMod;

	//LOD
	int lod = (int) max(smoothstep(_LODEnd, _LODStart, dist)*_LODMax, 1);

	//Calculate grass interaction
	fixed3 interaction = fixed3(0, 0, 1);
	fixed4 burnFactor = fixed4(1, 1, 1, 1);
	#if defined(GRASS_INTERACTION)
		#if defined(GRASS_RENDERTEXTURE_INTERACTION)
			float2 coords = (oPos.xz - _GrassRenderTextureArea.xy) / _GrassRenderTextureArea.zw;
			fixed4 interactionTexture = tex2Dlod(_GrassRenderTextureInteraction, float4(coords, 0, 0));
			interaction.xyz = normalize(interactionTexture.rgb * 2 - float3(1, 1, 1));
			burnFactor = tex2Dlod(_GrassRenderTextureBurn, float4(coords, 0, 0));
		#else
			interaction = tex2Dlod(_Displacement, uv);

			//Convert from texture to vector
			interaction.xy = (interaction.xy * 2.0) - fixed2(1, 1);
		#endif
	#endif

	//Width is split up by offset to right and to left, so we only need half
	width *= 0.5;

	realHeight *= burnFactor.a;
	//width *= burnFactor;

    //Abort generating grass, when certain values are too low
    //Both could probably be replaced by realHeight < 0.01, but I don't want to break compatibility with very small scenes
    if (dist > _GrassFadeEnd || burnFactor.a < _BurnCutoff)
	{
        GS_OUTPUT pIn = (GS_OUTPUT)0;

		triStream.Append(geomToFrag(pIn));
		return;
	}

	//Color
	fixed4 color = fixed4(1,1,1,1);
	#if !defined(SHADOWPASS)
		color = tex;
		color *= lerp(mainColor, secColor, rand(randCalcPos.xz + fixed2(1000, -1000)));
		color *= fixed4(burnFactor.rgb, 1);
	#endif

	#if defined(GRASS_ALPHA_SMOOTHING) && !defined(SIMPLE_GRASS)
		color.a = p.smoothing;
	#else
		color.a = 1.0f;
	#endif

	//Calculate the floor color
	fixed4 floorColor = _GrassFloorColor * tex2Dlod(_GrassFloorColorTexture, uv);
	floorColor.rgb *= burnFactor.rgb;

	//Apply grass softness
	fixed softnessFactor = realHeight * softness;

	//Cacluate ground right direction
	fixed3 groundRight = normalize(cross(up, orientationDir));

	//Set all default values, that (can) stay the same for the whole blade of grass
	GS_OUTPUT pIn;

	#if !defined(SIMPLE_GRASS)
		pIn.texIndex = texIndex;
	#endif
	
	#ifdef GRASS_TEXTURE_ATLAS
		pIn.textureAtlasIndex = textureAtlasIndex;
	#endif

	//Set the surface normal for rendering
	#if defined(GRASS_SURFACE_NORMAL_LIGHTING) || defined(GRASS_HYBRID_NORMAL_LIGHTING)
		pIn.normal = normalize(p.normal);
	#endif

	//Generate the grass itself
	generateBladeOfGrass(/* inout */ triStream, pIn, 
		lod, rendererPos, oPos, up, groundRight,
		realHeight, width, 
		color, floorColor, windDir, interaction, softnessFactor);

	#if defined(GRASS_RANDOM_DIR)
	generateBladeOfGrass(/* inout */ triStream, pIn,
		lod, rendererPos, oPos, up, -groundRight,
		realHeight, width,
		color, floorColor, windDir, interaction, softnessFactor);
	#endif
}

#if defined(GRASS_RANDOM_DIR)
[maxvertexcount(2 * MAX_VERTEX_COUNT)]
#else
[maxvertexcount(MAX_VERTEX_COUNT)]
#endif
void geom(triangle GS_INPUT p[3], inout TriangleStream<FS_INPUT> triStream)
{
	pointGeometryShader(p[0], float3(0, 0, 0), /* inout */ triStream);
	//pointGeometryShader(p[1], float3(0.01, 0, 0), /* inout */ triStream);
	//pointGeometryShader(p[2], float3(0, 0, 0.01), /* inout */ triStream);
}
#endif