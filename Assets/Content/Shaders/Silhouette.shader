// Силуэт врага за стеной: рисуется только там, где машина закрыта чем-то (ZTest Greater), плоским цветом
// с подсветкой контура. Кладётся вторым материалом на копию сетки кузова (EnemySilhouette)
Shader "RacingProject/Silhouette"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.4, 0.25, 0.15, 0.35)
        _RimPower ("Rim power", Range(0.5, 8)) = 2
        _DepthOffset ("Depth offset, m", Range(0, 20)) = 7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Greater
        Cull Back
        Blend SrcAlpha One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RimPower;
                float _DepthOffset;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                // Глубину сдвигаем к камере: части кузова, закрытые самой машиной (машина в игре длиной ~10 м),
                // силуэт не дают — срабатывают только препятствия заметно ближе. Положение на экране не меняется
                float3 toCamera = GetCameraPositionWS() - p.positionWS;
                float distance = length(toCamera);
                float4 shifted = TransformWorldToHClip(p.positionWS + toCamera / max(distance, 1e-3) * min(_DepthOffset, distance * 0.5));
                o.positionCS.z = shifted.z / shifted.w * p.positionCS.w;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceNormalizeViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float rim = 1 - saturate(dot(normalize(i.normalWS), i.viewWS));
                float a = _Color.a * (0.35 + 0.65 * pow(rim, _RimPower));
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
