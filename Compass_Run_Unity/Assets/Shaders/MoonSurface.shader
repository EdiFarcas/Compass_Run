Shader "Compass Run/Moon Surface"
{
    Properties
    {
        _BaseColor ("Moon Color", Color) = (0.8, 0.85, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalOS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalOS = input.normalOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalOS);
                float mottling = sin(normal.x * 27 + sin(normal.z * 14)) * sin(normal.y * 31 + normal.z * 9);
                float craters = smoothstep(.45, .9, mottling);
                float limb = pow(saturate(dot(normalize(input.normalWS), normalize(GetCameraPositionWS() - input.positionWS))), .3);
                float shade = (.72 + .2 * mottling - .28 * craters) * (.35 + .65 * limb);
                return half4(_BaseColor.rgb * shade, 1);
            }
            ENDHLSL
        }
    }
}
