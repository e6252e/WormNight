using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public readonly struct AttachedSegmentDebugEntry
    {
        public readonly int DisplayIndex;
        public readonly int ChainIndex;
        public readonly int DamageMeterKey;
        public readonly string SegmentId;
        public readonly string DisplayName;
        public readonly int SegmentLevel;
        public readonly ConvoySegmentRuntime Runtime;

        public AttachedSegmentDebugEntry(int displayIndex, int chainIndex, int damageMeterKey, string segmentId, string displayName, int segmentLevel, ConvoySegmentRuntime runtime)
        {
            DisplayIndex = displayIndex;
            ChainIndex = chainIndex;
            DamageMeterKey = damageMeterKey;
            SegmentId = segmentId;
            DisplayName = displayName;
            SegmentLevel = Mathf.Max(1, segmentLevel);
            Runtime = runtime;
        }
    }

    public sealed partial class ConvoyController
    {
        public void CollectAttachedSegmentDebugEntries(List<AttachedSegmentDebugEntry> results) // DPS 미터용 현재 체인 순서
        {
            if (results == null)
            {
                return; // 수집 대상 없음
            }

            results.Clear(); // 이전 결과 제거
            SyncSegmentRuntimes(true); // 현재 체인 런타임 보정

            for (int i = 0; i < segmentRuntimes.Count; i++)
            {
                ConvoySegmentRuntime runtime = segmentRuntimes[i]; // 현재 세그먼트
                if (runtime == null || !runtime.IsAttached)
                {
                    continue; // 분리/비정상 제외
                }

                string segmentId = runtime.Weapon != null ? runtime.Weapon.EffectiveSegmentId : string.Empty; // 표시 ID
                string displayName = ResolveSegmentDebugDisplayName(runtime, segmentId); // 표시 이름
                results.Add(new AttachedSegmentDebugEntry(results.Count + 1, i, runtime.DamageMeterKey, segmentId, displayName, runtime.SegmentLevel, runtime)); // 현재 순서 유지
            }
        }

        public bool TryGetAttachedSegmentRuntimeByChainIndex(int chainIndex, out ConvoySegmentRuntime runtime) // DamageData 인덱스 fallback
        {
            runtime = null; // 기본값
            if (chainIndex < 0)
            {
                return false; // 잘못된 인덱스
            }

            SyncSegmentRuntimes(true); // 현재 체인 런타임 보정
            if (chainIndex >= segmentRuntimes.Count)
            {
                return false; // 범위 밖
            }

            runtime = segmentRuntimes[chainIndex]; // 현재 런타임
            return runtime != null && runtime.IsAttached; // 연결된 세그먼트만 허용
        }

        private static string ResolveSegmentDebugDisplayName(ConvoySegmentRuntime runtime, string segmentId) // 카탈로그 이름 우선
        {
            if (!string.IsNullOrWhiteSpace(segmentId)
                && CoreStatProvider.Active != null
                && CoreStatProvider.Active.SegmentCatalogAsset != null
                && CoreStatProvider.Active.SegmentCatalogAsset.TryFind(segmentId, out SegmentDefinition definition)
                && definition != null
                && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName.Trim(); // 정식 표시명
            }

            if (!string.IsNullOrWhiteSpace(segmentId))
            {
                return segmentId.Trim(); // ID fallback
            }

            return runtime != null ? runtime.gameObject.name : "Segment"; // 최종 fallback
        }
    }
}
