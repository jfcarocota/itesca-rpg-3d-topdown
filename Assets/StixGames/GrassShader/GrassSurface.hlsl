#ifndef GRASS_SURFACE
#define GRASS_SURFACE

inline void surf(FS_INPUT i, inout SurfaceOutputStandardSpecular o, inout GrassSurfaceOutput go)
{
	half4 color = 0.0;

	#if defined(SIMPLE_GRASS)
		#ifndef SHADOWPASS
			color = fixed4(lerp(i.floorColor.rgb, i.color.rgb, i.floorColor.a), 1.0f);
			o.Smoothness = _Smoothness00;
			o.Specular = _SpecColor00;
			go.Subsurface = _Subsurface00;
		#endif
	#else
		fixed2 uv = i.uv;

		float2 offset;
		float sizeOffset;

		#ifdef GRASS_TEXTURE_ATLAS
			float width, height;
			uint atlasRow, atlasColumn;
		#endif

		#if !defined(ONE_GRASS_TYPE)
		switch(i.texIndex)
		{
			case 0:
		#endif
				//Apply the color leakage preventing _AtlasOffset
				offset = float2(_AtlasOffset00, _AtlasOffset00);
				sizeOffset = 1 - 2 * _AtlasOffset00;
				uv = offset + uv * sizeOffset;

				#ifdef GRASS_TEXTURE_ATLAS
					width = 1.0 / _TextureAtlasWidth00;
					height = 1.0 / _TextureAtlasHeight00;
					atlasRow = i.textureAtlasIndex / _TextureAtlasWidth00;
					atlasColumn = i.textureAtlasIndex % _TextureAtlasWidth00;

					uv.x *= width;
					uv.y *= height;
					uv.x += atlasColumn * width;
					uv.y += atlasRow * height;
				#endif

				color = tex2D(_GrassTex00, uv);
				#if !defined(SHADOWPASS)
					o.Smoothness = _Smoothness00;
					o.Specular = _SpecColor00;
					go.Subsurface = _Subsurface00;
				#endif
		#if !defined(ONE_GRASS_TYPE)
				break;

			case 1:
				//Apply the color leakage preventing _AtlasOffset
				offset = float2(_AtlasOffset01, _AtlasOffset01);
				sizeOffset = 1 - 2 * _AtlasOffset01;
				uv = offset + uv * sizeOffset;

				#ifdef GRASS_TEXTURE_ATLAS
					width = 1.0 / _TextureAtlasWidth01;
					height = 1.0 / _TextureAtlasHeight01;
					atlasRow = i.textureAtlasIndex / _TextureAtlasWidth01;
					atlasColumn = i.textureAtlasIndex % _TextureAtlasWidth01;

					uv.x *= width;
					uv.y *= height;
					uv.x += atlasColumn * width;
					uv.y += atlasRow * height;
				#endif

				color = tex2D(_GrassTex01, uv);
				#if !defined(SHADOWPASS)
					o.Smoothness = _Smoothness01;
					o.Specular = _SpecColor01;
					go.Subsurface = _Subsurface01;
				#endif
				break;
		
		#if !defined(TWO_GRASS_TYPES)
			case 2:
				//Apply the color leakage preventing _AtlasOffset
				offset = float2(_AtlasOffset02, _AtlasOffset02);
				sizeOffset = 1 - 2 * _AtlasOffset02;
				uv = offset + uv * sizeOffset;

				#ifdef GRASS_TEXTURE_ATLAS
					width = 1.0 / _TextureAtlasWidth02;
					height = 1.0 / _TextureAtlasHeight02;
					atlasRow = i.textureAtlasIndex / _TextureAtlasWidth02;
					atlasColumn = i.textureAtlasIndex % _TextureAtlasWidth02;

					uv.x *= width;
					uv.y *= height;
					uv.x += atlasColumn * width;
					uv.y += atlasRow * height;
				#endif

				color = tex2D(_GrassTex02, uv);
				#if !defined(SHADOWPASS)
					o.Smoothness = _Smoothness02;
					o.Specular = _SpecColor02;
					go.Subsurface = _Subsurface02;
				#endif
				break;
		
		#if !defined(THREE_GRASS_TYPES)
			case 3:
				//Apply the color leakage preventing _AtlasOffset
				offset = float2(_AtlasOffset03, _AtlasOffset03);
				sizeOffset = 1 - 2 * _AtlasOffset03;
				uv = offset + uv * sizeOffset;

				#ifdef GRASS_TEXTURE_ATLAS
					width = 1.0 / _TextureAtlasWidth03;
					height = 1.0 / _TextureAtlasHeight03;
					atlasRow = i.textureAtlasIndex / _TextureAtlasWidth03;
					atlasColumn = i.textureAtlasIndex % _TextureAtlasWidth03;

					uv.x *= width;
					uv.y *= height;
					uv.x += atlasColumn * width;
					uv.y += atlasRow * height;
				#endif

				color = tex2D(_GrassTex03, uv);
				#if !defined(SHADOWPASS)
					o.Smoothness = _Smoothness03;
					o.Specular = _SpecColor03;
					go.Subsurface = _Subsurface03;
				#endif
				break;
		#endif
		#endif

			default:
				discard;
				break;
		}
		#endif

		color = fixed4(lerp(i.floorColor.rgb, color * i.color.rgb, i.floorColor.a), color.a);

		//Cuts off the texture when texture alpha is smaller than the smoothing/Texture cutoff value
		fixed cutoff = lerp(1, _TextureCutoff, i.color.a);
		#if defined(SHADOWPASS) || defined(GRASS_PASS_DEFERRED) || defined(GRASS_FALLBACK_RENDERER)
			clip(color.a - cutoff);
		#else
			color.a = (color.a - cutoff) / max(fwidth(color.a), 0.0001) + 0.5f;
		#endif
	#endif // !SIMPLE_GRASS

	o.Albedo = color.rgb;
	o.Alpha = color.a;
}

#endif