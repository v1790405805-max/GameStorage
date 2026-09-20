#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float4x4 _InverseProjectionMatrix;//转换矩阵
float4x4 _InverseViewMatrix;//转换矩阵

TEXTURE2D(_CameraDepthTexture);
TEXTURE2D(_CameraNormalsTexture);
SAMPLER(sampler_CameraDepthTexture);
SAMPLER(sampler_CameraNormalsTexture);

float2 _uv;

//通过深度重建世界空间坐标
float4 GetWorldSpacePosition(float depth, float2 uv)
{
    // 屏幕空间 --> 视锥空间
    float4 view_vector = mul(_InverseProjectionMatrix, float4(2.0 * uv - 1.0, depth, 1.0));
    view_vector.xyz /= view_vector.w;
    //视锥空间 --> 世界空间
    float4x4 l_matViewInv = _InverseViewMatrix;
    float4 world_vector = mul(l_matViewInv, float4(view_vector.xyz, 1));
    return world_vector;
}

void PositionWorldToScreen(float3 position,inout  float2 uv, inout float depth)
{
    float4 posVS = mul(Inverse(_InverseViewMatrix),float4(position,1));
    posVS.xyz *= posVS.w;
    float4 uvPos = mul(Inverse(_InverseProjectionMatrix),posVS);
    uv = (uvPos.xy + 1) / 2;
    depth = uvPos.z;
}


struct LightSDF
{
    float4x4 Matrix;
    float Intensity;
    float4 Color;
    float2 SegmentRange;
    uint SegmentNum;
    bool UseNormal;
    float LightPow;
    float SegmentPow;
};

struct SphereSDF
{
    LightSDF Light;
    float S;
};
StructuredBuffer<SphereSDF> _sphereBuffer;
int _sphereCount;

struct BoxSDF
{
    LightSDF Light;
    float3 B;
    float R;
};
StructuredBuffer<BoxSDF> _boxBuffer;
int _boxCount;

struct TorusSDF
{
    LightSDF Light;
    float3 T;
};
StructuredBuffer<TorusSDF> _torusBuffer;
int _torusCount;

struct CapsuleSDF
{
    LightSDF Light;
    float H;
    float R;
};
StructuredBuffer<CapsuleSDF> _capsuleBuffer;
int _capsuleCount;

float _SDFSmooth;


//圆形
float sdSphere( float3 p, float s )
{
    return length(p)-s;
}

//Box
float sdBox( float3 p, float3 b ,float r)
{
    //float3 q = abs(p) - b;
    //return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);

    float3 q = abs(p) - b + r;
    return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0) - r;
}

//甜甜圈
float sdTorus( float3 p, float3 t )
{
    float2 q = float2(length(p.xz)-t.x,p.y);
    return length(q)-t.y;
}

//胶囊
float sdCapsule( float3 p, float3 a, float3 b, float r )
{
    float3 pa = p - a, ba = b - a;
    float h = clamp( dot(pa,ba)/dot(ba,ba), 0.0, 1.0 );
    return length( pa - ba*h ) - r;
}

float sdVerticalCapsule( float3 p, float h, float r )
{
    p.y -= clamp( p.y, 0.0, h );
    return length( p ) - r;
}


//运算
//并集
float opSmoothUnion( float d1, float d2, float k )
{
    float h = clamp( 0.5 + 0.5*(d2-d1)/k, 0.0, 1.0 );
    return lerp( d2, d1, h ) - k*h*(1.0-h);
}

//差集
float opSmoothSubtraction( float d1, float d2, float k )
{
    float h = clamp( 0.5 - 0.5*(d2+d1)/k, 0.0, 1.0 );
    return lerp( d2, -d1, h ) + k*h*(1.0-h);
}

//交集
float opSmoothIntersection( float d1, float d2, float k )
{
    float h = clamp( 0.5 - 0.5*(d2-d1)/k, 0.0, 1.0 );
    return lerp( d2, d1, h ) + k*h*(1.0-h);
}


