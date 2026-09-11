Shader "Custom/PulseFresnel"
{
    Properties
    {
        [MainColor]
        _base_Color ("Glass Base Color", Color) = (0.05, 0.35, 0.45, 1)

        _emission_color ("Emission Color", Color) = (0.0, 0.8, 1.0, 1)

        _glow_strength ("Glow Strength", Range(0, 10)) = 2.5

        _pulse_speed ("Pulse Speed", Range(0, 10)) = 1.0

        _alpha_value ("Glass Transparency", Range(0, 1)) = 0.35

        _FresnelPower ("Fresnel Power", Range(0.1, 5)) = 2.0

        _FresnelStrength ("Fresnel Strength", Range(0, 10)) = 3.0

        _SpecularStrength ("Specular Strength", Range(0, 5)) = 1.5

        _Smoothness ("Smoothness", Range(0, 1)) = 0.9

        _RefractionTint ("Glass Tint", Color) = (0.0, 0.5, 0.65, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha

        ZWrite Off

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

                float _FresnelPower;
                float _FresnelStrength;

                float _SpecularStrength;
                float _Smoothness;

                float4 _RefractionTint;

            CBUFFER_END


            // =========================================================
            // VERTEX
            // =========================================================

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = positionInputs.positionCS;

                OUT.positionWS = positionInputs.positionWS;

                OUT.normalWS =
                    normalize(normalInputs.normalWS);

                return OUT;
            }


            // =========================================================
            // FRAGMENT
            // =========================================================

            half4 Frag(Varyings IN) : SV_Target
            {
                // -----------------------------------------------------
                // 1. NORMAL
                // -----------------------------------------------------

                float3 normalWS =
                    normalize(IN.normalWS);


                // -----------------------------------------------------
                // 2. VIEW DIRECTION
                // -----------------------------------------------------

                float3 viewDir =
                    normalize(
                        GetWorldSpaceViewDir(IN.positionWS)
                    );


                // =====================================================
                // PULSE
                // =====================================================

                // -----------------------------------------------------
                // 3. TIME
                // -----------------------------------------------------

                float timeValue = _Time.y;


                // -----------------------------------------------------
                // 4. TIME × PULSE SPEED
                // -----------------------------------------------------

                float pulseTime =
                    timeValue * _pulse_speed;


                // -----------------------------------------------------
                // 5. SINE
                // -----------------------------------------------------

                float sineValue =
                    sin(pulseTime);


                // -----------------------------------------------------
                // 6. REMAP -1..1 → 0..1
                // -----------------------------------------------------

                float pulse =
                    (sineValue + 1.0) * 0.5;


                // Make pulse smoother
                pulse =
                    smoothstep(
                        0.0,
                        1.0,
                        pulse
                    );


                // =====================================================
                // FRESNEL
                // =====================================================

                // -----------------------------------------------------
                // 7. VIEW ANGLE
                // -----------------------------------------------------

                float viewDot =
                    saturate(
                        dot(
                            normalWS,
                            viewDir
                        )
                    );


                // -----------------------------------------------------
                // 8. FRESNEL
                // -----------------------------------------------------

                float fresnel =
                    pow(
                        1.0 - viewDot,
                        _FresnelPower
                    );


                // -----------------------------------------------------
                // 9. STRONG GLASS EDGE
                // -----------------------------------------------------

                float glassEdge =
                    fresnel *
                    _FresnelStrength;


                glassEdge =
                    saturate(glassEdge);


                // =====================================================
                // BASE GLASS COLOR
                // =====================================================

                float3 glassColor =
                    _base_Color.rgb;


                // =====================================================
                // EMISSION
                // =====================================================

                // -----------------------------------------------------
                // 10. EMISSION COLOR
                // -----------------------------------------------------

                float3 emission =
                    _emission_color.rgb *
                    _glow_strength;


                // -----------------------------------------------------
                // 11. PULSE EMISSION
                // -----------------------------------------------------

                float3 pulseEmission =
                    emission *
                    pulse;


                // -----------------------------------------------------
                // 12. EDGE EMISSION
                // -----------------------------------------------------

                float3 edgeEmission =
                    emission *
                    glassEdge;


                // =====================================================
                // GLASS SPECULAR
                // =====================================================

                // -----------------------------------------------------
                // 13. HALF VECTOR
                // -----------------------------------------------------

                float3 lightDirection =
                    normalize(
                        float3(
                            0.3,
                            0.8,
                            0.5
                        )
                    );


                float3 halfVector =
                    normalize(
                        lightDirection +
                        viewDir
                    );


                // -----------------------------------------------------
                // 14. SPECULAR
                // -----------------------------------------------------

                float specular =
                    pow(
                        saturate(
                            dot(
                                normalWS,
                                halfVector
                            )
                        ),
                        lerp(
                            8.0,
                            128.0,
                            _Smoothness
                        )
                    );


                specular *=
                    _SpecularStrength;


                // =====================================================
                // COMBINE GLASS
                // =====================================================

                // -----------------------------------------------------
                // 15. BASE TRANSPARENT COLOR
                // -----------------------------------------------------

                float3 finalColor =
                    glassColor *
                    0.25;


                // -----------------------------------------------------
                // 16. REFRACTION / GLASS TINT
                // -----------------------------------------------------

                finalColor +=
                    _RefractionTint.rgb *
                    fresnel *
                    0.5;


                // -----------------------------------------------------
                // 17. PULSING EMISSION
                // -----------------------------------------------------

                finalColor +=
                    pulseEmission *
                    0.35;


                // -----------------------------------------------------
                // 18. EDGE GLOW
                // -----------------------------------------------------

                finalColor +=
                    edgeEmission;


                // -----------------------------------------------------
                // 19. SPECULAR HIGHLIGHT
                // -----------------------------------------------------

                finalColor +=
                    specular *
                    _emission_color.rgb;


                // =====================================================
                // ALPHA
                // =====================================================

                // Base transparency
                float alpha =
                    _alpha_value;


                // More opaque around the Fresnel edge
                alpha +=
                    fresnel *
                    0.35;


                // Small pulse contribution
                alpha +=
                    pulse *
                    0.05;


                alpha =
                    saturate(alpha);


                // =====================================================
                // FINAL
                // =====================================================

                return half4(
                    finalColor,
                    alpha
                );
            }

            ENDHLSL
        }
    }
}