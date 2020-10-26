Texture2D txDiffuse : register( t0 );
SamplerState samLinear : register( s0 );

cbuffer ConstantBuffer : register( b0 )
{
	matrix World;
	matrix View;
	matrix Projection;
}

struct VS_INPUT_UNLIT
{
    float4 Pos : POSITION;
    float2 Tex : TEXCOORD0;
};

struct PS_INPUT_UNLIT
{
    float4 Pos : SV_POSITION;
    float2 Tex : TEXCOORD0;
};

//Unlit Vertex Shader
PS_INPUT_UNLIT VS_UNLIT( VS_INPUT_UNLIT input )
{
    PS_INPUT_UNLIT output = (PS_INPUT_UNLIT)0;
    output.Pos = mul( input.Pos, World );
    output.Pos = mul( output.Pos, View );
    output.Pos = mul( output.Pos, Projection );
    output.Tex = input.Tex;
    
    return output;
}

//Unlit Pixel Shader
float4 PS_UNLIT( PS_INPUT_UNLIT input) : SV_Target
{
    return txDiffuse.Sample( samLinear, input.Tex );
}