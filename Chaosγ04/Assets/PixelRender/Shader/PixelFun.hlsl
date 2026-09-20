#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

void Unity_Dither_float4(float2 screenPos, inout float alpha)
{
    alpha = pow(alpha,2);
    alpha = clamp(alpha,0,1);
    float2 uv = screenPos.xy * _ScreenParams.xy/2;
    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
    };
    uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
    alpha -= DITHER_THRESHOLDS[index];
    clip(alpha);
}

float3 CalculateNormalMap(Texture2D noramlTex,SamplerState sampler_noramleTex,float2 uv,float3x3 TBN,float normalScale)
{
    half4 NormalTex = SAMPLE_TEXTURE2D(noramlTex,sampler_noramleTex,uv);
    float3 normalTS = UnpackNormalScale(NormalTex,normalScale);              
    normalTS.z = pow((1 - pow(normalTS.x,2) - pow(normalTS.y,2)),0.5);         
    return normalize(mul(normalTS,TBN)); 
}

//漫反射
float3 CalculateDiffuse(float3 normalWorld,float3 lightDir,float shadingOffset,Texture2D shadowRampTex,SamplerState sampler_ShadeowRamp,out float diffuse)
{
    diffuse = dot(normalWorld,lightDir) * 0.5 + 0.5 - shadingOffset;
    float3 diffuseColor = SAMPLE_TEXTURE2D(shadowRampTex,sampler_ShadeowRamp,float2(diffuse ,0)).rgb;
    return diffuseColor;
}

//边缘光
float CalculateRim(float3 normalWorld,float3 viewDir,float rimSize,float rimEdgeSmoothness)
{
    float rim = 1.0 - dot(viewDir, normalWorld);
    float rimSpread = 1.0 - rimSize;
    float rimEdgeSmooth = rimEdgeSmoothness;
    float rimTransition = smoothstep(rimSpread - rimEdgeSmooth * 0.5, rimSpread + rimEdgeSmooth * 0.5, rim);
    return rimTransition;
}

float CalculateRimDepth(float depth,float2 screenPos,float3 normalWorld,float rimSize,float rimEdge)
{
    half2 nDirCS = normalize(TransformWorldToHClipDir(normalWorld).xy);
    nDirCS.y = -nDirCS.y;
    nDirCS.y *= _ScreenParams.x / _ScreenParams.y;
    half offsetDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_CameraDepthTexture,screenPos + nDirCS * 0.05 * rimSize).r;
    float rim = depth - offsetDepth;
    rim = step(rimEdge * 0.01,rim);
    return rim;
}

//高光
float CalculateSpecular(float3 normalWorld,float3 lightDir,float3 viewDir,float specularSize,float specularEdgeSmoothness)
{
    float3 halfV = normalize(lightDir + viewDir);
    float NdotH = dot(normalWorld, halfV) * 0.5 + 0.5;
    float specular = saturate(pow(abs(NdotH), 100.0 * (1.0 - specularSize) * (1.0 - specularSize)));
    float specularTransition = smoothstep(0.5 - specularEdgeSmoothness * 0.1,
                                   0.5 + specularEdgeSmoothness * 0.1, specular);
    return specularTransition;
}

//额外光源
float3 CaleulateAddDiffuse(float3 normalWorld,float3 position,Texture2D addShadowRampTex,SamplerState sampler_AddShadowRampTex)
{
    float3 AddColor = float3(0,0,0);
    int addLightsCount = GetAdditionalLightsCount();
    for(int j=0; j<addLightsCount; j++){
        Light addlight=GetAdditionalLight(j,position,0);
        half addHalfLambert = max(0,dot(normalWorld,addlight.direction));
        addHalfLambert *= addlight.shadowAttenuation;
        addHalfLambert *= addlight.distanceAttenuation;
        float lambert = dot(normalWorld , addlight.direction) * 0.5 + 0.5;
        half lightColorRmap = SAMPLE_TEXTURE2D(addShadowRampTex,sampler_AddShadowRampTex,half2(addHalfLambert * lambert,0)).r;//对Rmap图采样
        lightColorRmap = step(0.001,addHalfLambert) * lightColorRmap;//对0.001一下的强度取0，防止没有Rmap图的时候光照会影响整个模型
        AddColor += lightColorRmap * addlight.color;
    }
    return AddColor;
}

