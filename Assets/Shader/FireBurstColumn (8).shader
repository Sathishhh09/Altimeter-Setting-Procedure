Shader "Custom/FireBurstColumn"
{
    Properties
    {
        [Header(Combustion Control)]
        _CombustionActive ("Combustion Active (0-1)", Range(0,1)) = 1
        _Intensity ("Intensity", Range(0,1)) = 1

        [Header(Burner Ring)]
        _FlameCount ("Flame Count (burner holes around ring)", Range(1,24)) = 8
        _TongueWidth ("Tongue Width (fraction of wedge used, 0-1)", Range(0.1,1)) = 0.55

        [Header(Flame Shape Outer Tongue)]
        _FlameHeight ("Flame Height", Range(0.1,3)) = 1.0
        _BaseWidth ("Base Width (silhouette taper)", Range(0.1,2)) = 1.0
        _TipTaper ("Tip Taper Sharpness", Range(0.5,6)) = 2.5
        _LickStrength ("Lick Curl Strength (sway/waver)", Range(0,1)) = 0.4

        [Header(Turbulence and Flow Noise)]
        _FlowSpeed ("Upward Flow Speed", Float) = 1.2
        _WarpSpeed ("Domain Warp Speed", Float) = 0.6
        _WarpStrength ("Domain Warp Strength", Range(0,1)) = 0.35
        _DetailScale ("Fine Detail Noise Scale", Float) = 6
        _FlickerSpeed ("Flicker Speed", Float) = 4
        _PerFlameVariation ("Per-Flame Variation (each tongue flickers independently)", Range(0,1)) = 0.6

        [Header(Color Ramp Base To Tip Outer Tongue)]
        _ColorBase ("Base Color", Color) = (1,0.6,0.1,1)
        _ColorMid ("Mid Color (yellow-orange)", Color) = (1,0.55,0.05,1)
        _ColorTip ("Tip Color (cooling, fades to smoke)", Color) = (0.6,0.08,0.02,1)
        _EmissionStrength ("Emission Strength", Float) = 3.5

        [Header(Edge)]
        _EdgeSoftness ("Edge Softness", Range(0.01,0.5)) = 0.1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _CombustionActive;
                float _Intensity;
                float _FlameCount;
                float _TongueWidth;
                float _FlameHeight;
                float _BaseWidth;
                float _TipTaper;
                float _LickStrength;
                float _FlowSpeed;
                float _WarpSpeed;
                float _WarpStrength;
                float _DetailScale;
                float _FlickerSpeed;
                float _PerFlameVariation;
                float4 _ColorBase;
                float4 _ColorMid;
                float4 _ColorTip;
                float _EmissionStrength;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float Hash1(float p)
            {
                return frac(sin(p * 127.1) * 43758.5453123);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(Hash2(i + float2(0,0)), f - float2(0,0));
                float b = dot(Hash2(i + float2(1,0)), f - float2(1,0));
                float c = dot(Hash2(i + float2(0,1)), f - float2(0,1));
                float d = dot(Hash2(i + float2(1,1)), f - float2(1,1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            float FBM(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * ValueNoise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float time = _Time.y;
                float3 p = IN.positionOS;

                // ---------------------------------------------------------------
                // Split the ring into _FlameCount equal wedges using object-space
                // angle. Each wedge gets its own independent flame with a local
                // 0-1 "tongueX" coordinate centered on the wedge, so every flame
                // is a self-contained single tongue rather than a slice of one
                // big radial pattern.
                // ---------------------------------------------------------------
                float ang = atan2(p.z, p.x);          // -PI..PI
                float angle01 = ang / (2.0 * PI) + 0.5; // 0..1 around the ring

                float wedgeCount = max(_FlameCount, 1.0);
                float wedgePos = angle01 * wedgeCount;   // which wedge + position inside it
                float flameID = floor(wedgePos);         // unique index per flame, for per-flame variation
                float tongueX = frac(wedgePos) - 0.5;     // -0.5..0.5, centered on this flame's tongue

                // Squeeze into the tongue's actual width, area outside is empty space between flames
                tongueX /= max(_TongueWidth, 0.001);

                // Per-flame random seed so each burner hole flickers/sways independently
                float seed = Hash1(flameID + 11.7);
                float flamePhase = seed * 6.2831;
                float flameSpeedMul = lerp(1.0 - _PerFlameVariation * 0.4, 1.0 + _PerFlameVariation * 0.4, seed);

                // Height runs up local Y, normalized by FlameHeight
                float heightUV = saturate(p.y / max(_FlameHeight, 0.001));

                // --- Domain warp for organic curl, varies per flame via seed offset ---
                float2 baseCoord = float2(tongueX * 2.0, heightUV * 3.0) + seed * 13.0;
                float2 warpCoord = baseCoord + float2(0, -time * _WarpSpeed * flameSpeedMul);
                float2 warp = float2(
                    FBM(warpCoord, 3),
                    FBM(warpCoord + 17.3, 3)
                ) - 0.5;

                float2 flowUV = float2(tongueX * 3.0, heightUV * 4.0) + seed * 13.0;
                flowUV += warp * _WarpStrength;
                flowUV.y -= time * _FlowSpeed * flameSpeedMul;

                float2 detailUV = float2(tongueX, heightUV) * _DetailScale + seed * 13.0
                                   + float2(0, -time * _FlowSpeed * 1.5 * flameSpeedMul);
                float detail = FBM(detailUV, 3);

                float mainNoise = FBM(flowUV, 4);
                float combined = saturate(mainNoise * 0.7 + detail * 0.3);
                combined = saturate((combined - 0.1) * 1.25); // mild contrast; FBM output centers well below 1.0

                // --- Outer tongue silhouette: narrow base, tapered tip, gentle sway ---
                float sway = sin(heightUV * 3.14159 * 1.3 + time * _FlickerSpeed * 0.3 * flameSpeedMul + flamePhase)
                             * _LickStrength * heightUV * 0.5;
                float localX = tongueX - sway;

                float taper = (1.0 - pow(heightUV, _TipTaper)) * _BaseWidth;
                float widthFalloff = 1.0 - saturate(abs(localX) / max(taper, 0.001));
                widthFalloff = smoothstep(0.0, 1.0, widthFalloff);

                float coverage = widthFalloff * lerp(0.35, 1.0, combined);

                // Flicker: fast subtle per-flame brightness variation
                float flicker = 0.9 + 0.1 * sin(time * _FlickerSpeed * flameSpeedMul + flamePhase
                                 + FBM(baseCoord * 2.0, 2) * 10.0);
                coverage *= flicker;

                // Tip falloff so it fades rather than cutting off hard
                coverage *= smoothstep(1.0, 0.8, heightUV);

                float edge = smoothstep(0.5 - _EdgeSoftness, 0.5 + _EdgeSoftness, coverage);

                // --- Color ramp base -> mid -> tip for the outer tongue ---
                float colorT = saturate(heightUV * 1.3 + (1.0 - combined) * 0.2);
                float3 outerCol = lerp(_ColorBase.rgb, _ColorMid.rgb, saturate(colorT * 2.0));
                outerCol = lerp(outerCol, _ColorTip.rgb, saturate((colorT - 0.5) * 2.0));

                float activeAmount = _CombustionActive * _Intensity;

                float3 col = outerCol * _EmissionStrength * edge;
                float alpha = edge;

                col *= activeAmount;
                alpha *= activeAmount;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
