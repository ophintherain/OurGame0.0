Shader "Spotlight/RuleDarkMask"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Hole0 ("Hole 0", Vector) = (0.5, 0.5, 0.1, 0.1)
        _Hole1 ("Hole 1", Vector) = (0.5, 0.5, 0.1, 0.1)
        _Hole2 ("Hole 2", Vector) = (0.5, 0.5, 0.1, 0.1)
        _Hole3 ("Hole 3", Vector) = (0.5, 0.5, 0.1, 0.1)
        _HoleCount ("Hole Count", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _Hole0;
            float4 _Hole1;
            float4 _Hole2;
            float4 _Hole3;
            float _HoleCount;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            bool InsideHole(float2 uv, float4 hole)
            {
                float2 halfSize = hole.zw * 0.5;
                return abs(uv.x - hole.x) <= halfSize.x &&
                       abs(uv.y - hole.y) <= halfSize.y;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                if (_HoleCount >= 1 && InsideHole(input.uv, _Hole0)) discard;
                if (_HoleCount >= 2 && InsideHole(input.uv, _Hole1)) discard;
                if (_HoleCount >= 3 && InsideHole(input.uv, _Hole2)) discard;
                if (_HoleCount >= 4 && InsideHole(input.uv, _Hole3)) discard;

                return fixed4(_Color.rgb, _Color.a);
            }
            ENDCG
        }
    }
}
