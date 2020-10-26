Texture2D txDiffuse : register( t0 );
Texture2D txDiffuse2 : register( t1 );
SamplerState samLinear : register( s0 );

cbuffer ConstantBuffer : register( b0 )
{
	matrix World;
	matrix View;
	matrix Projection;
}

cbuffer ConstantBuffer2 : register( b1 )
{
	float4 colourTint;
}

struct VS_INPUT
{
    float4 Pos : POSITION;
    float2 Tex : TEXCOORD0;
    float3 Norm : NORMAL;
};

struct PS_INPUT
{
    float4 Pos : SV_POSITION;
    float4 WorldPos : POSITION;
    float2 Tex : TEXCOORD0;
    float3 Norm : NORMAL;
};

//Base Vertex Shader
PS_INPUT VS( VS_INPUT input )
{
    PS_INPUT output = (PS_INPUT)0;
    output.Pos = mul( input.Pos, World );
    output.WorldPos = mul( input.Pos, World );
    output.Pos = mul( output.Pos, View );
    output.Pos = mul( output.Pos, Projection );
    output.Tex = input.Tex;
    output.Norm = input.Norm.xyz;
    
    return output;
}

//Base Pixel Shader
float4 PS( PS_INPUT input) : SV_Target
{
    return saturate((txDiffuse2.Sample( samLinear, input.Tex ) * colourTint) + (saturate((input.Norm.x + input.Norm.y + input.Norm.z)/3)));
}