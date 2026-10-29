Shader "PCG/Terrain Slope Blend"
{
    Properties
    {
        _SoilColor("Soil", Color) = (0.34,0.46,0.24,1)
        _RockColor("Rock", Color) = (0.48,0.46,0.40,1)
        _BaseMap("Base map", 2D) = "white" {}
        _BaseColor("Base color", Color) = (1,1,1,1)
        _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _SoilColor, _RockColor, _BaseColor;
                float4 _BaseMap_ST;
                float _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half rock:TEXCOORD2;
                half fog:TEXCOORD3;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.rock=v.color.r; o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            float Hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float Noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 normal=normalize(i.normalWS);
                // World-space variation does not stretch when a polygon becomes steep.
                float coarse=Noise(i.positionWS*1.1), fine=Noise(i.positionWS*9);
                half weight=saturate(i.rock+(coarse-0.5)*0.12*i.rock*(1-i.rock));
                half3 soil=_SoilColor.rgb*(0.94+coarse*0.12);
                half layers=sin(i.positionWS.y*7+coarse*2)*0.018;
                half3 rock=_RockColor.rgb*(0.95+coarse*0.09+(fine-0.5)*0.045+layers);
                half3 albedo=lerp(soil,rock,weight);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lighting=SampleSH(normal)+light.color*saturate(dot(normal,light.direction))*light.distanceAttenuation*light.shadowAttenuation;
                return half4(MixFog(albedo*lighting,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
