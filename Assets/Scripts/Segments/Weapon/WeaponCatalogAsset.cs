using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TeamProject01.Gameplay
{
    [Serializable]
    public struct WeaponSegmentEnhancementGroup // 세그먼트 1종 = Cannon/Missile 과 같은 블록
    {
        [Tooltip("세그먼트 ID — SegmentCatalog.SegmentId 와 동일 (예: SG03_RapidShot)")]
        public string SegmentId; // 대상 세그먼트

        [Tooltip("이 세그먼트 강화 카드 (보통 3개 — Damage/Speed 등)")]
        public WeaponDefinition[] Enhancements; // WeaponDefinition 목록

        public string NormalizedSegmentId => string.IsNullOrWhiteSpace(SegmentId) ? string.Empty : SegmentId.Trim(); // 비교 ID
        public bool HasSegmentId => !string.IsNullOrWhiteSpace(SegmentId); // ID 존재
        public bool HasEnhancements => Enhancements != null && Enhancements.Length > 0; // 강화 목록 존재
        public bool IsValid => HasSegmentId && HasEnhancements; // 유효 블록
    }

    [CreateAssetMenu(menuName = "OZ/Segments/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalogAsset : ScriptableObject // 무기 강화 2단계 카드용 카탈로그
    {
        [Header("Cannon")]
        public WeaponDefinition[] CannonEnhancements = Array.Empty<WeaponDefinition>(); // SG01_Cannon

        [Header("Missile")]
        public WeaponDefinition[] MissileEnhancements = Array.Empty<WeaponDefinition>(); // SG02_Missile

        [Header("Additional Segments")]
        [Tooltip("Size + 로 세그먼트 블록 추가 → Segment Id + Enhancements(3개)")]
        [FormerlySerializedAs("AdditionalEnhancements")]
        [FormerlySerializedAs("AdditionalCategories")]
        public WeaponSegmentEnhancementGroup[] AdditionalSegments = Array.Empty<WeaponSegmentEnhancementGroup>(); // SG03~

        public bool TryFind(string enhancementId, out WeaponDefinition definition) // 강화 ID 검색
        {
            definition = null; // 기본값
            if (string.IsNullOrWhiteSpace(enhancementId))
            {
                return false; // 검색 불가
            }

            string normalizedId = enhancementId.Trim(); // 비교 ID
            if (TryFindInArray(CannonEnhancements, normalizedId, out definition))
            {
                return true; // 캐논
            }

            if (TryFindInArray(MissileEnhancements, normalizedId, out definition))
            {
                return true; // 미사일
            }

            return TryFindInAdditionalSegments(normalizedId, out definition); // 추가 세그먼트
        }

        public bool TryGetEnhancementsForSegment(string targetSegmentId, out WeaponDefinition[] enhancements) // 세그먼트 ID → 강화 목록
        {
            enhancements = Array.Empty<WeaponDefinition>(); // 기본값
            if (string.IsNullOrWhiteSpace(targetSegmentId))
            {
                return false; // 대상 없음
            }

            string normalizedTarget = targetSegmentId.Trim(); // 비교 ID
            if (string.Equals(normalizedTarget, "SG01_Cannon", StringComparison.OrdinalIgnoreCase)
                && CannonEnhancements != null
                && CannonEnhancements.Length > 0)
            {
                enhancements = CannonEnhancements;
                return true;
            }

            if (string.Equals(normalizedTarget, "SG02_Missile", StringComparison.OrdinalIgnoreCase)
                && MissileEnhancements != null
                && MissileEnhancements.Length > 0)
            {
                enhancements = MissileEnhancements;
                return true;
            }

            return TryGetAdditionalSegmentEnhancements(normalizedTarget, out enhancements); // 추가 세그먼트
        }

        public void AppendAllEnhancements(List<WeaponDefinition> results) // 전체 강화 풀 (CardUI 랜덤용)
        {
            if (results == null)
            {
                return; // 대상 없음
            }

            AppendValidDefinitions(results, CannonEnhancements);
            AppendValidDefinitions(results, MissileEnhancements);
            AppendValidDefinitionsFromAdditionalSegments(results);
        }

        public void ForEachAdditionalSegmentId(Action<string> action) // 추가 세그먼트 ID 순회 (디버그)
        {
            if (action == null || AdditionalSegments == null)
            {
                return; // 처리 없음
            }

            for (int i = 0; i < AdditionalSegments.Length; i++)
            {
                WeaponSegmentEnhancementGroup group = AdditionalSegments[i]; // 세그먼트 블록 1개
                if (group.IsValid)
                {
                    action(group.NormalizedSegmentId);
                }
            }
        }

        private bool TryFindInAdditionalSegments(string normalizedId, out WeaponDefinition definition)
        {
            definition = null; // 기본값
            if (AdditionalSegments == null)
            {
                return false; // 블록 없음
            }

            for (int i = 0; i < AdditionalSegments.Length; i++)
            {
                if (TryFindInArray(AdditionalSegments[i].Enhancements, normalizedId, out definition))
                {
                    return true; // 발견
                }
            }

            return false; // 없음
        }

        private bool TryGetAdditionalSegmentEnhancements(string normalizedTarget, out WeaponDefinition[] enhancements)
        {
            enhancements = Array.Empty<WeaponDefinition>(); // 기본값
            if (AdditionalSegments == null)
            {
                return false; // 블록 없음
            }

            for (int i = 0; i < AdditionalSegments.Length; i++)
            {
                WeaponSegmentEnhancementGroup group = AdditionalSegments[i]; // 후보 블록
                if (!group.IsValid)
                {
                    continue; // 비어 있음
                }

                if (string.Equals(group.NormalizedSegmentId, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    enhancements = group.Enhancements;
                    return true; // 매칭
                }
            }

            return false; // 없음
        }

        private void AppendValidDefinitionsFromAdditionalSegments(List<WeaponDefinition> results)
        {
            if (AdditionalSegments == null)
            {
                return; // 블록 없음
            }

            for (int i = 0; i < AdditionalSegments.Length; i++)
            {
                AppendValidDefinitions(results, AdditionalSegments[i].Enhancements);
            }
        }

        private static void AppendValidDefinitions(List<WeaponDefinition> results, WeaponDefinition[] entries) // 유효 강화만 풀에 추가
        {
            if (entries == null || entries.Length == 0)
            {
                return; // 항목 없음
            }

            for (int i = 0; i < entries.Length; i++)
            {
                WeaponDefinition definition = entries[i]; // 후보
                if (definition == null || !definition.HasAnyStatBonus || !definition.HasTarget)
                {
                    continue; // 적용 불가 제외
                }

                results.Add(definition); // 풀 등록
            }
        }

        private static bool TryFindInArray(WeaponDefinition[] entries, string normalizedId, out WeaponDefinition definition) // 배열 내부 검색
        {
            definition = null; // 기본값
            if (entries == null || entries.Length == 0)
            {
                return false; // 비어 있음
            }

            for (int i = 0; i < entries.Length; i++)
            {
                WeaponDefinition candidate = entries[i]; // 후보
                if (candidate != null && string.Equals(candidate.NormalizedId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate; // 발견
                    return true;
                }
            }

            return false; // 없음
        }
    }
}
