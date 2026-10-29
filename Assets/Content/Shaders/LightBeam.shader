// Видимый луч прожектора: конус вдоль +Z, ярче к оси и к прожектору (альфа в цвете вершин),
// края мягкие — гаснут там, где поверхность конуса идёт вдоль взгляда; вплотную к камере луч исчезает
Shader "RacingProject/LightBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.96, 0.86, 1)
        _EdgePower ("Edge softness", Range(0.5, 6)) = 2
        _NearFade ("Near fade distance", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _EdgePower;
                float _NearFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                // Нормаль конуса — от оси наружу
                o.normalWS = TransformObjectToWorldNormal(normalize(float3(v.positionOS.xy, 0.0001)));
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 view = GetCameraPositionWS() - i.positionWS;
                float distance = length(view);
                float facing = abs(dot(normalize(i.normalWS), view / distance));
                float edge = pow(facing, _EdgePower);
                float near = saturate(distance / _NearFade - 0.25);
                return half4(_Color.rgb, i.color.a * edge * near);
            }
            ENDHLSL
        }
    }
}
