Shader "Hidden/PSX_PostProcess"
{
    Properties
    {
        _ColorDepth ("Color Depth", Float) = 64
        _DitherStrength ("Dither Strength", Float) = 0.05
        
        // НОВЫЕ НАСТРОЙКИ ДЛЯ ИНСПЕКТОРА
        _BlackPoint ("Black Point", Range(0.0, 0.2)) = 0.005
        _ShadowFade ("Shadow Fade (Softness)", Range(1.0, 100.0)) = 50.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PSX_Effect"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColorDepth;
            float _DitherStrength;
            
            // Объявляем переменные для кода
            float _BlackPoint;
            float _ShadowFade;

            static const float ditherMatrix[16] = {
                0.0, 0.5, 0.125, 0.625,
                0.75, 0.25, 0.875, 0.375,
                0.1875, 0.6875, 0.0625, 0.5625,
                0.9375, 0.4375, 0.8125, 0.3125
            };

            half4 frag(Varyings input) : SV_Target
            {
                // Забираем пиксель
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, input.texcoord);
                
                // Вычисляем оригинальную яркость пикселя ДО эффектов
                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));

                // Координаты для дизеринга
                uint2 pixelCoord = uint2(input.texcoord.x * _ScreenParams.x, input.texcoord.y * _ScreenParams.y);
                int x = pixelCoord.x % 4;
                int y = pixelCoord.y % 4;
                
                float ditherValue = ditherMatrix[x + y * 4] - 0.5; 

                // Применяем PSX эффекты
                col.rgb += ditherValue * _DitherStrength;
                col.rgb = floor(col.rgb * _ColorDepth + 0.5) / _ColorDepth;

                // СОЗДАЕМ МАСКУ ТЕМНОТЫ НА ОСНОВЕ ПОЛЗУНКОВ ИЗ ИНСПЕКТОРА
                // Вычитаем BlackPoint (всё, что было темнее него, станет нулем или отрицательным)
                // Умножаем на ShadowFade для настройки резкости перехода
                // saturate обрезает значения, оставляя их строго от 0 до 1
                float shadowMask = saturate((luminance - _BlackPoint) * _ShadowFade);

                // Применяем маску к финальному цвету
                col.rgb *= shadowMask;

                return col;
            }
            ENDHLSL
        }
    }
}