#ifndef NEMAINCLUDE
#define NEMAINCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
// 添加球体位置参数
uniform float4 _BallPositions[10];
uniform float  _BallRadii[10];
uniform int    _BallCount;
uniform float  _SmoothFactor = 0.5;
#endif
