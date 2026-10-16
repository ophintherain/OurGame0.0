Shader "Spotlight/WhiteKeySprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 pixel = tex2D(_MainTex, input.uv) * input.color;
                fixed lowest = min(pixel.r, min(pixel.g, pixel.b));
                fixed highest = max(pixel.r, max(pixel.g, pixel.b));
                fixed white = smoothstep(0.94, 1.0, lowest) *
                    (1.0 - smoothstep(0.01, 0.06, highest - lowest));
                pixel.a *= 1.0 - white;
                return pixel;
            }
            ENDCG
        }
    }
}
