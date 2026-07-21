using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [CreateAssetMenu(menuName = "OZ/Segments/Segment Catalog", fileName = "SegmentCatalog")]
    public sealed class SegmentCatalogAsset : ScriptableObject // 전체 세그먼트 목록
    {
        public SegmentDefinition[] Segments = Array.Empty<SegmentDefinition>(); // 등록된 세그먼트들

        public bool TryFind(string segmentId, out SegmentDefinition definition) // ID 검색
        {
            definition = null; // 기본값
            if (Segments == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return false; // 검색 불가
            }

            string normalizedId = segmentId.Trim(); // 비교 ID
            for (int i = 0; i < Segments.Length; i++)
            {
                SegmentDefinition candidate = Segments[i];
                if (candidate != null && string.Equals(candidate.NormalizedId, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate; // 발견
                    return true;
                }
            }

            return false; // 없음
        }

        public bool TryGetPrefab(string segmentId, int level, out GameObject prefab) // ID + 레벨 → 프리팹
        {
            prefab = null; // 기본값
            return TryFind(segmentId, out SegmentDefinition definition) && definition.TryGetSegmentPrefab(level, out prefab);
        }

        public SegmentCatalogEntry[] BuildCatalogEntries() // 기존 카탈로그 배열 생성
        {
            if (Segments == null || Segments.Length == 0)
            {
                return Array.Empty<SegmentCatalogEntry>(); // 비어 있음
            }

            List<SegmentCatalogEntry> entries = new List<SegmentCatalogEntry>(Segments.Length);
            for (int i = 0; i < Segments.Length; i++)
            {
                SegmentDefinition definition = Segments[i];
                if (definition == null || !definition.HasId)
                {
                    continue; // 빈 슬롯
                }

                entries.Add(definition.ToCatalogEntry()); // 호환 등록
            }

            return entries.ToArray();
        }
    }
}
