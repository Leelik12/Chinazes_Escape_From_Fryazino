// Ударная волна: светящееся кольцо на плоскости, расходится и гаснет по _Progress (0–1)
Shader "RacingProject/Shockwave"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.5, 1.6, 0.8, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        _Width ("Ring width", Range(0.01, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Progress;
                float _Width;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2 - 1;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = length(i.uv);
                // Кольцо на радиусе, бегущем к краю квада, с мягкими краями; внутри — слабое свечение пыли
                float radius = _Progress;
                float ring = saturate(1 - abs(r - radius) / _Width);
                ring *= ring;
                float inner = saturate(1 - r / max(radius, 0.001)) * 0.15;
                float fade = (1 - _Progress) * (1 - _Progress);
                float edge = saturate((1 - r) * 8);
                return half4(_Color.rgb * (ring + inner) * fade * edge, 1);
            }
            ENDHLSL
        }
    }
}
