using TMPro;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class DamageFloatingTextStyle // 데미지 숫자 TMP 스타일
    {
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.72f); // 그림자 색

        public static void Apply(TMP_Text text, ref Material runtimeMaterial) // 외곽/그림자 적용
        {
            if (text == null)
            {
                return; // 대상 없음
            }

            text.outlineWidth = 0f; // 외곽선 끔
            text.outlineColor = Color.clear; // 외곽선 색 초기화

            Material source = text.fontSharedMaterial != null ? text.fontSharedMaterial : text.fontMaterial; // 기준 재질
            if (source == null)
            {
                return; // 재질 없음
            }

            if (runtimeMaterial == null)
            {
                runtimeMaterial = new Material(source) { name = source.name + "_DamageFloating" }; // 팝업 전용 재질
            }

            text.fontMaterial = runtimeMaterial; // 런타임 재질 적용
            ApplyMaterial(runtimeMaterial); // 그림자 값 적용
        }

        private static void ApplyMaterial(Material material) // TMP underlay 설정
        {
            if (material == null)
            {
                return; // 재질 없음
            }

            material.EnableKeyword("UNDERLAY_ON"); // 그림자 활성화
            SetFloat(material, ShaderUtilities.ID_OutlineWidth, 0f); // 외곽선 제거
            SetColor(material, ShaderUtilities.ID_OutlineColor, Color.clear); // 외곽선 투명
            SetColor(material, ShaderUtilities.ID_UnderlayColor, ShadowColor); // 그림자 색
            SetFloat(material, ShaderUtilities.ID_UnderlayOffsetX, 0.18f); // 그림자 X
            SetFloat(material, ShaderUtilities.ID_UnderlayOffsetY, -0.18f); // 그림자 Y
            SetFloat(material, ShaderUtilities.ID_UnderlaySoftness, 0.2f); // 그림자 부드러움
        }

        private static void SetFloat(Material material, int id, float value) // float 안전 적용
        {
            if (material.HasProperty(id))
            {
                material.SetFloat(id, value); // 값 적용
            }
        }

        private static void SetColor(Material material, int id, Color value) // color 안전 적용
        {
            if (material.HasProperty(id))
            {
                material.SetColor(id, value); // 값 적용
            }
        }
    }
}