float3 opTx( float3 p,  float4x4 t)
{
    return mul(Inverse(t), float4(p,1)).xyz;
}

void Segment(uint SegmentNum,float2 range,float lightPow,float segmentPow,inout float s)
{
    s = clamp(range.x,range.y,s);
    s = lerp(0,1,(s - range.x) / (range.y - range.x));
    s = max(0,s);
    s = pow(s,lightPow);
    for(uint i = 0;i < SegmentNum; i++)
    {
        float value = ((float)i+1.0) / (float)SegmentNum;
        if( s < value )
        {
            value = max(0,value);
            s = pow(value,segmentPow);
            return;
        }
    }
}

void SDFLight(LightSDF data,inout float sdf)
{
    sdf = max(0,sdf);
    if(data.SegmentNum>0)
    {
        Segment(data.SegmentNum,data.SegmentRange,data.LightPow,data.SegmentPow,sdf);
    }
    sdf *= data.Intensity;
    // float3 outlineMask = SAMPLE_TEXTURE2D_LOD(_OutlineTexture,sampler_OutlineTexture,_uv,0).rgb;
    // float outline = min(1,outlineMask.r + outlineMask.g + outlineMask.b);
    // outline *= lerp(0,1,data.LineIntensity);
    // outline += 1;
    // sdf *= outline;
}

float3 _SphereColor(float3 pos,float3 normal)
{
    float3 sdfColor = 0;
    for(int i=0;i<_sphereCount;i++)
    {
        SphereSDF sdfData = _sphereBuffer[i];
        float3 newPos = mul(Inverse(sdfData.Light.Matrix),float4(pos,1)).xyz;
        float s = -sdSphere(newPos,sdfData.S);
        if(s<0)
            continue;
        float diffuse = 1;
        if(sdfData.Light.UseNormal)
        {
            float3 newNormal = normalize(mul(Inverse(sdfData.Light.Matrix),float4(normal,0))).xyz;
            float newS = -sdSphere(newPos + newNormal * 0.01,sdfData.S);
            diffuse = clamp(-1,1,(newS - s)/0.01) * 0.5 + 0.5;
        }
        s *= diffuse;
        SDFLight(sdfData.Light,s);
        float3 color = float3(sdfData.Light.Color.rgb * s);
        sdfColor += color;
    }
    return sdfColor;
}

float3 _BoxColor(float3 pos,float3 normal)
{
    float3 sdfColor = 0;
    for(int i=0;i<_boxCount;i++)
    {
        BoxSDF sdfData = _boxBuffer[i];
        float3 newPos = mul(Inverse(sdfData.Light.Matrix),float4(pos,1)).xyz;
        float s = -sdBox(newPos,sdfData.B,sdfData.R);
        if(s<0)
            continue;
        float diffuse = 1;
        if(sdfData.Light.UseNormal)
        {
            float3 newNormal = normalize(mul(Inverse(sdfData.Light.Matrix),float4(normal,0))).xyz;
            float newS = -sdBox(newPos + newNormal * 0.01,sdfData.B,sdfData.R);
            diffuse = clamp(-1,1,(newS - s)/0.01) * 0.5 + 0.5;
        }
        s *= diffuse;
        SDFLight(sdfData.Light,s);
        float3 color = float3(sdfData.Light.Color.rgb * s);
        sdfColor += color;
    }
    return sdfColor;
}

float3 _SampleTorus(float3 pos,float3 normal)
{
    float3 sdfColor = 0;
    for(int i=0;i<_torusCount;i++)
    {
        TorusSDF sdfData = _torusBuffer[i];
        float3 newPos = mul(Inverse(sdfData.Light.Matrix),float4(pos,1)).xyz;
        float s = -sdTorus(newPos,sdfData.T);
        if(s<0)
            continue;
        float diffuse = 1;
        if(sdfData.Light.UseNormal)
        {
            float3 newNormal = normalize(mul(Inverse(sdfData.Light.Matrix),float4(normal,0))).xyz;
            float newS = -sdTorus(newPos + newNormal * 0.01,sdfData.T);
            diffuse = clamp(-1,1,(newS - s)/0.01) * 0.5 + 0.5;
        }
        s *= diffuse;
        SDFLight(sdfData.Light,s);
        float3 color = float3(sdfData.Light.Color.rgb * s);
        sdfColor += color;
    }
    return sdfColor;
}

