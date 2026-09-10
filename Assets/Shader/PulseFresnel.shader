Shader "Custom/PulseFresnel"
{
    Properties
    {
        [MainColor]
        _base_Color ("Base Color", Color) = (1, 1, 1, 1)

        _emission_color ("Emission Color", Color) = (1, 1, 1, 1)

        _glow_strength ("Glow Strength", Range(0, 10)) = 1.0

        _pulse_speed ("Pulse Speed", Range(0, 10)) = 1.0

        _alpha_value ("Alpha", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)

                float4 _base_Color;
                float4 _emission_color;

                float _glow_strength;
                float _pulse_speed;
                float _alpha_value;

            CBUFFER_END


            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalize(normalInputs.normalWS);

                return OUT;
            }


            half4 Frag(Varyings IN) : SV_Target
            {
                // --------------------------------------------------
                // 1. TIME
                // --------------------------------------------------

                float timeValue = _Time.y;


                // --------------------------------------------------
                // 2. TIME × PULSE SPEED
                // --------------------------------------------------

                float pulseTime =
                    timeValue * _pulse_speed;


                // --------------------------------------------------
                // 3. SINE
                // --------------------------------------------------

                float sineValue =
                    sin(pulseTime);


                // --------------------------------------------------
                // 4. REMAP
                //
                // Shader Graph:
                // Input  = -1 to 1
                // Output =  0 to 1
                // --------------------------------------------------

                float pulse =
                    (sineValue + 1.0) * 0.5;


                // --------------------------------------------------
                // 5. VIEW DIRECTION
                // --------------------------------------------------

                float3 viewDir =
                    normalize(GetWorldSpaceViewDir(IN.positionWS));


                // --------------------------------------------------
                // 6. FRESNEL
                // --------------------------------------------------

                float fresnel =
                    1.0 - saturate(
                        dot(
                            normalize(IN.normalWS),
                            viewDir
                        )
                    );


                // Your Shader Graph Fresnel Power = 1
                fresnel = pow(fresnel, 1.0);


                // --------------------------------------------------
                // 7. EMISSION COLOR × GLOW STRENGTH
                // --------------------------------------------------

                float3 emission =
                    _emission_color.rgb *
                    _glow_strength;


                // --------------------------------------------------
                // 8. EMISSION × FRESNEL
                // --------------------------------------------------

                float3 fresnelEmission =
                    emission *
                    fresnel;


                // --------------------------------------------------
                // 9. × PULSE
                // --------------------------------------------------

                float3 finalColor =
                    fresnelEmission *
                    pulse;


                // --------------------------------------------------
                // 10. BASE COLOR
                // --------------------------------------------------

                finalColor *= _base_Color.rgb;


                return half4(
                    finalColor,
                    _alpha_value
                );
            }

            ENDHLSL
        }
    }
}