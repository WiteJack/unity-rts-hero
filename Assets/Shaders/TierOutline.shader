// ОБНОВЛЕНИЕ 33: шейдер тирового контура нейтралов.
// Классический «hull outline»: рисуем меш вывернутыми (Cull Front) треугольниками,
// выдавленными вдоль нормалей — получается обводка силуэта цветом тира.
// Базовый материал юнита НЕ трогается: текстура шерсти остаётся натуральной,
// контур добавляется ВТОРЫМ слотом материалов через NeutralFactionTag.RecolorByTier().
// Работает и на SkinnedMeshRenderer: GPU-скиннинг в Unity выполняется до вершинного шейдера.
Shader "Andron/TierOutline"
{
    Properties
    {
        _OutlineColor ("Цвет контура", Color) = (1, 0.92, 0.2, 1)
        _OutlineWidth ("Толщина контура (в единицах меша)", Range(0.0, 0.2)) = 0.02
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }
        Pass
        {
            Name "TierOutline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 inflatedOS = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(inflatedOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
