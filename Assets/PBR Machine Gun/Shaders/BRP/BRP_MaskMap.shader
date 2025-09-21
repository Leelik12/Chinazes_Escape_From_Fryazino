// Shader created with Shader Forge v1.40 
// Shader Forge (c) Freya Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.40;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,cpap:True,lico:1,lgpr:1,limd:3,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:True,hqlp:True,rprd:True,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:0,bdst:1,dpts:2,wrdp:True,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:False,qofs:0,qpre:1,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.1280277,fgcg:0.1953466,fgcb:0.2352941,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:3755,x:32719,y:32712,varname:node_3755,prsc:2|diff-8066-OUT,spec-5632-R,gloss-5632-A,normal-1711-OUT,emission-278-OUT,difocc-5632-G;n:type:ShaderForge.SFN_Tex2d,id:6037,x:32213,y:32130,ptovrint:False,ptlb:BaseMap,ptin:_BaseMap,varname:node_6037,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Multiply,id:8066,x:32423,y:32221,varname:node_8066,prsc:2|A-6037-RGB,B-1800-RGB,C-9769-OUT;n:type:ShaderForge.SFN_Color,id:1800,x:32213,y:32316,ptovrint:False,ptlb:Tint,ptin:_Tint,varname:node_1800,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:1;n:type:ShaderForge.SFN_Tex2d,id:9084,x:31397,y:33173,ptovrint:False,ptlb:MaskMap,ptin:_MaskMap,varname:node_9084,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:252,x:32229,y:33234,ptovrint:False,ptlb:NormalMap,ptin:_NormalMap,varname:node_252,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:3,isnm:True;n:type:ShaderForge.SFN_SwitchProperty,id:1711,x:32447,y:33126,ptovrint:False,ptlb:UseNormalMap,ptin:_UseNormalMap,varname:node_1711,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:True|A-7992-OUT,B-252-RGB;n:type:ShaderForge.SFN_NormalVector,id:7992,x:32229,y:33065,prsc:2,pt:False;n:type:ShaderForge.SFN_Append,id:5742,x:32144,y:32937,varname:node_5742,prsc:2|A-7501-OUT,B-571-OUT,C-9084-B,D-1988-OUT;n:type:ShaderForge.SFN_SwitchProperty,id:7875,x:32325,y:32869,ptovrint:False,ptlb:UseMaskMap,ptin:_UseMaskMap,varname:node_7875,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-5772-OUT,B-5742-OUT;n:type:ShaderForge.SFN_Slider,id:1841,x:31602,y:32718,ptovrint:False,ptlb:Metallic,ptin:_Metallic,varname:node_1841,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:1;n:type:ShaderForge.SFN_Slider,id:7809,x:31602,y:32961,ptovrint:False,ptlb:Smoothness,ptin:_Smoothness,varname:node_7809,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.5,max:1;n:type:ShaderForge.SFN_Slider,id:6806,x:31602,y:32810,ptovrint:False,ptlb:AO,ptin:_AO,varname:node_6806,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:1;n:type:ShaderForge.SFN_Append,id:5772,x:31960,y:32764,varname:node_5772,prsc:2|A-1841-OUT,B-6806-OUT,C-2751-OUT,D-7809-OUT;n:type:ShaderForge.SFN_Vector1,id:2751,x:31759,y:32883,varname:node_2751,prsc:2,v1:0;n:type:ShaderForge.SFN_ComponentMask,id:5632,x:32477,y:32869,varname:node_5632,prsc:2,cc1:0,cc2:1,cc3:2,cc4:3|IN-7875-OUT;n:type:ShaderForge.SFN_Slider,id:7068,x:31472,y:33096,ptovrint:False,ptlb:MetallicStrength,ptin:_MetallicStrength,varname:_Metallic_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:1;n:type:ShaderForge.SFN_Multiply,id:7501,x:31781,y:33143,varname:node_7501,prsc:2|A-7068-OUT,B-9084-R;n:type:ShaderForge.SFN_Slider,id:8458,x:31553,y:33290,ptovrint:False,ptlb:AOStrength,ptin:_AOStrength,varname:_AO_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:1;n:type:ShaderForge.SFN_Multiply,id:571,x:31866,y:33309,varname:node_571,prsc:2|A-8458-OUT,B-9084-G;n:type:ShaderForge.SFN_Slider,id:3782,x:31520,y:33550,ptovrint:False,ptlb:SmoothnessStrength,ptin:_SmoothnessStrength,varname:_Smoothness_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:1;n:type:ShaderForge.SFN_Multiply,id:1988,x:31832,y:33468,varname:node_1988,prsc:2|A-9084-A,B-3782-OUT;n:type:ShaderForge.SFN_ValueProperty,id:9769,x:32362,y:32388,ptovrint:False,ptlb:Intensity,ptin:_Intensity,varname:node_9769,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:1;n:type:ShaderForge.SFN_Tex2d,id:1773,x:32166,y:32484,ptovrint:False,ptlb:EmissionMap,ptin:_EmissionMap,varname:node_1773,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Multiply,id:433,x:32362,y:32535,varname:node_433,prsc:2|A-1773-RGB,B-9173-RGB;n:type:ShaderForge.SFN_Color,id:9173,x:32166,y:32669,ptovrint:False,ptlb:EmissionColor,ptin:_EmissionColor,varname:node_9173,prsc:2,glob:False,taghide:False,taghdr:True,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:1;n:type:ShaderForge.SFN_SwitchProperty,id:278,x:32477,y:32676,ptovrint:False,ptlb:UseEmission,ptin:_UseEmission,varname:node_278,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:False|A-2424-OUT,B-433-OUT;n:type:ShaderForge.SFN_Vector3,id:2424,x:32430,y:32478,varname:node_2424,prsc:2,v1:0,v2:0,v3:0;proporder:6037-1800-9769-1711-252-7875-9084-7068-8458-3782-1841-6806-7809-278-1773-9173;pass:END;sub:END;*/

