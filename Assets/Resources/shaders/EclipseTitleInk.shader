Shader "Eclipse/UI/Title Ink"
{
    Properties { [PerRendererData] _MainTex ("Source", 2D) = "white" {} _WhiteInk ("White ink", Float) = 0
        _KeepRed ("Keep vermilion under white ink", Float) = 0 _BlackAlpha ("Black ink opacity", Float) = 1 _RedAlpha ("Vermilion opacity", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 mask : TEXCOORD1; };
            sampler2D _MainTex;
            float _WhiteInk, _KeepRed, _BlackAlpha, _RedAlpha;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color;
                // RectMask2D support (with softness), as in Unity's default UI shader.
                float2 pixelSize = o.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                // Recover only black and vermilion ink from the original loading plate.
                // The old baked paper remains in its source asset and never covers the new paper.
                float blackInk = 1 - smoothstep(.16, .43, max(c.r, max(c.g, c.b)));
                float redInk = smoothstep(.18, .42, c.r - max(c.g, c.b));
                fixed3 ink = lerp(fixed3(.045,.035,.025), fixed3(.56,.09,.06), redInk);
                // White ink can keep the vermilion seal, and either ink can be dropped so the
                // lettering and the seal draw (and animate) as separate layers.
                fixed3 colour = lerp(lerp(ink, fixed3(.97,.91,.79), _WhiteInk), fixed3(.72,.13,.09), redInk * _KeepRed);
                float alpha = max(blackInk * _BlackAlpha, redInk * _RedAlpha) * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                alpha *= m.x * m.y;
                #endif
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }
}
