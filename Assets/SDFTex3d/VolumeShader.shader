Shader "Custom/SDFLiquid_URP"
{
    Properties
    {
        _SDFTex("Container SDF (Texture3D)", 3D) = "" {}
       

        // 填充与重力
        _Fill01("Fill Level 0..1 (along -Gravity)", Range(0,1)) = 0.5
        _GravityWS("Gravity Dir (World)", Vector) = (0,-1,0,0)

        // 步进与精度
        _Steps("Raymarch Steps", Range(16,256)) = 96
        _StepSize("Step Size (obj units)", Range(0.001,0.1)) = 0.02
        _NormalEps("Normal Epsilon (tex space)", Range(0.001,0.02)) = 0.01

        // 颜色/折射/吸收
        _Tint("Liquid Tint", Color) = (0.15, 0.6, 0.9, 1)
        _RefractStrength("Refraction Strength (px)", Range(0, 2)) = 0.35
        _F0("Fresnel F0", Range(0.0, 0.1)) = 0.04
        _Smoothness("Specular Smoothness", Range(0,1)) = 0.8
        _AbsorptionColor("Absorption Color", Color) = (1, 0.8, 0.6, 1)
        _AbsorptionStrength("Absorption Strength", Range(0,10)) = 2.0

        // 泡沫/自由液面强化
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        _FoamStrength("Foam Strength", Range(0,1)) = 0.25

        // 背景兜底
        _BGColor("Background Fallback", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True"
        }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "LiquidRaymarch"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // 体素容器
            TEXTURE3D(_SDFTex);
            SAMPLER(sampler_SDFTex);

            // 场景颜色（URP: 启用 Opaque Texture）
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 posHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
               
                float  _Fill01;
                float4 _GravityWS;
                float  _Steps;
                float  _StepSize;
                float  _NormalEps;

                float4 _Tint;
                float  _RefractStrength;
                float  _F0;
                float  _Smoothness;
                float4 _AbsorptionColor;
                float  _AbsorptionStrength;

                float4 _FoamColor;
                float  _FoamStrength;

                float4 _BGColor;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f    o;
                float3 wp  = TransformObjectToWorld(v.vertex.xyz);
                o.posHCS   = TransformWorldToHClip(wp);
                o.worldPos = wp;
                return o;
            }

            // AABB [-0.5,0.5]^3（对象空间）求交
            bool RayBoxIntersect(float3 ro, float3 rd, out float tEnter, out float tExit)
            {
                float3 bmin = float3(-0.5, -0.5, -0.5);
                float3 bmax = float3(0.5, 0.5, 0.5);

                float3 inv = 1.0 / rd;
                float3 t0  = (bmin - ro) * inv;
                float3 t1  = (bmax - ro) * inv;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tEnter = max(max(tmin.x, tmin.y), tmin.z);
                tExit  = min(min(tmax.x, tmax.y), tmax.z);

                return tExit >= max(tEnter, 0.0);
            }

            // 对象空间 [-0.5,0.5] -> 纹理 [0,1]
            float3 ObjToTex(float3 pObj) { return pObj + 0.5; }

            // 容器SDF（球体等）采样
            float SDF_Shape(float3 pObj)
            {
                float3 uvw = ObjToTex(pObj);
                return SAMPLE_TEXTURE3D(_SDFTex, sampler_SDFTex, uvw).r ;
                // 距离值补偿，保持距离单位（可确保法线、dp vs ds 判断更稳定）
            }

            // 自由液面平面 SDF：dot(n, p) - h
            // n 为对象空间单位向量；h 为对象空间高度（-0.5..0.5）
            float SDF_Plane(float3 pObj, float3 nObj, float hObj)
            {
                return dot(nObj, pObj) - hObj;
            }

            // 液体体积 = 容器 ∩ 平面半空间 => SDF = max(ds, dp)
            float SDF_Liquid(float3 pObj, float3 nObj, float hObj, out float ds, out float dp)
            {
                ds = SDF_Shape(pObj);
                dp = SDF_Plane(pObj, nObj, hObj);
                return max(ds, dp);
            }

            // 对 f=max(ds,dp) 做中心差分
            float3 Gradient_Liquid(float3 p, float3 nObj, float hObj, float eps)
            {
                float3 ex = float3(eps, 0, 0);
                float3 ey = float3(0, eps, 0);
                float3 ez = float3(0, 0, eps);

                float ds0, dp0, ds1, dp1;

                float fpx = SDF_Liquid(p + ex, nObj, hObj, ds0, dp0);
                float fmx = SDF_Liquid(p - ex, nObj, hObj, ds1, dp1);
                float fpy = SDF_Liquid(p + ey, nObj, hObj, ds0, dp0);
                float fmy = SDF_Liquid(p - ey, nObj, hObj, ds1, dp1);
                float fpz = SDF_Liquid(p + ez, nObj, hObj, ds0, dp0);
                float fmz = SDF_Liquid(p - ez, nObj, hObj, ds1, dp1);

                return normalize(float3(fpx - fmx, fpy - fmy, fpz - fmz));
            }

            // 计算屏幕 UV（任意世界点）
            float2 ScreenUVFromWorld(float3 worldPos)
            {
                float4 clip = TransformWorldToHClip(worldPos);
                float2 uv   = clip.xy / max(clip.w, 1e-6);
                uv          = uv * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            // Schlick Fresnel
            float FresnelSchlick(float cosTheta, float F0)
            {
                return F0 + (1.0 - F0) * pow(1.0 - saturate(cosTheta), 5.0);
            }

            float4 frag(v2f i) : SV_Target
            {
                // 摄像机射线（世界 -> 对象）
                float3 camPosW = _WorldSpaceCameraPos.xyz;
                float3 rayDirW = normalize(i.worldPos - camPosW);

                float3 ro = TransformWorldToObject(camPosW);
                float3 rd = normalize(TransformWorldToObjectDir(rayDirW));

                // 光线与体盒求交
                float tEnter, tExit;
                if (!RayBoxIntersect(ro, rd, tEnter, tExit))
                    return _BGColor;

                tEnter = max(0.0, tEnter);

                // 世界重力 -> 对象空间自由液面法线（自由液面法线 ~ -gravity）
                float3 gW       = normalize(_GravityWS.xyz);
                float3 planeN_W = -gW;
                float3 planeN_O = normalize(mul((float3x3)unity_WorldToObject, planeN_W));

                // 填充高度（0..1）映射到对象空间 [-0.5,0.5] 沿 planeN_O 方向的位移。
                // 简化起见，我们把 h 定义为相对于对象原点在 planeN_O 方向的标量位移：
                float hObj = lerp(-0.5, 0.5, _Fill01);

                // 光线步进找表面
                int   steps   = max(16, (int)_Steps);
                float stepLen = _StepSize;

                float t       = tEnter;
                float prevF   = 0;
                bool  hasPrev = false;

                float hitT  = -1.0;
                float hitDs = 0, hitDp = 0;

                [loop]
                for (int s = 0; s < steps && t <= tExit; s++)
                {
                    float3 p = ro + rd * t;
                    float  ds, dp;
                    float  f = SDF_Liquid(p, planeN_O, hObj, ds, dp);

                    if (hasPrev && prevF > 0 && f <= 0)
                    {
                        // 二分细化
                        float t0 = t - stepLen;
                        float t1 = t;
                        [unroll(6)]
                        for (int k = 0; k < 6; k++)
                        {
                            float  tm = 0.5 * (t0 + t1);
                            float3 pm = ro + rd * tm;
                            float  dsm, dpm;
                            float  fm = SDF_Liquid(pm, planeN_O, hObj, dsm, dpm);
                            if (fm > 0) t0 = tm;
                            else t1        = tm;
                        }
                        hitT = 0.5 * (t0 + t1);

                        // 顺便记录最近一步的 ds/dp 以做自由液面判断（近似即可）
                        hitDs = ds;
                        hitDp = dp;
                        break;
                    }

                    prevF   = f;
                    hasPrev = true;
                    t += stepLen;
                }

                if (hitT < 0)
                {
                    // 没有击中液体表面
                    return _BGColor;
                }

                // 命中点
                float3 pHit_O = ro + rd * hitT;
                float3 pHit_W = TransformObjectToWorld(pHit_O);

                // 法线（对象->世界）
                float3   nObj          = Gradient_Liquid(pHit_O, planeN_O, hObj, _NormalEps);
                float3x3 objToWorldRot = (float3x3)unity_ObjectToWorld;
                float3   nWorld        = normalize(mul(nObj, objToWorldRot));

                // 厚度估计：从命中点继续向前步进，找到离开液体的点
                float thickness = 0.0;
                {
                    float t2 = hitT + stepLen;
                    [loop]
                    for (int s = 0; s < steps / 4 && t2 <= tExit; s++)
                    {
                        float3 p2 = ro + rd * t2;
                        float  ds2, dp2;
                        float  f2 = SDF_Liquid(p2, planeN_O, hObj, ds2, dp2);
                        thickness += stepLen;
                        if (f2 > 0) break; // 离开液体
                        t2 += stepLen;
                    }
                }

                // 折射 + Fresnel（屏幕空间）
                float3 V     = -rayDirW;
                float  NdotV = saturate(dot(nWorld, V));
                float  F     = FresnelSchlick(NdotV, _F0);

                float2 uv = ScreenUVFromWorld(pHit_W);
                // 视差性：用法线扰动UV实现简易折射
                float2 distort  = nWorld.xy * _RefractStrength;
                float3 sceneCol = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + distort).
rgb;

                // 吸收（Beer-Lambert 近似）
                float3 transmittance = exp(-_AbsorptionStrength * thickness * _AbsorptionColor.rgb);

                // 主光高光（简化 Blinn-Phong）
                Light  mainLight = GetMainLight();
                float3 L         = normalize(-mainLight.direction);
                float3 H         = normalize(L + V);
                float  NdotL     = saturate(dot(nWorld, L));
                float  NdotH     = saturate(dot(nWorld, H));
                float  spec      = pow(NdotH, lerp(8.0, 128.0, _Smoothness)) * NdotL;

                // 基础颜色：折射场景 * 吸收 + 自身色调
                float3 baseCol = sceneCol * transmittance;
                baseCol        = lerp(baseCol, _Tint.rgb, 0.15); // 少量自有染色

                // 自由液面识别：若 dp >= ds，表明平面项主导
                float  freeSurface = step(hitDs, hitDp);
                float  foam = _FoamStrength * freeSurface * saturate(1.0 - abs(dot(normalize(planeN_W), nWorld)));
                float3 foamCol = _FoamColor.rgb * foam;

                float3 finalRGB = baseCol + spec * mainLight.color + foamCol;
                float  finalA   = saturate(0.35 + (1.0 - F) * 0.35 + foam * 0.2); // 折射越强越透明，泡沫略增不透明

                return float4(finalRGB, finalA);
            }
            ENDHLSL
        }
    }
}