Shader "PBR/BRP_MaskMap" {
    Properties {
        _BaseMap ("BaseMap", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float ) = 1
        [MaterialToggle] _UseNormalMap ("UseNormalMap", Float ) = 0
        _NormalMap ("NormalMap", 2D) = "bump" {}
        [MaterialToggle] _UseMaskMap ("UseMaskMap", Float ) = 0
        _MaskMap ("MaskMap", 2D) = "white" {}
        _MetallicStrength ("MetallicStrength", Range(0, 1)) = 1
        _AOStrength ("AOStrength", Range(0, 1)) = 1
        _SmoothnessStrength ("SmoothnessStrength", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _AO ("AO", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [MaterialToggle] _UseEmission ("UseEmission", Float ) = 0
        _EmissionMap ("EmissionMap", 2D) = "white" {}
        [HDR]_EmissionColor ("EmissionColor", Color) = (1,1,1,1)
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        LOD 200
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma target 3.0
            uniform sampler2D _BaseMap; uniform float4 _BaseMap_ST;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform sampler2D _NormalMap; uniform float4 _NormalMap_ST;
            uniform sampler2D _EmissionMap; uniform float4 _EmissionMap_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseNormalMap)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseMaskMap)
                UNITY_DEFINE_INSTANCED_PROP( float, _Metallic)
                UNITY_DEFINE_INSTANCED_PROP( float, _Smoothness)
                UNITY_DEFINE_INSTANCED_PROP( float, _AO)
                UNITY_DEFINE_INSTANCED_PROP( float, _MetallicStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _AOStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _SmoothnessStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _Intensity)
                UNITY_DEFINE_INSTANCED_PROP( float4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseEmission)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                LIGHTING_COORDS(7,8)
                UNITY_FOG_COORDS(9)
                #if defined(LIGHTMAP_ON) || defined(UNITY_SHOULD_SAMPLE_SH)
                    float4 ambientOrLightmapUV : TEXCOORD10;
                #endif
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                #ifdef LIGHTMAP_ON
                    o.ambientOrLightmapUV.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.ambientOrLightmapUV.zw = 0;
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                    o.ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                UNITY_SETUP_INSTANCE_ID( i );
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _NormalMap_var = UnpackNormal(tex2D(_NormalMap,TRANSFORM_TEX(i.uv0, _NormalMap)));
                float3 _UseNormalMap_var = lerp( i.normalDir, _NormalMap_var.rgb, UNITY_ACCESS_INSTANCED_PROP( Props, _UseNormalMap ) );
                float3 normalLocal = _UseNormalMap_var;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float _Metallic_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Metallic );
                float _AO_var = UNITY_ACCESS_INSTANCED_PROP( Props, _AO );
                float _Smoothness_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Smoothness );
                float _MetallicStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _MetallicStrength );
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float _AOStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _AOStrength );
                float _SmoothnessStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _SmoothnessStrength );
                float4 _UseMaskMap_var = lerp( float4(_Metallic_var,_AO_var,0.0,_Smoothness_var), float4((_MetallicStrength_var*_MaskMap_var.r),(_AOStrength_var*_MaskMap_var.g),_MaskMap_var.b,(_MaskMap_var.a*_SmoothnessStrength_var)), UNITY_ACCESS_INSTANCED_PROP( Props, _UseMaskMap ) );
                float4 node_5632 = _UseMaskMap_var.rgba;
                float gloss = node_5632.a;
                float perceptualRoughness = 1.0 - node_5632.a;
                float roughness = perceptualRoughness * perceptualRoughness;
                float specPow = exp2( gloss * 10.0 + 1.0 );
