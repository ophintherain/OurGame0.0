Shader "Spotlight/SceneTransition"
{
    Properties { _Progress ("Progress", Range(0, 1)) = 0; _Center ("Center", Vector) = (0.5, 0.5, 0, 0) }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Progress;
            float4 _Center;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.uv - _Center.xy;
                delta.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);
                float farRadius = length(float2(_ScreenParams.x / max(1.0, _ScreenParams.y), 1.0));
                float radius = lerp(farRadius, -0.03, saturate(_Progress));
                float alpha = smoothstep(radius - 0.03, radius + 0.03, length(delta));
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}
