// The route ahead in the camera view (screen A): a translucent teal track on the floor with white
// chevrons that move the way to go, as on blueprint screen A. It fades in a little ahead of the
// feet and out with distance, so it never covers the floor right under the phone or reaches far
// past where the position is reliable. The vertices are drawn slightly towards the camera, so the
// floor in ARKit's depth image does not cut the track, while walls (much closer) still hide it
// (decision record 12).
Shader "IndoorNavigation/RouteRibbon"
{
    Properties
    {
        _Color ("Track", Color) = (0.059, 0.463, 0.431, 0.55)
        _EdgeColor ("Track edge", Color) = (0.043, 0.361, 0.337, 0.85)
        _ChevronColor ("Chevrons", Color) = (1, 1, 1, 0.95)
        _Spacing ("Chevron spacing (m)", Float) = 0.9
        _Speed ("Chevron speed (m/s)", Float) = 0.5
        _Bend ("Chevron bend (m forward per m across)", Float) = 0.9
        _Near ("Fade in from, to (m)", Vector) = (0.5, 1.4, 0, 0)
        _Far ("Fade out from, to (m)", Vector) = (8, 14, 0, 0)
        _HalfWidth ("Half width (m)", Float) = 0.28
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Track"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EdgeColor;
            fixed4 _ChevronColor;
            float _Spacing;
            float _Speed;
            float _Bend;
            float4 _Near;
            float4 _Far;
            float _HalfWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;   // x: 0 left edge, 1 right edge; y: metres to the destination
                fixed4 color : COLOR;    // alpha: fade at a cut end
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float fromCamera : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 toCamera = _WorldSpaceCameraPos - world;
                float metres = length(toCamera);
                // Pull towards the camera by 5 cm plus 2 % of the distance: enough to win against the
                // floor's own depth, far less than a wall in front of the track.
                world += toCamera / max(metres, 1e-3) * (0.05 + 0.02 * metres);
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = v.uv;
                o.color = v.color;
                o.fromCamera = length(_WorldSpaceCameraPos.xz - world.xz);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float across = abs(i.uv.x - 0.5) * 2.0;             // 0 centre, 1 edge
                float acrossMetres = across * _HalfWidth;
                float aa = max(fwidth(i.uv.y), 1e-4);

                // Chevrons pointing the way: "forward" is towards the destination, where uv.y falls.
                float forward = -i.uv.y + _Bend * acrossMetres - _Speed * _Time.y;
                float cell = frac(forward / _Spacing) * _Spacing;    // metres into this chevron's cell
                float band = 0.22;                                   // chevron stroke, metres along
                float chevron = smoothstep(0.0, aa * 1.5, cell) * (1.0 - smoothstep(band - aa * 1.5, band, cell));
                chevron *= 1.0 - smoothstep(0.62, 0.72, across);     // only in the middle of the track

                // A slightly darker rim keeps the track's shape readable on teal-ish or busy floors.
                float rim = smoothstep(0.78, 0.86, across) * (1.0 - smoothstep(0.94, 1.0, across));
                fixed4 track = lerp(_Color, _EdgeColor, rim);
                fixed4 col = lerp(track, _ChevronColor, chevron);

                float edge = 1.0 - smoothstep(0.96, 1.0, across);
                float fadeIn = smoothstep(_Near.x, _Near.y, i.fromCamera);
                float fadeOut = 1.0 - smoothstep(_Far.x, _Far.y, i.fromCamera);
                col.a *= edge * fadeIn * fadeOut * i.color.a;
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