/////// GI Data:
                UnityLight light;
                #ifdef LIGHTMAP_OFF
                    light.color = lightColor;
                    light.dir = lightDirection;
                    light.ndotl = LambertTerm (normalDirection, light.dir);
                #else
                    light.color = half3(0.f, 0.f, 0.f);
                    light.ndotl = 0.0f;
                    light.dir = half3(0.f, 0.f, 0.f);
                #endif
                UnityGIInput d;
                d.light = light;
                d.worldPos = i.posWorld.xyz;
                d.worldViewDir = viewDirection;
                d.atten = attenuation;
                #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                    d.ambient = 0;
                    d.lightmapUV = i.ambientOrLightmapUV;
                #else
                    d.ambient = i.ambientOrLightmapUV;
                #endif
                #if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMin[0] = unity_SpecCube0_BoxMin;
                    d.boxMin[1] = unity_SpecCube1_BoxMin;
                #endif
                #if UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMax[0] = unity_SpecCube0_BoxMax;
                    d.boxMax[1] = unity_SpecCube1_BoxMax;
                    d.probePosition[0] = unity_SpecCube0_ProbePosition;
                    d.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                d.probeHDR[0] = unity_SpecCube0_HDR;
                d.probeHDR[1] = unity_SpecCube1_HDR;
                Unity_GlossyEnvironmentData ugls_en_data;
                ugls_en_data.roughness = 1.0 - gloss;
                ugls_en_data.reflUVW = viewReflectDirection;
                UnityGI gi = UnityGlobalIllumination(d, 1, normalDirection, ugls_en_data );
                lightDirection = gi.light.dir;
                lightColor = gi.light.color;
