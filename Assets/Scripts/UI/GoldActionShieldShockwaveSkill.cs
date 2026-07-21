using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GoldActionShieldShockwaveSkill : MonoBehaviour // 2번 액션 HUD 보호막 충격파
    {
        private const float MinimumRadius = 18f; // 최소 판정 반경
        private const float MinimumKnockbackDistance = 1f; // 최소 넉백 거리
        private const float MinimumKnockbackDuration = 0.05f; // 최소 넉백 시간

        [Header("Scene References")]
        public NexusController Nexus; // 중심 넥서스
        public NexusVfxController NexusVfx; // 보호막 VFX 제어
        public QuarterViewCamera ShakeCamera; // 발동 카메라 흔들림

        [Header("Area")]
        [Min(1f)] public float BaseRadius = 48f; // Lv1 충격파 반경
        [Min(0f)] public float RadiusPerUpgrade = 6f; // 강화당 반경 증가
        [Range(0.05f, 1f)] public float EdgeKnockbackMultiplier = 0.45f; // 외곽 넉백 비율

        [Header("Knockback")]
        [Min(0f)] public float BaseKnockbackDistance = 14f; // Lv1 넉백 거리
        [Min(0f)] public float KnockbackDistancePerUpgrade = 2.5f; // 강화당 넉백 증가
        [Min(0.01f)] public float KnockbackDuration = 0.42f; // 밀려나는 시간
        [Min(0f)] public float StaggerDuration = 0.35f; // 짧은 경직
        [Range(0f, 1f)] public float BossKnockbackMultiplier = 0.35f; // 보스 넉백 감쇠

        [Header("Shield VFX Pulse")]
        [Min(1f)] public float BasePulseXzMultiplier = 10f; // 보호막 XZ 확장
        [Min(0f)] public float PulseXzMultiplierPerUpgrade = 0.25f; // 강화당 확장 증가
        [Min(0.01f)] public float PulseYMultiplier = 0.9f; // 보호막 Y 유지 비율
        [Min(0.01f)] public float PulseExpandDuration = 0.18f; // 확장 시간
        [Min(0f)] public float PulseHoldDuration = 0.04f; // 최대 확장 유지
        [Min(0.01f)] public float PulseReturnDuration = 0.32f; // 복귀 시간

        [Header("Camera Shake")]
        [Min(0f)] public float ShakeDuration = 0.35f; // 발동 흔들림 시간
        [Min(0f)] public float ShakeAmplitude = 0.65f; // 발동 흔들림 세기
        [Min(1f)] public float ShakeFrequency = 18f; // 발동 흔들림 속도

        private readonly List<EnemyController> targets = new List<EnemyController>(128); // 재사용 대상 목록

        public bool Play(int upgradeLevel) // 액션 HUD 2번 발동
        {
            EnsureReferences(); // 런타임 참조 보장

            if (Nexus == null)
            {
                Debug.LogWarning("[GoldActionShockwave] Nexus가 없어 보호막 충격파를 발동할 수 없습니다.", this);
                return false; // 중심 필요
            }

            int level = Mathf.Max(1, upgradeLevel); // 강화 레벨
            Vector3 center = GroundService.ProjectToGround(Nexus.transform.position, 0f); // 넥서스 중심
            float radius = GetRadius(level); // 실제 반경
            float knockbackDistance = GetKnockbackDistance(level); // 실제 넉백
            float pulseXzMultiplier = GetPulseXzMultiplier(level); // 보호막 확장

            GameplaySfxEmitter.TryPlayCatalogAt(GameplaySfxCue.HudSkill2, center);
            NexusVfx?.PlayShieldShockwavePulse(
                pulseXzMultiplier,
                PulseYMultiplier,
                PulseExpandDuration,
                PulseHoldDuration,
                PulseReturnDuration); // 보호막 확장 연출

            ShakeCamera?.AddShake(ShakeDuration, ShakeAmplitude, ShakeFrequency); // 발동 흔들림

            int affected = PushEnemies(center, radius, knockbackDistance); // 몬스터 밀기
            Debug.Log($"[GoldActionShockwave] Shield shockwave cast: Lv{level}, affected {affected}, radius {radius:0.0}", this);
            return true; // 몬스터가 없어도 발동 성공
        }

        private int PushEnemies(Vector3 center, float radius, float knockbackDistance) // 범위 몬스터 넉백
        {
            EnemyController.CollectActiveInRange(center, radius, targets); // 활성 몬스터 수집

            int affected = 0; // 적용 수
            float safeRadius = Mathf.Max(radius, MinimumRadius); // 반경 보정
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyController enemy = targets[i]; // 대상
                if (enemy == null || enemy.IsDead)
                {
                    continue; // 무효 대상
                }

                Vector3 enemyPosition = enemy.transform.position; // 대상 위치
                Vector3 direction = enemyPosition - center; // 넥서스에서 바깥 방향
                direction.y = 0f; // 평면 방향
                float distance = direction.magnitude; // 중심 거리
                float distanceRatio = Mathf.Clamp01(distance / safeRadius); // 중심-외곽 비율
                float falloff = Mathf.Lerp(1f, EdgeKnockbackMultiplier, distanceRatio); // 외곽 감쇠
                float resolvedKnockback = knockbackDistance * falloff; // 최종 넉백

                if (enemy.Grade == EnemyGrade.Boss)
                {
                    resolvedKnockback *= BossKnockbackMultiplier; // 보스 감쇠
                }

                MonsterFeedbackData feedback = MonsterFeedbackData.Create(
                    center,
                    direction,
                    enemyPosition,
                    resolvedKnockback,
                    KnockbackDuration,
                    StaggerDuration,
                    -1,
                    DamageType.Electric,
                    gameObject); // 넉백 피드백

                if (MonsterFeedbackApi.TryApplyFeedback(enemy, feedback))
                {
                    affected++; // 적용 성공
                }
            }

            targets.Clear(); // 다음 발동 준비
            return affected; // 적용 수
        }

        private void EnsureReferences() // 참조 자동 보강
        {
            if (Nexus == null)
            {
                Nexus = NexusController.Active; // 활성 넥서스
            }

            if (Nexus == null)
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 이름 fallback
                Nexus = nexusObject != null ? nexusObject.GetComponent<NexusController>() : null; // 컴포넌트
            }

            if (NexusVfx == null && Nexus != null)
            {
                NexusVfx = Nexus.GetComponent<NexusVfxController>(); // 넥서스 부착 VFX
            }

            if (ShakeCamera == null)
            {
                Camera mainCamera = Camera.main; // 메인 카메라
                ShakeCamera = mainCamera != null ? mainCamera.GetComponent<QuarterViewCamera>() : null; // 카메라 컴포넌트
            }

            if (ShakeCamera == null)
            {
                ShakeCamera = FindFirstObjectByType<QuarterViewCamera>(); // fallback
            }
        }

        private float GetRadius(int upgradeLevel) // 강화 반영 반경
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            return Mathf.Max(MinimumRadius, BaseRadius + (level - 1) * RadiusPerUpgrade); // 반경
        }

        private float GetKnockbackDistance(int upgradeLevel) // 강화 반영 넉백
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            return Mathf.Max(MinimumKnockbackDistance, BaseKnockbackDistance + (level - 1) * KnockbackDistancePerUpgrade); // 거리
        }

        private float GetPulseXzMultiplier(int upgradeLevel) // 강화 반영 보호막 확장
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            return Mathf.Max(1f, BasePulseXzMultiplier + (level - 1) * PulseXzMultiplierPerUpgrade); // XZ 배율
        }

#if UNITY_EDITOR
        private void OnValidate() // 에디터 저장값 보정
        {
            BaseRadius = Mathf.Max(BaseRadius, MinimumRadius);
            BaseKnockbackDistance = Mathf.Max(BaseKnockbackDistance, MinimumKnockbackDistance);
            KnockbackDuration = Mathf.Max(KnockbackDuration, MinimumKnockbackDuration);
            EdgeKnockbackMultiplier = Mathf.Clamp(EdgeKnockbackMultiplier, 0.05f, 1f);
            BossKnockbackMultiplier = Mathf.Clamp01(BossKnockbackMultiplier);
            BasePulseXzMultiplier = Mathf.Max(1f, BasePulseXzMultiplier);
            PulseYMultiplier = Mathf.Max(0.01f, PulseYMultiplier);
            PulseExpandDuration = Mathf.Max(0.01f, PulseExpandDuration);
            PulseReturnDuration = Mathf.Max(0.01f, PulseReturnDuration);
            ShakeFrequency = Mathf.Max(1f, ShakeFrequency);
        }
#endif
    }
}
