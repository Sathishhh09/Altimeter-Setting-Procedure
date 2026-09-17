Shader "Skybox/BlendedCubemap3D"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Exposure", Float) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        
        [NoScaleOffset] _Tex1 ("First Cubemap", CUBE) = "grey" {}
        [NoScaleOffset] _Tex2 ("Second Cubemap", CUBE) = "grey" {}
        
        _Blend ("Blend Factor", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off 
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            samplerCUBE _Tex1;
            half4 _Tex1_HDR;
            
            samplerCUBE _Tex2;
            half4 _Tex2_HDR;

            half4 _Tint;
            half _Exposure;
            float _Blend;
            float _Rotation;
            float4x4 _RotationMatrix; // Received from C# script

            struct appdata_t 
            {
                float4 vertex : POSITION;
            };

            struct v2f 
            {
                float4 vertex : SV_POSITION;
                float3 skyDir : TEXCOORD0;
            };

            // Fallback function to rotate single Y-axis if matrix is identity
            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Multiply direction vector by 3D rotation matrix sent from script
                float3 dir = mul((float3x3)_RotationMatrix, v.vertex.xyz);

                // Fallback to Y-axis rotation if matrix is zeroed/unassigned
                if (length(dir) == 0.0)
                {
                    dir = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                }

                o.skyDir = dir;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.skyDir);

                // Sample cubemaps with HDR decoding
                half4 tex1_raw = texCUBE(_Tex1, dir);
                half4 tex2_raw = texCUBE(_Tex2, dir);

                half3 tex1 = DecodeHDR(tex1_raw, _Tex1_HDR);
                half3 tex2 = DecodeHDR(tex2_raw, _Tex2_HDR);

                // Blend between cubemaps based on _Blend factor
                half3 blendedTex = lerp(tex1, tex2, _Blend);

                // Apply exposure, tinting, and color space compensation
                half3 c = blendedTex * _Tint.rgb * unity_ColorSpaceDouble.rgb;
                c *= _Exposure;

                return half4(c, 1.0);
            }
            ENDCG
        }
    }
}