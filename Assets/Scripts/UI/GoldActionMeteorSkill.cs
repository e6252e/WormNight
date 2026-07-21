using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GoldActionMeteorSkill : MonoBehaviour
    {
        private const int MinimumBaseMeteorCount = 15; // Lv1 최소 운석 수
        private const int MinimumMaxMeteorCount = 23; // Lv5 기준 최소 최대 운석 수
        private const int MinimumWaveRepeatCount = 3; // 최소 웨이브 반복 수
        private const float MinimumWaveRepeatDelay = 2.5f; // 최소 웨이브 시작 간격
        private const float MinimumFallDuration = 3f; // 메테오 최소 낙하 시간
        private const float DefaultImpactRadius = 7f; // 기본 폭발 반경
        private const float LegacyImpactRadius = 10.4f; // 기존 씬 직렬화값
        private const float MinimumImpactRadius = 7f; // 메테오 최소 폭발 반경
        private const float MinimumNearNexusRadius = 52f; // 넥서스 주변 최소 배치 반경
        private const float MinimumWideAreaRadius = 96f; // 전체 맵 최소 배치 반경
        private const float MinimumCastShakeDuration = 0.7f; // 발동 최소 흔들림 시간
        private const float MinimumCastShakeAmplitude = 1.05f; // 발동 최소 흔들림 세기
        private const float DefaultCastShakeFrequency = 16f; // 발동 흔들림 속도
        private const float MinimumImpactShakeDuration = 0.4f; // 착탄 최소 흔들림 시간
        private const float MinimumImpactShakeAmplitude = 0.7f; // 착탄 최소 흔들림 세기
        private const float DefaultImpactShakeFrequency = 18f; // 착탄 흔들림 속도

        [Header("Prefab")]
        public GoldActionMeteorImpact MeteorPrefab; // 정식 운석 프리팹

        [Header("Scene References")]
        public Transform EffectRoot; // 운석 생성 루트
        public Transform NexusCenterOverride; // 중심 위치 강제 지정
        public QuarterViewCamera ShakeCamera; // 흔들릴 카메라

        [Header("Storm")]
        [Min(0.5f)] public float StormDuration = 10f; // 전체 지속 시간
        [Min(1)] public int BaseMeteorCount = 15; // 기본 운석 수
        [Min(0)] public int MeteorCountPerUpgrade = 2; // 강화당 추가 운석
        [Min(1)] public int MaxMeteorCount = 23; // 최대 운석 수
        [Min(1)] public int WaveRepeatCount = 3; // 한 번 발동 시 웨이브 반복 수
        [Min(0f)] public float WaveRepeatDelay = 2.5f; // 웨이브 시작 간격
        [Range(0f, 1f)] public float NearNexusRatio = 0.7f; // 넥서스 주변 우선 비율
        [Min(1f)] public float NearNexusRadius = 52f; // 넥서스 주변 배치 반경
        [Min(1f)] public float WideAreaRadius = 96f; // 넓은 배치 반경
        [Min(0f)] public float PlacementPadding = 0.5f; // 원형 범위 사이 여백
        [Range(8, 240)] public int PlacementAttemptsPerMeteor = 90; // 배치 시도 횟수

        [Header("Meteor")]
        [Min(1f)] public float FallHeight = 24f; // 운석 시작 높이
        [Min(0.1f)] public float FallDuration = 3f; // 예고 후 낙하 시간
        [Min(0.1f)] public float ImpactRadius = 7f; // 폭발 반경
        [Min(0f)] public float BaseDamage = 35f; // 기본 피해
        [Min(0f)] public float DamagePerUpgrade = 12f; // 강화당 피해 증가
        [Min(0f)] public float KnockbackDistance = 4.5f; // 폭발 넉백 거리
        [Min(0.01f)] public float KnockbackDuration = 0.18f; // 폭발 넉백 시간
        [Min(0f)] public float StaggerDuration = 0.35f; // 경직 시간

        [Header("Camera Shake")]
        [Min(0f)] public float CastShakeDuration = 0.7f; // 발동 흔들림 시간
        [Min(0f)] public float CastShakeAmplitude = 1.05f; // 발동 흔들림 세기
        [Min(1f)] public float CastShakeFrequency = 16f; // 발동 흔들림 속도
        [Min(0f)] public float ImpactShakeDuration = 0.4f; // 착탄 흔들림 시간
        [Min(0f)] public float ImpactShakeAmplitude = 0.7f; // 착탄 흔들림 세기
        [Min(1f)] public float ImpactShakeFrequency = 18f; // 착탄 흔들림 속도

#if UNITY_EDITOR
        private void OnValidate() // 에디터 저장값 최소 기준 보정
        {
            BaseMeteorCount = Mathf.Max(BaseMeteorCount, MinimumBaseMeteorCount);
            MaxMeteorCount = Mathf.Max(MaxMeteorCount, BaseMeteorCount, MinimumMaxMeteorCount);
            WaveRepeatCount = Mathf.Max(WaveRepeatCount, MinimumWaveRepeatCount);
            WaveRepeatDelay = Mathf.Max(WaveRepeatDelay, MinimumWaveRepeatDelay);
            FallDuration = Mathf.Max(FallDuration, MinimumFallDuration);
            if (Mathf.Approximately(ImpactRadius, LegacyImpactRadius))
            {
                ImpactRadius = DefaultImpactRadius; // 기존 10.4 저장값 자동 보정
            }

            ImpactRadius = Mathf.Max(ImpactRadius, MinimumImpactRadius);
            NearNexusRadius = Mathf.Max(NearNexusRadius, MinimumNearNexusRadius);
            WideAreaRadius = Mathf.Max(WideAreaRadius, MinimumWideAreaRadius);
            CastShakeDuration = Mathf.Max(CastShakeDuration, MinimumCastShakeDuration);
            CastShakeAmplitude = Mathf.Max(CastShakeAmplitude, MinimumCastShakeAmplitude);
            CastShakeFrequency = Mathf.Clamp(CastShakeFrequency, 1f, DefaultCastShakeFrequency);
            ImpactShakeDuration = Mathf.Max(ImpactShakeDuration, MinimumImpactShakeDuration);
            ImpactShakeAmplitude = Mathf.Max(ImpactShakeAmplitude, MinimumImpactShakeAmplitude);
            ImpactShakeFrequency = Mathf.Clamp(ImpactShakeFrequency, 1f, DefaultImpactShakeFrequency);
        }
#endif

        public bool Play(int upgradeLevel) // 액션 HUD 메테오 발동
        {
            EnsureReferences(); // 런타임 참조 보장

            if (MeteorPrefab == null)
            {
                Debug.LogWarning("[GoldActionMeteor] MeteorPrefab이 연결되지 않아 메테오를 발동할 수 없습니다.", this);
                return false; // 정식 프리팹 필요
            }

            Vector3 center = ResolveStormCenter(); // 넥서스 기준 중심
            int meteorCount = GetMeteorCount(upgradeLevel); // 운석 수
            float damage = GetDamage(upgradeLevel); // 피해량
            float impactRadius = GetImpactRadius(); // 실제 폭발 반경
            float impactShakeDuration = GetImpactShakeDuration(); // 실제 착탄 흔들림 시간
            float impactShakeAmplitude = GetImpactShakeAmplitude(); // 실제 착탄 흔들림 세기
            float impactShakeFrequency = GetImpactShakeFrequency(); // 실제 착탄 흔들림 속도

            GameplaySfxEmitter.TryPlayCatalogAt(GameplaySfxCue.MeteorCast, center);
            StartCoroutine(MeteorWaveSequenceRoutine(center, meteorCount, damage, impactRadius, impactShakeDuration, impactShakeAmplitude, impactShakeFrequency)); // 시간차 웨이브 시작
            return true; // 발동 성공
        }

        private IEnumerator MeteorWaveSequenceRoutine(Vector3 center, int meteorCount, float damage, float impactRadius, float impactShakeDuration, float impactShakeAmplitude, float impactShakeFrequency) // 시간차 메테오 웨이브
        {
            int waveCount = GetWaveRepeatCount(); // 반복 수
            float repeatDelay = GetWaveRepeatDelay(); // 시작 간격

            for (int wave = 0; wave < waveCount; wave++)
            {
                List<Vector3> impactPositions = PickImpactPositions(center, meteorCount, impactRadius); // 웨이브별 착탄 위치
                if (impactPositions.Count > 0)
                {
                    ShakeCamera?.AddShake(GetCastShakeDuration(), GetCastShakeAmplitude(), GetCastShakeFrequency()); // 웨이브 시작 흔들림
                    StartCoroutine(MeteorStormRoutine(impactPositions, damage, impactRadius, impactShakeDuration, impactShakeAmplitude, impactShakeFrequency)); // 운석 비 시작
                }

                if (wave < waveCount - 1)
                {
                    yield return new WaitForSeconds(repeatDelay); // 다음 웨이브까지 대기
                }
            }
        }

        private IEnumerator MeteorStormRoutine(List<Vector3> impactPositions, float damage, float impactRadius, float impactShakeDuration, float impactShakeAmplitude, float impactShakeFrequency) // 운석 전체 스케줄
        {
            int count = impactPositions.Count; // 운석 수
            float fallDuration = GetFallDuration(); // 실제 낙하 시간
            float availableTime = Mathf.Max(0.1f, StormDuration - fallDuration); // 낙하 전개 시간
            float interval = count <= 1 ? 0f : availableTime / Mathf.Max(1, count - 1); // 운석 간격

            for (int i = 0; i < count; i++)
            {
                SpawnMeteor(impactPositions[i], damage, impactRadius, fallDuration, impactShakeDuration, impactShakeAmplitude, impactShakeFrequency); // 운석 프리팹 생성

                if (i < count - 1)
                {
                    yield return new WaitForSeconds(interval); // 다음 운석 대기
                }
            }
        }

        private void SpawnMeteor(Vector3 impactPosition, float damage, float impactRadius, float fallDuration, float impactShakeDuration, float impactShakeAmplitude, float impactShakeFrequency) // 운석 프리팹 생성
        {
            Transform root = EffectRoot != null ? EffectRoot : transform; // 생성 루트
            GoldActionMeteorImpact meteor = Instantiate(MeteorPrefab, root); // 정식 프리팹 인스턴스
            meteor.Play(
                impactPosition,
                FallHeight,
                fallDuration,
                impactRadius,
                damage,
                KnockbackDistance,
                KnockbackDuration,
                StaggerDuration,
                ShakeCamera,
                impactShakeDuration,
                impactShakeAmplitude,
                impactShakeFrequency); // 낙하 시작
        }

        private List<Vector3> PickImpactPositions(Vector3 center, int count, float impactRadius) // 겹치지 않는 착탄 위치 선정
        {
            List<Vector3> positions = new List<Vector3>(count); // 결과
            int nearCount = Mathf.Clamp(Mathf.RoundToInt(count * NearNexusRatio), 0, count); // 넥서스 주변 우선 수
            float minSpacing = Mathf.Max(0.1f, impactRadius * 2f + PlacementPadding); // 원형 범위 비겹침 거리
            float nearRadius = Mathf.Max(NearNexusRadius, MinimumNearNexusRadius, impactRadius * 3.4f); // 넥서스 우선 배치 반경 보정
            float wideRadius = Mathf.Max(WideAreaRadius, MinimumWideAreaRadius, impactRadius * 6f); // 전체 배치 반경 보정

            for (int i = 0; i < count; i++)
            {
                float radius = i < nearCount ? nearRadius : wideRadius; // 우선권 반경
                bool picked = TryPickPosition(center, radius, minSpacing, positions, out Vector3 position); // 1차 시도

                if (!picked)
                {
                    picked = TryPickPosition(center, Mathf.Max(radius, wideRadius), minSpacing * 0.72f, positions, out position); // 완화 재시도
                }

                if (!picked)
                {
                    position = center + new Vector3(Random.Range(-wideRadius, wideRadius), 0f, Random.Range(-wideRadius, wideRadius)); // 최후 fallback
                }

                positions.Add(GroundService.ProjectToGround(position, 0f)); // 바닥 기준 저장
            }

            return positions; // 착탄 위치
        }

        private bool TryPickPosition(Vector3 center, float radius, float minSpacing, List<Vector3> existing, out Vector3 position) // 위치 하나 뽑기
        {
            float safeRadius = Mathf.Max(0.1f, radius); // 반경 보정
            for (int attempt = 0; attempt < PlacementAttemptsPerMeteor; attempt++)
            {
                Vector2 random = Random.insideUnitCircle * safeRadius; // 원 내부 랜덤
                Vector3 candidate = center + new Vector3(random.x, 0f, random.y); // 후보 위치
                candidate = GroundService.ProjectToGround(candidate, 0f); // 바닥 보정

                if (IsFarEnough(candidate, existing, minSpacing))
                {
                    position = candidate; // 성공 위치
                    return true; // 성공
                }
            }

            position = Vector3.zero; // 실패
            return false; // 실패
        }

        private static bool IsFarEnough(Vector3 candidate, List<Vector3> existing, float minSpacing) // 기존 착탄과 거리 확인
        {
            float minSqr = minSpacing * minSpacing; // 거리 제곱
            for (int i = 0; i < existing.Count; i++)
            {
                Vector3 offset = candidate - existing[i]; // 후보 간 거리
                offset.y = 0f; // 평면 거리
                if (offset.sqrMagnitude < minSqr)
                {
                    return false; // 너무 가까움
                }
            }

            return true; // 사용 가능
        }

        private void EnsureReferences() // 참조 자동 보강
        {
            if (EffectRoot == null)
            {
                GameObject root = GameObject.Find("GoldActionMeteorRoot"); // 기존 루트 검색
                if (root == null)
                {
                    root = new GameObject("GoldActionMeteorRoot"); // 새 루트
                }

                EffectRoot = root.transform; // 루트 저장
            }

            if (ShakeCamera == null)
            {
                Camera mainCamera = Camera.main; // 메인 카메라
                ShakeCamera = mainCamera != null ? mainCamera.GetComponent<QuarterViewCamera>() : null; // 컴포넌트
            }

            if (ShakeCamera == null)
            {
                ShakeCamera = FindFirstObjectByType<QuarterViewCamera>(); // fallback
            }
        }

        private Vector3 ResolveStormCenter() // 메테오 중심 위치
        {
            if (NexusCenterOverride != null)
            {
                return GroundService.ProjectToGround(NexusCenterOverride.position, 0f); // 지정 중심
            }

            if (NexusController.Active != null)
            {
                return GroundService.ProjectToGround(NexusController.Active.transform.position, 0f); // 현재 넥서스
            }

            GameObject nexus = GameObject.Find("Nexus_Core"); // 이름 fallback
            if (nexus != null)
            {
                return GroundService.ProjectToGround(nexus.transform.position, 0f); // 이름 중심
            }

            return GroundService.ProjectToGround(Vector3.zero, 0f); // 최후 fallback
        }

        private int GetMeteorCount(int upgradeLevel) // 강화 반영 운석 수
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            int targetCount = BaseMeteorCount + (level - 1) * MeteorCountPerUpgrade; // 레벨별 목표 수
            int requiredMax = BaseMeteorCount + MeteorCountPerUpgrade * 4; // 기본 Lv5 보장
            int effectiveMax = Mathf.Max(MaxMeteorCount, requiredMax, MinimumMaxMeteorCount); // 기존 씬 15값 보정
            return Mathf.Clamp(targetCount, 1, Mathf.Max(1, effectiveMax)); // 수량
        }

        private int GetWaveRepeatCount() // 실제 웨이브 반복 수
        {
            return Mathf.Max(WaveRepeatCount, MinimumWaveRepeatCount);
        }

        private float GetWaveRepeatDelay() // 실제 웨이브 시작 간격
        {
            return Mathf.Max(WaveRepeatDelay, MinimumWaveRepeatDelay);
        }

        private float GetDamage(int upgradeLevel) // 강화 반영 피해
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            return Mathf.Max(0f, BaseDamage + (level - 1) * DamagePerUpgrade); // 피해
        }

        private float GetImpactRadius() // 실제 폭발 반경
        {
            float radius = Mathf.Approximately(ImpactRadius, LegacyImpactRadius) ? DefaultImpactRadius : ImpactRadius; // 기존 씬 10.4값 보정
            return Mathf.Max(radius, MinimumImpactRadius);
        }

        private float GetFallDuration() // 실제 낙하 시간
        {
            return Mathf.Max(FallDuration, MinimumFallDuration);
        }

        private float GetCastShakeDuration() // 실제 발동 흔들림 시간
        {
            return Mathf.Max(CastShakeDuration, MinimumCastShakeDuration);
        }

        private float GetCastShakeAmplitude() // 실제 발동 흔들림 세기
        {
            return Mathf.Max(CastShakeAmplitude, MinimumCastShakeAmplitude);
        }

        private float GetCastShakeFrequency() // 실제 발동 흔들림 속도
        {
            return Mathf.Clamp(CastShakeFrequency, 1f, DefaultCastShakeFrequency);
        }

        private float GetImpactShakeDuration() // 실제 착탄 흔들림 시간
        {
            return Mathf.Max(ImpactShakeDuration, MinimumImpactShakeDuration);
        }

        private float GetImpactShakeAmplitude() // 실제 착탄 흔들림 세기
        {
            return Mathf.Max(ImpactShakeAmplitude, MinimumImpactShakeAmplitude);
        }

        private float GetImpactShakeFrequency() // 실제 착탄 흔들림 속도
        {
            return Mathf.Clamp(ImpactShakeFrequency, 1f, DefaultImpactShakeFrequency);
        }
    }
}
