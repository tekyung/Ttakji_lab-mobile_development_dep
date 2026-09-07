// UIGrayscale.shader — uGUI Image를 흑백으로 그린다.
//
// 왜 있는가:
//   용병이 고유 능력을 썼다는 표시를 카드 회전에서 흑백으로 바꾸면서 필요해졌다.
//   Image.color 틴트로는 어둡게만 될 뿐 채도를 못 없앤다. 셰이더가 있어야 진짜 흑백이 된다.
//
// ★ Unity 빌트인 UI/Default를 그대로 베껴 오고 프래그먼트 끝에서 채도만 0으로 만들었다.
//   스텐실·클리핑(_ClipRect)·알파 컷은 손대지 않았다 —
//   그걸 빼면 Mask 안에서 잘리지 않거나 정렬이 어긋난다.
//
// 밝기 계산은 사람 눈의 감도(Rec. 601)를 따른다. 단순 평균을 쓰면 빨강이 너무 밝게 뜬다.
Shader "UI/Grayscale"
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

        // 0이면 원본 그대로, 1이면 완전 흑백. 중간값으로 반쯤 바랜 느낌도 낼 수 있다.
        _GrayAmount ("Gray Amount", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _GrayAmount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                // ★ 여기만 UI/Default와 다르다.
                //   Rec. 601 가중치 — 사람 눈은 초록에 가장 민감하고 파랑에 가장 둔하다.
                half gray = dot(color.rgb, half3(0.299, 0.587, 0.114));
                color.rgb = lerp(color.rgb, half3(gray, gray, gray), _GrayAmount);

                return color;
            }
        ENDCG
        }
    }
}