////// Specular:
                float NdotL = saturate(dot( normalDirection, lightDirection ));
                float LdotH = saturate(dot(lightDirection, halfDirection));
                float3 specularColor = node_5632.r;
                float specularMonochrome;
                float4 _BaseMap_var = tex2D(_BaseMap,TRANSFORM_TEX(i.uv0, _BaseMap));
                float4 _Tint_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Tint );
                float _Intensity_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Intensity );
                float3 diffuseColor = (_BaseMap_var.rgb*_Tint_var.rgb*_Intensity_var); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = abs(dot( normalDirection, viewDirection ));
                float NdotH = saturate(dot( normalDirection, halfDirection ));
                float VdotH = saturate(dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, roughness );
                float normTerm = GGXTerm(NdotH, roughness);
                float specularPBL = (visTerm*normTerm) * UNITY_PI;
                #ifdef UNITY_COLORSPACE_GAMMA
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                #endif
                specularPBL = max(0, specularPBL * NdotL);
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                    specularPBL = 0.0;
                #endif
                half surfaceReduction;
                #ifdef UNITY_COLORSPACE_GAMMA
                    surfaceReduction = 1.0-0.28*roughness*perceptualRoughness;
                #else
                    surfaceReduction = 1.0/(roughness*roughness + 1.0);
                #endif
                specularPBL *= any(specularColor) ? 1.0 : 0.0;
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                half grazingTerm = saturate( gloss + specularMonochrome );
                float3 indirectSpecular = (gi.indirect.specular);
                indirectSpecular *= FresnelLerp (specularColor, grazingTerm, NdotV);
                indirectSpecular *= surfaceReduction;
                float3 specular = (directSpecular + indirectSpecular);
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += gi.indirect.diffuse;
                indirectDiffuse *= node_5632.g; // Diffuse AO
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
////// Emissive:
                float4 _EmissionMap_var = tex2D(_EmissionMap,TRANSFORM_TEX(i.uv0, _EmissionMap));
                float4 _EmissionColor_var = UNITY_ACCESS_INSTANCED_PROP( Props, _EmissionColor );
                float3 _UseEmission_var = lerp( float3(0,0,0), (_EmissionMap_var.rgb*_EmissionColor_var.rgb), UNITY_ACCESS_INSTANCED_PROP( Props, _UseEmission ) );
                float3 emissive = _UseEmission_var;
/// Final Color:
                float3 finalColor = diffuse + specular + emissive;
                fixed4 finalRGBA = fixed4(finalColor,1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma target 3.0
            uniform sampler2D _BaseMap; uniform float4 _BaseMap_ST;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform sampler2D _NormalMap; uniform float4 _NormalMap_ST;
            uniform sampler2D _EmissionMap; uniform float4 _EmissionMap_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseNormalMap)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseMaskMap)
                UNITY_DEFINE_INSTANCED_PROP( float, _Metallic)
                UNITY_DEFINE_INSTANCED_PROP( float, _Smoothness)
                UNITY_DEFINE_INSTANCED_PROP( float, _AO)
                UNITY_DEFINE_INSTANCED_PROP( float, _MetallicStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _AOStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _SmoothnessStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _Intensity)
                UNITY_DEFINE_INSTANCED_PROP( float4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseEmission)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                LIGHTING_COORDS(7,8)
                UNITY_FOG_COORDS(9)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                UNITY_SETUP_INSTANCE_ID( i );
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _NormalMap_var = UnpackNormal(tex2D(_NormalMap,TRANSFORM_TEX(i.uv0, _NormalMap)));
                float3 _UseNormalMap_var = lerp( i.normalDir, _NormalMap_var.rgb, UNITY_ACCESS_INSTANCED_PROP( Props, _UseNormalMap ) );
                float3 normalLocal = _UseNormalMap_var;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float _Metallic_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Metallic );
                float _AO_var = UNITY_ACCESS_INSTANCED_PROP( Props, _AO );
                float _Smoothness_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Smoothness );
                float _MetallicStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _MetallicStrength );
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float _AOStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _AOStrength );
                float _SmoothnessStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _SmoothnessStrength );
                float4 _UseMaskMap_var = lerp( float4(_Metallic_var,_AO_var,0.0,_Smoothness_var), float4((_MetallicStrength_var*_MaskMap_var.r),(_AOStrength_var*_MaskMap_var.g),_MaskMap_var.b,(_MaskMap_var.a*_SmoothnessStrength_var)), UNITY_ACCESS_INSTANCED_PROP( Props, _UseMaskMap ) );
                float4 node_5632 = _UseMaskMap_var.rgba;
                float gloss = node_5632.a;
                float perceptualRoughness = 1.0 - node_5632.a;
                float roughness = perceptualRoughness * perceptualRoughness;
                float specPow = exp2( gloss * 10.0 + 1.0 );
