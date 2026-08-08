Shader "Hidden/ES/DynamicAtlasCopyPadding"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ESAtlasCopyData;
            float _ESAtlasPremultiply;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexId : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
                output.position = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 padding = _ESAtlasCopyData.xy;
                float2 contentSize = max(_ESAtlasCopyData.zw, 1.0);
                float2 allocatedSize = contentSize + padding * 2.0;
                float2 uv = saturate((input.uv * allocatedSize - padding) / contentSize);
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                    uv.y = 1.0 - uv.y;
                #endif
                fixed4 color = tex2D(_MainTex, uv);
                color.rgb = lerp(color.rgb, color.rgb * color.a, _ESAtlasPremultiply);
                return color;
            }
            ENDCG
        }
    }
}
