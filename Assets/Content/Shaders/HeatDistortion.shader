// Марево от взрыва: картинка за сферой дрожит и смещается кольцом, бегущим от центра.
// Берёт снимок непрозрачной сцены (Opaque Texture в настройках URP); без него эффект не показывается
Shader "RacingProject/HeatDistortion"
{
    Properties
    {
        _Strength ("Strength", Range(0, 0.1)) = 0.03
        _Progress ("Progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Back
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Progress;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceNormalizeViewDir(p.positionWS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.screenPos.xy / i.screenPos.w;
                // Кромка сферы (где нормаль перпендикулярна взгляду) искажает сильнее всего, центр — слабо
                float facing = saturate(dot(normalize(i.normalWS), i.viewWS));
                float rim = facing * (1 - facing) * 4;
                float wave = sin(i.positionOS.y * 40 + i.positionOS.x * 31 + _Time.y * 25) * 0.5
                           + sin(i.positionOS.z * 37 - _Time.y * 19) * 0.5;
                float fade = 1 - _Progress;
                float2 offset = float2(wave, -wave * 0.6) * _Strength * rim * fade;
                return half4(SampleSceneColor(uv + offset), 1);
            }
            ENDHLSL
        }
    }
}
