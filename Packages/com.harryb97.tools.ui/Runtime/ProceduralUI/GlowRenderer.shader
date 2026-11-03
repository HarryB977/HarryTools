Shader "Hidden/HarryTools/UI/GlowRenderer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // Vertex attributes packed by GlowGraphic.OnPopulateMesh:
            //   uv0: texU, texV, width, height
            //   uv1: roundness (x, y, z, w)
            //   uv2: spread, blur, power, encodedFrame
            //        encodedFrame: 0 = solid shape; < 0 = frame mode, abs = 1 + placement*4096 + round(frameWidth)
            //   color: glow color (per-vertex gradient)

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float4 uv0    : TEXCOORD0;
                float4 uv1    : TEXCOORD1;
                float4 uv2    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 sdfPos     : TEXCOORD0;
                float4 roundness  : TEXCOORD1;
                float4 params     : TEXCOORD2; // xy=halfSize, z=spread, w=blur
                float2 params2    : TEXCOORD3; // x=power, y=encodedFrame
                float2 worldPos   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;

            float sdRoundedBox(float2 p, float2 b, float4 r)
            {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x = (p.y > 0.0) ? r.x : r.y;
                float2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }

            v2f vert(appdata v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.color = v.color * _Color;

                // UV transform for padding
                float2 size = v.uv0.zw;
                float spread = v.uv2.x;
                float blur = v.uv2.y;
                float frameVal = v.uv2.w;
                float frameOutward = 0;
                if (frameVal < -0.5)
                {
                    float absVal = abs(frameVal) - 1.0;
                    float placement = floor(absVal / 4096.0 + 0.001);
                    float fw = absVal - placement * 4096.0;
                    frameOutward = placement < 0.5 ? 0.0
                                 : placement < 1.5 ? fw * 0.5
                                 : fw;
                }
                float padding = spread + blur + frameOutward + 1.0;
                float2 normPad = padding / size;
                float2 uv = v.uv0.xy * (1 + normPad * 2) - normPad;

                OUT.sdfPos = (uv - 0.5) * size;
                OUT.roundness = v.uv1;
                OUT.params = float4(size * 0.5, spread, blur);
                OUT.params2 = float2(v.uv2.z, frameVal);
                OUT.worldPos = v.vertex.xy;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 halfSize = IN.params.xy;
                float spread = IN.params.z;
                float blur = max(IN.params.w, 0.001);
                float power = IN.params2.x;
                float frameVal = IN.params2.y;

                float dist = sdRoundedBox(IN.sdfPos, halfSize, IN.roundness);

                // In frame mode, derive ring SDF so glow wraps both ring edges
                float sdf;
                if (frameVal < -0.5)
                {
                    float absVal = abs(frameVal) - 1.0;
                    float placement = floor(absVal / 4096.0 + 0.001);
                    float fw = absVal - placement * 4096.0;

                    float outerEdge, innerEdge;
                    if (placement < 0.5)       { outerEdge = 0.0;      innerEdge = -fw; }
                    else if (placement < 1.5)  { outerEdge = fw * 0.5; innerEdge = -fw * 0.5; }
                    else                       { outerEdge = fw;       innerEdge = 0.0; }

                    float ringCenter = (outerEdge + innerEdge) * 0.5;
                    float ringHalf = (outerEdge - innerEdge) * 0.5;
                    sdf = abs(dist - ringCenter) - ringHalf;
                }
                else
                {
                    sdf = dist;
                }

                // Glow: smooth falloff centered around 'spread' distance from shape/ring edge
                float glow = 1.0 - smoothstep(spread - blur, spread + blur, sdf);
                glow = pow(glow, power);

                // Premultiplied alpha output
                float alpha = IN.color.a * glow;
                half4 result;
                result.rgb = IN.color.rgb * alpha;
                result.a = alpha;

                #ifdef UNITY_UI_CLIP_RECT
                result *= UnityGet2DClipping(IN.worldPos, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
