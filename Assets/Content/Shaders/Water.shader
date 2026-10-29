// Вода озера: непрозрачная тёмная гладь с бегущей рябью (сумма синусоид в мировых координатах),
// отражением неба по Френелю и длинными бликами от луны, фонарей и фар (все источники Forward+).
// Рябь к горизонту сглаживается, чтобы не рябило в VR
Shader "RacingProject/Water"
{
    Properties
    {
        _DeepColor ("Deep color", Color) = (0.015, 0.035, 0.04, 1)
        _ShallowColor ("Scatter color", Color) = (0.04, 0.09, 0.08, 1)
        _WaveScale ("Wave scale", Float) = 1
        _WaveStrength ("Wave strength", Range(0, 1)) = 0.35
        _WaveSpeed ("Wave speed", Float) = 1
        _Roughness ("Reflection roughness", Range(0, 1)) = 0.06
        _Reflection ("Reflection strength", Range(0, 2)) = 1
        _SpecPower ("Highlight sharpness", Float) = 400
        _SpecStrength ("Highlight strength", Float) = 4
        _FadeDistance ("Ripple fade distance", Float) = 250
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                float _WaveScale;
                float _WaveStrength;
                float _WaveSpeed;
                float _Roughness;
                float _Reflection;
                float _SpecPower;
                float _SpecStrength;
                float _FadeDistance;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fog : TEXCOORD1;
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
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Наклон поверхности от одной волны: направление, длина волны (м), амплитуда (м), скорость (м/с)
            float2 Wave(float2 p, float2 dir, float length, float amplitude, float speed, float time)
            {
                float k = 6.2831853 / length;
                float phase = (dot(dir, p) - speed * time) * k;
                return dir * (k * amplitude * cos(phase));
            }

            float3 WaterNormal(float2 p, float fade)
            {
                float t = _Time.y * _WaveSpeed;
                p /= _WaveScale;
                float2 g = 0;
                g += Wave(p, normalize(float2(1.0, 0.3)), 9.0, 0.10, 1.6, t);
                g += Wave(p, normalize(float2(-0.6, 1.0)), 5.3, 0.06, 1.2, t);
                g += Wave(p, normalize(float2(0.2, -1.0)), 3.1, 0.035, 0.9, t);
                g += Wave(p, normalize(float2(-1.0, -0.45)), 1.7, 0.018, 0.7, t);
                g += Wave(p, normalize(float2(0.75, 0.9)), 0.9, 0.009, 0.55, t);
                g += Wave(p, normalize(float2(-0.35, 0.2)), 0.55, 0.005, 0.4, t);
                g *= _WaveStrength * fade;
                return normalize(float3(-g.x, 1.0, -g.y));
            }

            half3 Highlight(Light light, float3 normal, float3 view, half attenuation)
            {
                float3 h = normalize(light.direction + view);
                float spec = pow(saturate(dot(normal, h)), _SpecPower);
                return light.color * (spec * _SpecStrength * attenuation);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 toEye = GetCameraPositionWS() - i.positionWS;
                float distance = length(toEye);
                float3 view = toEye / distance;
                float fade = saturate(1.0 - distance / _FadeDistance) * 0.8 + 0.2;
                float3 normal = WaterNormal(i.positionWS.xz, fade);

                // Френель (F0 воды 0,02): у ног видна тёмная толща, к горизонту — отражение неба
                float cosTheta = saturate(dot(normal, view));
                float fresnel = 0.02 + 0.98 * pow(1.0 - cosTheta, 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-view, normal), _Roughness, 1.0) * _Reflection;

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;
                half3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(normal.y * 0.5 - 0.3))
                    * (mainLight.color * (0.3 + 0.7 * saturate(mainLight.direction.y)) + SampleSH(normal) * 0.6);
                half3 color = lerp(body, reflection, fresnel);
                color += Highlight(mainLight, normal, view, shadow);

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normal;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    color += Highlight(light, normal, view, light.distanceAttenuation * light.shadowAttenuation);
                }
                #endif
                LIGHT_LOOP_BEGIN(count)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    // Блик фонаря на ряби тянется дорожкой: мягкая часть пошире добавляется к острой
                    half att = light.distanceAttenuation * light.shadowAttenuation;
                    color += Highlight(light, normal, view, att);
                    float3 h = normalize(light.direction + view);
                    color += light.color * (pow(saturate(dot(normal, h)), _SpecPower * 0.08) * 0.15 * att);
                LIGHT_LOOP_END
                #endif

                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
