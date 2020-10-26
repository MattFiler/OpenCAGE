SamplerState samLinear : register(s0);

cbuffer ConstantBuffer : register(b0)
{
	matrix World;
	matrix View;
	matrix Projection;
}

cbuffer ConstantBuffer1 : register(b1) {
    float4 colour;
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

float4 PS( PS_INPUT input) : SV_Target
{
	return colour;

}