float3 _SampleCapsule(float3 pos,float3 normal)
{
    float3 sdfColor = 0;
    for(int i=0;i<_capsuleCount;i++)
    {
        CapsuleSDF sdfData = _capsuleBuffer[i];
        float3 newPos = mul(Inverse(sdfData.Light.Matrix),float4(pos,1)).xyz;
        float s = -sdVerticalCapsule(newPos,sdfData.H,sdfData.R);
        if(s<0)
            continue;
        float diffuse = 1;
        if(sdfData.Light.UseNormal)
        {
            float3 newNormal = normalize(mul(Inverse(sdfData.Light.Matrix),float4(normal,0))).xyz;
            float newS = -sdVerticalCapsule(newPos + newNormal * 0.01,sdfData.H,sdfData.R);
            diffuse = clamp(-1,1,(newS - s)/0.01) * 0.5 + 0.5;
        }
        s *= diffuse;
        SDFLight(sdfData.Light,s);
        float3 color = float3(sdfData.Light.Color.rgb * s);
        sdfColor += color;
    }
    return sdfColor;
}

float3 _PointLight(float3 pos,float3 normal,float2 uv)
{
    float3 pointColor = 0;
    _sphereCount = min(20,_sphereCount);
    for(int i=0;i<_sphereCount;i++)
    {
        SphereSDF sdfData = _sphereBuffer[i];
        float3 lightPos = float3(sdfData.Light.Matrix[0].w,sdfData.Light.Matrix[1].w,sdfData.Light.Matrix[2].w);
        float r = sdfData.S;
        float s = 1 - distance(pos,lightPos)/r;
        if(s<0)
            continue;
        float diffuse = 1;
        if(sdfData.Light.UseNormal)
        {
            float3 lightDir = normalize(lightPos - pos);
            diffuse = dot(lightDir,normal) * 0.5 + 0.5;
        }
        s *= diffuse;
        //开启屏幕空间阴影
        // float2 lightUV;
        // float lightDepth;
        // PositionWorldToScreen(lightPos,lightUV,lightDepth);
        // float2 lDirSS = uv - lightUV;
        // float lenght = floor(length(float2(lDirSS.x * _ScreenParams.x,lDirSS.y * _ScreenParams.y)));
        // //lenght = min(50,lenght);
        // float3 lDirWS = (pos - lightPos)/lenght;
        // float3 samplePos = lightPos;
        // bool isBlock = false;
        // for(int j=1;j<lenght;j++)
        // {
        //     samplePos += lDirWS;
        //     float2 sampleUV;
        //     float sampleDepth;
        //     PositionWorldToScreen(samplePos,sampleUV,sampleDepth);
        //     float screenDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture,sampler_CameraDepthTexture,sampleUV,0);
        //     if(screenDepth - 0.001 > sampleDepth)
        //     {
        //         isBlock = true;
        //         break; 
        //     }
        // }
        // if(isBlock)
        //     continue;
        
        SDFLight(sdfData.Light,s);
        float3 color = float3(sdfData.Light.Color.rgb * s);
        pointColor += color;
    }
    return pointColor;
}

float3 _SDFColor(float3 pos,float3 normal,float2 uv)
{
    float3 sdfColor = 0;
    _uv = uv;
    if(_sphereCount>0)
        sdfColor += _PointLight(pos,normal,uv);
    if(_boxCount>0)
        sdfColor += _BoxColor(pos,normal);
    if(_torusCount>0)
        sdfColor += _SampleTorus(pos,normal);
    if(_capsuleCount>0)
        sdfColor += _SampleCapsule(pos,normal);
    return sdfColor;
}