float4x4 RotateX(float angleX) {
    float c = cos(angleX);
    float s = sin(angleX);
    return float4x4(
        1, 0, 0, 0,
        0, c, -s, 0,
        0, s, c, 0,
        0, 0, 0, 1
    );
}

float4x4 RotateY(half angleY) {
    float c = cos(angleY);
    float s = sin(angleY);
    return float4x4(
        c, 0, -s, 0,
        0, 1, 0, 0,
        s, 0, c, 0,
        0, 0, 0, 1
    );
}

half radians(half angle) {
    return angle * PI / 180.0;
};

//获取自定义的向量
float3 GetDir(float angleX,float angleY)
{
    angleX = radians(angleX);
    angleY = radians(angleY);

    // 创建旋转矩阵
    float4x4 rotateXMatrix = RotateX(angleX);
    float4x4 rotateZMatriy = RotateY(angleY);

    // 应用旋转矩阵
    float4 vectorToRotate = float4(0,0,1,1);
    vectorToRotate = mul(rotateXMatrix, vectorToRotate); // 绕X轴旋转
    vectorToRotate = mul(rotateZMatriy, vectorToRotate); // 绕Z轴旋转
    return  vectorToRotate.xyz;
}

//广告牌
float3 Billboard(float3 positionOS,bool lockZ = false){
    float3 newZ=TransformWorldToObjectDir(UNITY_MATRIX_V[2].xyz);//获得模型空间的相机坐标作为新坐标的z轴
    //锁定Z轴
    if(lockZ)
        newZ.y = 0;
    newZ=normalize(newZ);
    //根据Z的位置去判断x的方向
    float3 newX= cross(float3(0,1,0),newZ);
    newX=normalize(newX);
    float3 newY=cross(newZ,newX);
    newY=normalize(newY);
    float3x3 Matrix={newX,newY,newZ};//这里应该取矩阵的逆 但是hlsl没有取逆矩阵的函数 
    float3 newpos=mul(positionOS,Matrix);//故在mul函数里进行右乘 等同于
    return newpos;
}

float3 RotateZ(float3 position,float angle)
{
    float3 angleX = float3(cos(angle),sin(angle),0);
    float3 angleY = float3(-sin(angle),cos(angle),0);
    float3 angleZ = float3(0,0,0);

    position = mul(position,float3x3(angleX,angleY,angleZ));
    return position;
}

inline float unity_noise_randomValue (float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453);
}

inline float unity_noise_interpolate (float a, float b, float t)
{
    return (1.0-t)*a + (t*b);
}

inline float unity_valueNoise (float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);

    uv = abs(frac(uv) - 0.5);
    float2 c0 = i + float2(0.0, 0.0);
    float2 c1 = i + float2(1.0, 0.0);
    float2 c2 = i + float2(0.0, 1.0);
    float2 c3 = i + float2(1.0, 1.0);
    float r0 = unity_noise_randomValue(c0);
    float r1 = unity_noise_randomValue(c1);
    float r2 = unity_noise_randomValue(c2);
    float r3 = unity_noise_randomValue(c3);

    float bottomOfGrid = unity_noise_interpolate(r0, r1, f.x);
    float topOfGrid = unity_noise_interpolate(r2, r3, f.x);
    float t = unity_noise_interpolate(bottomOfGrid, topOfGrid, f.y);
    return t;
}

void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
{
    float t = 0.0;

    float freq = pow(2.0, float(0));
    float amp = pow(0.5, float(3-0));
    t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

    freq = pow(2.0, float(1));
    amp = pow(0.5, float(3-1));
    t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

    freq = pow(2.0, float(2));
    amp = pow(0.5, float(3-2));
    t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

    Out = t;
}
