Shader "Hidden/Metaballs3D_Raymarch_URP"
{
    Properties
    {
        _MaxSteps("Max Steps", Range(1,512)) = 128
        _MaxDistance("Max Distance", Float) = 100
        _SphereRadius("Sphere Radius", Float) = 0.5
        _SmoothFactor("Smooth Factor", Range(0.01, 1.0)) = 0.5
        _AmbientColor("Ambient Color", Color) = (0.1, 0.1, 0.1, 1)
        _SpecularPower("Specular Power", Range(1, 256)) = 32
        _SpecularIntensity("Specular Intensity", Range(0, 1)) = 0.5
        _ShadowIntensity("Shadow Intensity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "RaymarchMetaballs"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "lib/SDF.hlsl"

            // 全局球体数据
            float4 _BallPositions[32];
            float  _BallRadii[32];
            int    _BallCount;
            float  _SmoothFactor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            // 属性
            int    _MaxSteps;
            float  _MaxDistance;
            float  _SphereRadius;
            float3 _AmbientColor;
            float  _SpecularPower;
            float  _SpecularIntensity;
            float  _ShadowIntensity;

          

   


            // Metaball SDF计算
            float MetaballSDF(float3 position)
            {
                float sdf = 100000; // 初始化为大值

                if (_BallCount > 0)
                {
                    for (int i = 0; i < _BallCount; i++)
                    {
                        float3 ballPos    = _BallPositions[i].xyz;
                        float  ballRadius = _BallRadii[i];
                        float  sphereSdf  = SdSphere(position, ballPos, ballRadius);
                        sdf               = smin(sdf, sphereSdf, _SmoothFactor);
                    }
                }
                else
                {
                    // 默认球体
                    sdf = SdSphere(position, float3(0, 0, 0), _SphereRadius);
                }

                return sdf;
            }

            // 计算法线函数
            float3 ComputeNormal(float3 p)
            {
                const float eps = 0.001;
                return normalize(float3(
                    MetaballSDF(p + float3(eps, 0, 0)) - MetaballSDF(p - float3(eps, 0, 0)),
                    MetaballSDF(p + float3(0, eps, 0)) - MetaballSDF(p - float3(0, eps, 0)),
                    MetaballSDF(p + float3(0, 0, eps)) - MetaballSDF(p - float3(0, 0, eps))
                ));
            }

            // 环境光遮蔽
            float AmbientOcclusion(float3 p, float3 n)
            {
                float ao = 0.0;
                float dist;
                for (int i = 1; i <= 5; i++)
                {
                    float fd = 0.01 * i;
                    dist     = MetaballSDF(p + n * fd);
                    ao += (fd - dist) / fd;
                }
                return 1.0 - clamp(ao / 5.0, 0.0, 1.0);
            }
            // 阴影计算
            float SoftShadow(float3 ro, float3 rd, float mint, float maxt, float k)
            {
                float res = 1.0;
                for (float t = mint; t < maxt;)
                {
                    float h = MetaballSDF(ro + rd * t);
                    if (h < 0.001)
                        return 0.0;
                    res = min(res, k * h / t);
                    t += h;
                }
                return res;
            }
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);

                // 计算世界空间位置和视图方向
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.worldPos      = worldPos;
                o.viewDir       = worldPos - _WorldSpaceCameraPos;
                o.screenPos     = ComputeScreenPos(o.pos);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // 设置光线起点和方向
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir    = normalize(i.viewDir);

                // 从深度纹理获取场景深度
                float2 screenUV      = i.screenPos.xy / i.screenPos.w;
                float  sceneDepth    = SampleSceneDepth(screenUV);
                float3 sceneWorldPos = ComputeWorldSpacePosition(screenUV, sceneDepth, UNITY_MATRIX_I_VP);
                float  maxDist       = min(_MaxDistance, distance(rayOrigin, sceneWorldPos));

                float  rayDistance = 0;
                float3 hitPosition = float3(0, 0, 0);
                bool   hit         = false;
                float  minDist     = 10000;

                // 光线步进循环
                for (int step = 0; step < _MaxSteps; step++)
                {
                    // 计算当前位置
                    float3 currentPos = rayOrigin + rayDir * rayDistance;

                    // 计算SDF值
                    float sdfValue = MetaballSDF(currentPos);
                    minDist        = min(minDist, sdfValue);

                    // 检查是否命中
                    if (sdfValue < 0.001 * rayDistance)
                    {
                        hit         = true;
                        hitPosition = currentPos;
                        break;
                    }

                    // 检查是否超出最大距离
                    if (rayDistance > maxDist)
                    {
                        break;
                    }

                    // 增加步进距离
                    rayDistance += sdfValue;
                }

                // 如果命中则渲染球体
                if (hit)
                {
                    // 计算法线
                    float3 normal = ComputeNormal(hitPosition);

                    // 获取主光源
                    Light  mainLight  = GetMainLight();
                    float3 lightDir   = mainLight.direction;
                    float3 lightColor = mainLight.color;

                    // 漫反射
                    float diffuse = max(0, dot(normal, lightDir));

                    // 镜面反射
                    float3 viewDir  = normalize(rayOrigin - hitPosition);
                    float3 halfDir  = normalize(lightDir + viewDir);
                    float  specular = pow(max(0, dot(normal, halfDir)), _SpecularPower) * _SpecularIntensity;

                    // 阴影
                    float shadow = SoftShadow(hitPosition + normal * 0.01, lightDir, 0.01, 10.0, 8.0);
                    shadow       = lerp(1.0, shadow, _ShadowIntensity);

                    // 环境光遮蔽
                    float ao = AmbientOcclusion(hitPosition, normal);

                    // 最终颜色
                    float3 color = float3(0.8, 0.2, 0.2); // 基础颜色
                    color *= (diffuse * shadow + _AmbientColor * ao);
                    color += specular * lightColor;

                    return float4(color, 1.0);
                }
                else
                {
                    // 未命中则返回背景色
                    discard;
                }
               
            }
            ENDHLSL
        }
    }
    FallBack Off
}