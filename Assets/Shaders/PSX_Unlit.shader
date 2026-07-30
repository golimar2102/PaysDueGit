Shader "Retro/PSX_Unlit_Fixed"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _Resolution ("Grid Resolution (Wobble)", Range(1, 1024)) = 256
        
        // НОВОЕ: На каком расстоянии эффект PS1 полностью исчезнет (в метрах)
        _FadeDistance ("Effect Fade Distance", Float) = 20.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 uv           : TEXCOORD0; 
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Resolution;
                float _FadeDistance; // Добавили переменную
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert (Attributes input)
            {
                Varyings output;

                // Стандартная позиция
                float4 posCS = TransformObjectToHClip(input.positionOS.xyz);

                // --- НОВОЕ: Считаем дистанцию от камеры и коэффициент затухания ---
                // Затухание от 0 (эффект PS1 на 100%) до 1 (эффекта нет)
                float fade = saturate(posCS.w / _FadeDistance);

                // Vertex Snapping (Эффект PS1)
                float4 snappedPos = posCS;
                snappedPos.xyz /= snappedPos.w;
                snappedPos.xy = floor(snappedPos.xy * _Resolution) / _Resolution;
                snappedPos.xyz *= snappedPos.w;

                // Плавный переход от кривых вершин к ровным в зависимости от дальности
                output.positionCS = lerp(snappedPos, posCS, fade);

                // Affine Texture Mapping (Переход от PS1-текстур к ровным)
                float2 baseUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                
                // Магия: у PS1 мы делим на Z (глубину), у современных игр на 1. 
                // Плавно смешиваем этот делитель.
                float wBlend = lerp(output.positionCS.w, 1.0, fade);
                
                output.uv.xy = baseUV * wBlend;
                output.uv.z = wBlend; 

                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 finalUV = input.uv.xy / input.uv.z;

                // Использование POINT фильтрации для пикселизации текстуры
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * _Color;
                return col;
            }
            ENDHLSL
        }
    }
}