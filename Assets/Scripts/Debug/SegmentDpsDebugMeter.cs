using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public readonly struct SegmentDpsDebugSnapshot
    {
        public readonly float TotalDamage;
        public readonly float WaveDamage;
        public readonly float TotalDps;
        public readonly float WaveDps;

        public SegmentDpsDebugSnapshot(float totalDamage, float waveDamage, float totalDps, float waveDps)
        {
            TotalDamage = totalDamage;
            WaveDamage = waveDamage;
            TotalDps = totalDps;
            WaveDps = waveDps;
        }
    }

    public static class SegmentDpsDebugMeter
    {
        private sealed class DamageBucket
        {
            public float TotalDamage;
            public float WaveDamage;
        }

        private static readonly Dictionary<int, DamageBucket> bucketsBySegmentKey = new Dictionary<int, DamageBucket>(64); // 세그먼트별 누적값
        private static float runStartTime; // 전체 DPS 기준 시간
        private static float waveStartTime; // 현재 웨이브 DPS 기준 시간
        private static bool initialized; // 시간 초기화 여부

        public static float TotalElapsedSeconds => initialized ? Mathf.Max(0.001f, Time.time - runStartTime) : 0.001f;
        public static float WaveElapsedSeconds => initialized ? Mathf.Max(0.001f, Time.time - waveStartTime) : 0.001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetOnSceneLoad()
        {
            ResetRun(); // Play 시작/씬 전환 시 초기화
        }

        public static void ResetRun() // 스테이지 런 시작
        {
            bucketsBySegmentKey.Clear(); // 이전 런 제거
            float now = Time.time;
            runStartTime = now;
            waveStartTime = now;
            initialized = true;
        }

        public static void BeginWave(int stage) // 새 웨이브 시작
        {
            EnsureInitialized(); // 시간 보장
            waveStartTime = Time.time;

            foreach (DamageBucket bucket in bucketsBySegmentKey.Values)
            {
                bucket.WaveDamage = 0f; // 웨이브 값만 리셋
            }
        }

        public static void RecordDamage(DamageData damage, float actualDamage) // 실제 HP 감소량 기록
        {
            if (actualDamage <= 0f)
            {
                return; // 오버킬/무효 피해 제외
            }

            if (!TryResolveRuntime(damage, out ConvoySegmentRuntime runtime))
            {
                return; // 세그먼트 출처 없음
            }

            int key = runtime.DamageMeterKey;
            if (key <= 0)
            {
                return; // 키 없음
            }

            EnsureInitialized(); // 시간 보장
            DamageBucket bucket = GetOrCreateBucket(key);
            bucket.TotalDamage += actualDamage;
            bucket.WaveDamage += actualDamage;
        }

        public static bool TryGetSnapshot(int damageMeterKey, out SegmentDpsDebugSnapshot snapshot) // UI 표시용 조회
        {
            EnsureInitialized(); // 시간 보장
            if (damageMeterKey <= 0 || !bucketsBySegmentKey.TryGetValue(damageMeterKey, out DamageBucket bucket))
            {
                snapshot = new SegmentDpsDebugSnapshot(0f, 0f, 0f, 0f);
                return false; // 아직 기록 없음
            }

            float totalElapsed = TotalElapsedSeconds;
            float waveElapsed = WaveElapsedSeconds;
            snapshot = new SegmentDpsDebugSnapshot(
                bucket.TotalDamage,
                bucket.WaveDamage,
                bucket.TotalDamage / totalElapsed,
                bucket.WaveDamage / waveElapsed);
            return true;
        }

        private static DamageBucket GetOrCreateBucket(int key)
        {
            if (!bucketsBySegmentKey.TryGetValue(key, out DamageBucket bucket))
            {
                bucket = new DamageBucket();
                bucketsBySegmentKey.Add(key, bucket);
            }

            return bucket;
        }

        private static bool TryResolveRuntime(DamageData damage, out ConvoySegmentRuntime runtime) // DamageData → 세그먼트 런타임
        {
            runtime = null; // 기본값

            if (damage.SourceObject != null)
            {
                SegmentWeaponBehaviour weapon = damage.SourceObject.GetComponentInParent<SegmentWeaponBehaviour>(); // 무기 출처
                if (weapon != null && weapon.Segment != null)
                {
                    runtime = weapon.Segment;
                    return true;
                }

                runtime = damage.SourceObject.GetComponentInParent<ConvoySegmentRuntime>(); // 루트 fallback
                if (runtime != null)
                {
                    return true;
                }
            }

            if (damage.SourceSegmentIndex >= 0)
            {
                ConvoyController convoy = CoreStatProvider.Active != null && CoreStatProvider.Active.Convoy != null
                    ? CoreStatProvider.Active.Convoy
                    : Object.FindFirstObjectByType<ConvoyController>(); // 인덱스 fallback

                return convoy != null && convoy.TryGetAttachedSegmentRuntimeByChainIndex(damage.SourceSegmentIndex, out runtime);
            }

            return false; // 비세그먼트 피해
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                ResetRun(); // 직접 호출 전 안전 초기화
            }
        }
    }
}