////// Specular:
                float NdotL = saturate(dot( normalDirection, lightDirection ));
                float LdotH = saturate(dot(lightDirection, halfDirection));
                float3 specularColor = node_5632.r;
                float specularMonochrome;
                float4 _BaseMap_var = tex2D(_BaseMap,TRANSFORM_TEX(i.uv0, _BaseMap));
                float4 _Tint_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Tint );
                float _Intensity_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Intensity );
                float3 diffuseColor = (_BaseMap_var.rgb*_Tint_var.rgb*_Intensity_var); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = abs(dot( normalDirection, viewDirection ));
                float NdotH = saturate(dot( normalDirection, halfDirection ));
                float VdotH = saturate(dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, roughness );
                float normTerm = GGXTerm(NdotH, roughness);
                float specularPBL = (visTerm*normTerm) * UNITY_PI;
                #ifdef UNITY_COLORSPACE_GAMMA
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                #endif
                specularPBL = max(0, specularPBL * NdotL);
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                    specularPBL = 0.0;
                #endif
                specularPBL *= any(specularColor) ? 1.0 : 0.0;
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                float3 specular = directSpecular;
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse + specular;
                fixed4 finalRGBA = fixed4(finalColor * 1,0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "Meta"
            Tags {
                "LightMode"="Meta"
            }
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_META 1
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityMetaPass.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma target 3.0
            uniform sampler2D _BaseMap; uniform float4 _BaseMap_ST;
            uniform sampler2D _MaskMap; uniform float4 _MaskMap_ST;
            uniform sampler2D _EmissionMap; uniform float4 _EmissionMap_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseMaskMap)
                UNITY_DEFINE_INSTANCED_PROP( float, _Metallic)
                UNITY_DEFINE_INSTANCED_PROP( float, _Smoothness)
                UNITY_DEFINE_INSTANCED_PROP( float, _AO)
                UNITY_DEFINE_INSTANCED_PROP( float, _MetallicStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _AOStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _SmoothnessStrength)
                UNITY_DEFINE_INSTANCED_PROP( float, _Intensity)
                UNITY_DEFINE_INSTANCED_PROP( float4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP( fixed, _UseEmission)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST );
                return o;
            }
            float4 frag(VertexOutput i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID( i );
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                UnityMetaInput o;
                UNITY_INITIALIZE_OUTPUT( UnityMetaInput, o );
                
                float4 _EmissionMap_var = tex2D(_EmissionMap,TRANSFORM_TEX(i.uv0, _EmissionMap));
                float4 _EmissionColor_var = UNITY_ACCESS_INSTANCED_PROP( Props, _EmissionColor );
                float3 _UseEmission_var = lerp( float3(0,0,0), (_EmissionMap_var.rgb*_EmissionColor_var.rgb), UNITY_ACCESS_INSTANCED_PROP( Props, _UseEmission ) );
                o.Emission = _UseEmission_var;
                
                float4 _BaseMap_var = tex2D(_BaseMap,TRANSFORM_TEX(i.uv0, _BaseMap));
                float4 _Tint_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Tint );
                float _Intensity_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Intensity );
                float3 diffColor = (_BaseMap_var.rgb*_Tint_var.rgb*_Intensity_var);
                float specularMonochrome;
                float3 specColor;
                float _Metallic_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Metallic );
                float _AO_var = UNITY_ACCESS_INSTANCED_PROP( Props, _AO );
                float _Smoothness_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Smoothness );
                float _MetallicStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _MetallicStrength );
                float4 _MaskMap_var = tex2D(_MaskMap,TRANSFORM_TEX(i.uv0, _MaskMap));
                float _AOStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _AOStrength );
                float _SmoothnessStrength_var = UNITY_ACCESS_INSTANCED_PROP( Props, _SmoothnessStrength );
                float4 _UseMaskMap_var = lerp( float4(_Metallic_var,_AO_var,0.0,_Smoothness_var), float4((_MetallicStrength_var*_MaskMap_var.r),(_AOStrength_var*_MaskMap_var.g),_MaskMap_var.b,(_MaskMap_var.a*_SmoothnessStrength_var)), UNITY_ACCESS_INSTANCED_PROP( Props, _UseMaskMap ) );
                float4 node_5632 = _UseMaskMap_var.rgba;
                diffColor = DiffuseAndSpecularFromMetallic( diffColor, node_5632.r, specColor, specularMonochrome );
                float roughness = 1.0 - node_5632.a;
                o.Albedo = diffColor + specColor * roughness * roughness * 0.5;
                
                return UnityMetaFragment( o );
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
