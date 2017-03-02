// textures
    Texture2D gDiffuseMap;
	Texture2D gDepthMap;
    Texture2D gBaseReflectionMap;
    Texture2D gNormalMap;
	Texture2D gFirstStepMap;

    SamplerState samPoint {
        Filter = MIN_MAG_MIP_POINT;
        AddressU = CLAMP;
        AddressV = CLAMP;
    };

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};
    
// input resources
    cbuffer cbPerFrame : register(b0) {
		matrix gCameraProjInv;
		matrix gCameraProj;

        matrix gWorldViewProjInv;
        matrix gWorldViewProj;
        float3 gEyePosW;

		float gStartFrom;
		float gFixMultipler;
		float gOffset;
		float gGlowFix;
		float gDistanceThreshold;

		// bool gTemporary;
    }

// fn structs
    struct VS_IN {
        float3 PosL : POSITION;
        float2 Tex  : TEXCOORD;
    };

    struct PS_IN {
        float4 PosH : SV_POSITION;
		float2 Tex  : TEXCOORD0;
    };

// functions
    float3 GetPosition(float2 uv, float depth){
        float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
        return position.xyz / position.w;
    }

    float GetDepth(float2 uv, SamplerState s){
        return gDepthMap.SampleLevel(s, uv, 0).x;
    }

    float3 GetUv(float3 position){
        float4 pVP = mul(float4(position, 1.0f), gWorldViewProj);
        pVP.xy = float2(0.5f, 0.5f) + float2(0.5f, -0.5f) * pVP.xy / pVP.w;
        return float3(pVP.xy, pVP.z / pVP.w);
    }

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;
        return vout;
    }

	float3 GetNormal(float2 coords, SamplerState s) {
		return gNormalMap.Sample(s, coords).xyz;
	}

// new
    #define ITERATIONS 30

	float4 GetReflection(float2 coords, float3 normal, SamplerState s) {
		float depth = GetDepth(coords, s);
		float3 position = GetPosition(coords, depth);
		float3 viewDir = normalize(position - gEyePosW);
		float3 reflectDir = normalize(reflect(viewDir, normal));

		//depth = pow(depth, 20);
		//return float4(depth, depth, depth, 1.0);

		float3 newUv = 0;
		float L = gStartFrom;

		float3 calculatedPosition, newPosition;
		float newL;

		for (int i = 0; i < ITERATIONS; i++) {
			calculatedPosition = position + reflectDir * L;

			newUv = GetUv(calculatedPosition);
			newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy, s));

			float newL = length(position - newPosition);
			newL = L + min(newL - L, gOffset + L * gGlowFix);

			L = L * (1 - gFixMultipler) + newL * gFixMultipler;
		}

		calculatedPosition = position + reflectDir * L;
		newUv = GetUv(calculatedPosition);
		newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy, s));

		float fresnel = saturate(3.2 * pow(1 + dot(viewDir, normal), 2));
		float quality = 1 - saturate(abs(length(calculatedPosition - newPosition)) / gDistanceThreshold);

		float alpha = fresnel * quality * saturate(min(newUv.x, 1 - newUv.x) / 0.1) * saturate(min(newUv.y, 1 - newUv.y) / 0.1);
		return float4(newUv.xy - coords, saturate(L / quality), alpha);
	}

	float4 SslrFn(float2 coords, SamplerState s) {
		return GetReflection(coords, GetNormal(coords, s), s);
	}

    float4 ps_Sslr(PS_IN pin) : SV_Target {
		return SslrFn(pin.Tex, samPoint);
    }

    technique11 Sslr {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Sslr() ) );
        }
    }

    float4 ps_Sslr_LinearFiltering(PS_IN pin) : SV_Target {
		return SslrFn(pin.Tex, samLinear);
    }

    technique11 Sslr_LinearFiltering {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Sslr_LinearFiltering() ) );
        }
    }
	
	cbuffer POISSON_DISKS {
		float2 poissonDisk[16] = {
			float2(-0.94201624, -0.39906216),
			float2(0.94558609, -0.76890725),
			float2(-0.094184101, -0.92938870),
			float2(0.34495938, 0.29387760),
			float2(-0.91588581, 0.45771432),
			float2(-0.81544232, -0.87912464),
			float2(-0.38277543, 0.27676845),
			float2(0.97484398, 0.75648379),
			float2(0.44323325, -0.97511554),
			float2(0.53742981, -0.47373420),
			float2(-0.26496911, -0.41893023),
			float2(0.79197514, 0.19090188),
			float2(-0.24188840, 0.99706507),
			float2(-0.81409955, 0.91437590),
			float2(0.19984126, 0.78641367),
			float2(0.14383161, -0.14100790)
		};
	};

	float4 GetReflection(float2 baseUv, float2 uv, float blur) {
		float4 reflection = (float4)0;

		for (float i = 0; i < 16; i++) {
			float2 uvOffset = poissonDisk[i] * blur;
			reflection += float4(gDiffuseMap.SampleLevel(samLinear, uv + uvOffset, 0).rgb, gFirstStepMap.SampleLevel(samLinear, baseUv + uvOffset, 0).a);
		}

		return reflection / 16;
	}

	float4 FinalStepFn(float2 coords, SamplerState s) {
		float4 firstStep = gFirstStepMap.SampleLevel(s, coords, 0);
		// return firstStep;

		float2 reflectedUv = coords + firstStep.xy;

		float3 diffuseColor = gDiffuseMap.SampleLevel(s, coords, 0).rgb;
		float specularExp = gNormalMap.Sample(s, coords).a;

		float4 baseReflection = gBaseReflectionMap.SampleLevel(s, coords, 0);
		float blur = saturate(firstStep.b / specularExp / 5.0 - 0.01);

		// return float4(diffuseColor - baseReflection.rgb * baseReflection.a, 1.0);

		float4 reflection;
		if (blur < 0.001) {
			reflection = float4(gDiffuseMap.SampleLevel(s, reflectedUv, 0).rgb, gFirstStepMap.SampleLevel(s, coords, 0).a);
		} else {
			reflection = GetReflection(coords, reflectedUv, blur);
		}

		float a = reflection.a * baseReflection.a;
		return float4(diffuseColor + (reflection.rgb - baseReflection.rgb) * a, 1.0);
	}

	float4 ps_FinalStep(PS_IN pin) : SV_Target {
		return FinalStepFn(pin.Tex, samPoint);
	}

    technique11 FinalStep {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_FinalStep() ) );
        }
    }

	float4 ps_FinalStep_LinearFiltering(PS_IN pin) : SV_Target {
		return FinalStepFn(pin.Tex, samLinear);
	}

    technique11 FinalStep_LinearFiltering {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_FinalStep_LinearFiltering() ) );
        }
    }