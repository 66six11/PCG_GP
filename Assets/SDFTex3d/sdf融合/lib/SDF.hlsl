#ifndef SDF
#define SDF

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

//球形sdf
float SdSphere(float3 p, float3 center, float radius)
{
	return distance(p, center) - radius;
}

// 立方体SDF
float SdBox(float3 p, float3 center, float3 size)
{
	float3 q = abs(p - center) - size;
	return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

// 平面SDF
float SdPlane(float3 p, float3 normal, float height)
{
	return dot(p, normalize(normal)) + height;
}

// 圆柱体SDF
float SdCylinder(float3 p, float3 center, float radius, float height)
{
	p -= center; // 将点转换到局部空间
	float2 d = abs(float2(length(p.xz), p.y)) - float2(radius, height);
	return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

// 更精确的圆锥体SDF (Y轴方向)
float SdCone(float3 p, float3 center, float radius, float height)
{
	p -= center;
	float2 q = float2(length(p.xz), p.y);

	// 圆锥体侧面距离
	float2 tip        = float2(0, height);
	float2 base       = float2(radius, 0);
	float2 dir        = normalize(tip - base);
	float  projection = dot(q - base, dir);
	float  sideDist   = length(q - base - dir * clamp(projection, 0.0, height));

	// 底部圆盘距离
	float baseDist = length(q - base) - radius;

	// 顶部点距离
	float tipDist = length(q - tip);

	// 取最小值
	return min(min(sideDist, baseDist), tipDist);
}

// 平滑过渡函数（用于SDF融合）
// 平滑最小值函数（用于融合效果）
float smin(float a, float b, float k)
{
	float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
	return lerp(b, a, h) - k * h * (1.0 - h);
}

float smax(float a, float b, float k)
{
	float h = clamp(0.5 + 0.5 * (a - b) / k, 0.0, 1.0);
	return lerp(b, a, h) + k * h * (1.0 - h);
}


#endif
