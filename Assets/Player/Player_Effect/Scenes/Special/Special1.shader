Shader "Custom/CrescentReveal"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _Color ("Base Color", Color) = (0.15,0.6,1,1)
        _GlowColor ("Glow Color", Color) = (0.6,0.9,1,1)
        _Progress ("Reveal Progress (0-1)", Range(0,1)) = 0
        _StartAngle ("Start Angle (deg)", Range(-180,180)) = -90
        _EndAngle ("End Angle (deg)", Range(-180,180)) = 90
        _EdgeSmooth ("Edge Smoothness", Range(0.001,0.5)) = 0.06
        _GlowWidth ("Glow Width", Range(0.01,0.5)) = 0.06
        _GlowIntensity ("Glow Intensity", Range(0,8)) = 4
        _CenterOffset ("Center Offset (X,Z)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _Progress;
            float _StartAngle;
            float _EndAngle;
            float _EdgeSmooth;
            float _GlowWidth;
            float _GlowIntensity;
            float4 _CenterOffset; // x = centerX, z = centerZ

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // helper: map angle in degrees to 0..1 between start and end
            float AngleTo01(float angDeg, float startDeg, float endDeg)
            {
                // normalize angles to [-180,180]
                float s = startDeg;
                float e = endDeg;
                float len = e - s;
                if (abs(len) < 0.0001) return 0;
                float t = (angDeg - s) / len;
                return saturate(t);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // center in object/world space: use worldPos.xz with offset
                float2 p = i.worldPos.xz - float2(_CenterOffset.x, _CenterOffset.z);

                // compute angle in degrees: atan2(y, x)
                float ang = atan2(p.y, p.x) * 57.2957795; // rad->deg

                // Map angle into 0..1 between start and end
                float mapped = AngleTo01(ang, _StartAngle, _EndAngle);

                // reveal based on _Progress
                float reveal = smoothstep(_Progress - _EdgeSmooth, _Progress, mapped);

                // end glow: compute distance from mapped to _Progress
                float distToTip = abs(mapped - _Progress);
                float glowFac = smoothstep(_GlowWidth, 0.0, distToTip); // 1 at tip

                // sample main texture
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 baseCol = _Color * tex;
                baseCol.a *= reveal;

                // emission from reveal + tip glow
                fixed4 emission = _GlowColor * (reveal * 0.6 + glowFac * _GlowIntensity);

                // final color (additive emission for glow; alpha for silhouette)
                fixed4 outCol;
                outCol.rgb = baseCol.rgb + emission.rgb;
                outCol.a = baseCol.a;

                return outCol;
            }
            ENDCG
        }
    }
}
