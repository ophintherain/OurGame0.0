Shader "Spotlight/BossDefeatDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0, 0.2)) = 0.055
        _EdgeColor ("Edge Color", Color) = (1, 0.42, 0.12, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha

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
            fixed4 _EdgeColor;
            float _Dissolve;
            float _EdgeWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 pixel = tex2D(_MainTex, i.uv) * i.color;
                float2 cell = floor(i.uv * float2(117, 173));
                float noise = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                float threshold = saturate(_Dissolve) * 1.15 - 0.075;
                float visible = step(threshold, noise);
                float edge = visible * (1 - step(threshold + _EdgeWidth, noise));
                pixel.rgb = lerp(pixel.rgb, _EdgeColor.rgb * pixel.a, edge);
                pixel.a *= visible;
                pixel.rgb *= pixel.a;
                return pixel;
            }
            ENDCG
        }
    }
}
