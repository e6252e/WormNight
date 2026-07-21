using UnityEngine;
using UnityEngine.Rendering;

namespace TeamProject01.Gameplay
{
    internal static class SegmentAttackVfxPlayer // 공격 VFX 공용 생성
    {
        public static GameObject Play(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime) // 일반 VFX
        {
            if (prefab == null)
            {
                return null; // 지정 없음
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation); // 생성
            if (lifetime > 0f)
            {
                Object.Destroy(instance, lifetime); // 수명 제거
            }

            return instance;
        }

        public static GameObject PlayAttached(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, float lifetime) // 부착 VFX
        {
            if (prefab == null || parent == null)
            {
                return null; // 지정 없음
            }

            GameObject instance = Object.Instantiate(prefab, parent); // 소켓 하위 생성
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = localPosition; // 소켓 기준 위치
            instanceTransform.localRotation = localRotation; // 소켓 기준 방향
            instanceTransform.localScale = localScale; // 프로필 스케일
            if (lifetime > 0f)
            {
                Object.Destroy(instance, lifetime); // 수명 제거
            }

            return instance;
        }

        public static GameObject PlayExplosion(GameObject prefab, Vector3 position, float radius, float lifetime, float alpha) // 폭발 VFX
        {
            GameObject instance = Play(prefab, position, Quaternion.identity, lifetime); // 생성
            if (instance == null)
            {
                return null; // 지정 없음
            }

            SegmentExplosionVfxScaleSettings settings = instance.GetComponentInChildren<SegmentExplosionVfxScaleSettings>(true);
            float scale = settings != null ? settings.ResolveUniformScale(radius) : Mathf.Max(0f, radius) * 2f;
            instance.transform.localScale = Vector3.one * scale; // 범위 표시

            if (settings == null || settings.ApplyAlphaOverride)
            {
                ApplyTransparent(instance, alpha); // 임시 구체 투명 처리
            }

            return instance;
        }

        private static void ApplyTransparent(GameObject instance, float alpha) // 반투명 보정
        {
            if (instance == null)
            {
                return; // 대상 없음
            }

            float resolvedAlpha = Mathf.Clamp01(alpha); // 알파 보정
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true); // 하위 포함
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i]; // 렌더러
                renderer.shadowCastingMode = ShadowCastingMode.Off; // 그림자 제거
                renderer.receiveShadows = false; // 그림자 수신 제거

                Material[] materials = renderer.materials; // 인스턴스 재질
                for (int j = 0; j < materials.Length; j++)
                {
                    ConfigureTransparentMaterial(materials[j], resolvedAlpha); // 재질 투명
                }
            }
        }

        private static void ConfigureTransparentMaterial(Material material, float alpha) // URP/기본 셰이더 투명
        {
            if (material == null)
            {
                return; // 재질 없음
            }

            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor"); // URP 색
                color.a = alpha; // 알파 적용
                material.SetColor("_BaseColor", color); // 저장
            }

            if (material.HasProperty("_Color"))
            {
                Color color = material.GetColor("_Color"); // 기본 색
                color.a = alpha; // 알파 적용
                material.SetColor("_Color", color); // 저장
            }

            material.SetOverrideTag("RenderType", "Transparent"); // 투명 태그
            SetFloatIfPresent(material, "_Surface", 1f); // URP Transparent
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha); // 알파 블렌드
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha); // 알파 블렌드
            SetFloatIfPresent(material, "_ZWrite", 0f); // ZWrite Off
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); // URP 키워드
            material.EnableKeyword("_ALPHABLEND_ON"); // 기본 키워드
            material.renderQueue = (int)RenderQueue.Transparent; // 투명 큐
        }

        private static void SetFloatIfPresent(Material material, string property, float value) // 선택 속성 설정
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value); // 값 적용
            }
        }
    }
}
