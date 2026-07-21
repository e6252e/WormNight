using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon : SegmentWeaponBehaviour // 데이터 기반 세그먼트 무기
    {
        public SegmentAttackProfile AttackProfile; // 공격 데이터
        public Transform HeadYawPivot; // 머리 회전축
        public Transform Muzzle; // 발사 위치
        public Transform[] ProjectileMuzzles; // 다중 포구 발사용 위치 목록
        public Transform MuzzleVfxSocket; // 발사 VFX 위치
        public Transform LoadedProjectileRoot; // 장전 미사일 표시 루트

        private readonly List<Transform> loadedProjectileVisuals = new List<Transform>(4); // 장전 표시 목록
        private readonly List<Transform> projectileMuzzleBuffer = new List<Transform>(8); // 자동 포구 수집 버퍼
        private float fireTimer; // 남은 쿨타임
        private float fireIntervalDuration; // 현재 쿨타임 길이
        private bool loadedProjectilesRestored = true; // 장전 표시 복구 여부
        private bool isFiringProjectileSequence; // 순차 발사 중
        private bool wasWeaponActive; // 이전 작동 상태
        private bool hasInitializedLoadedVisuals; // 초기 장전 표시 완료
        private EnemyController lockedSawTarget; // 톱날 조준 고정 대상
        private EnemyController projectileSequenceTarget; // 순차 발사 중 현재 조준 대상
        private int projectileSequencePreferredSide; // 순차 발사 중 우선 탐색할 좌/우 콘
        private bool hasProjectileSequenceLastAimPoint; // 순차 발사 fallback 위치 보유 여부
        private Vector3 projectileSequenceLastAimPoint; // 순차 발사 마지막 조준 위치
        private bool hasResolvedImpactPoint; // 지점 타격 프로필의 현재 착탄 지점
        private Vector3 resolvedImpactPoint; // 실제 피해/장판 중심
        private GameObject sustainedMuzzleVfxInstance;
        private ParticleSystem[] sustainedMuzzleVfxParticles;
        private Coroutine laserRoutine; // 레이저 지속 피해
        private Coroutine projectileSequenceRoutine; // 순차 투사체 발사
        // 투석기처럼 발사 전에 별도 무기 모션을 재생하는 컴포넌트
        private SegmentTrebuchetFireMotion trebuchetFireMotion; // SG03 투석기 숟가락 모션
        // 발사 반동 대상 임시 목록
        private readonly List<Transform> fireRecoilTargets = new List<Transform>(3); // Visual/Head 동시 반동
        // 반동 복귀 기준 pose 목록
        private readonly List<RecoilTargetPose> recoilTargetPoses = new List<RecoilTargetPose>(3); // 원래 위치/회전
        // 중복 반동 연출 제어용 DOTween 시퀀스
        private Sequence recoilSequence; // 현재 반동 트윈
        // SG02 미사일 프로필처럼 반동값이 아직 저장되지 않은 로켓 계열에 줄 아주 약한 기본 반동
        private const float DefaultLightMissileRecoilDistance = 0.045f; // 미사일 발사 순간 살짝 밀림
        // SG02 미사일 프로필처럼 반동값이 아직 저장되지 않은 로켓 계열에 줄 아주 약한 기본 기울기
        private const float DefaultLightMissileRecoilTiltAngle = 1.1f; // 캐논보다 훨씬 약한 기울기

        public override void Configure(ConvoySegmentRuntime segment) // 세그먼트 연결
        {
            base.Configure(segment); // 공통 저장
            CacheTrebuchetFireMotion(); // SG03 투석기 모션 연결
            CacheLoadedProjectileVisuals(); // 장전 표시 수집
            if (!hasInitializedLoadedVisuals)
            {
                RestoreLoadedProjectileVisuals(); // 최초 장전 상태
                hasInitializedLoadedVisuals = true;
            }
        }

        public override void SetWeaponActive(bool active) // 작동 상태
        {
            bool becameActive = active && !wasWeaponActive; // 비활성 -> 활성
            base.SetWeaponActive(active); // 공통 상태
            if (becameActive)
            {
                RestoreLoadedProjectileVisuals(); // 재활성화 시 장전 복구
            }
            wasWeaponActive = active; // 상태 저장

            if (!active && projectileSequenceRoutine != null)
            {
                StopCoroutine(projectileSequenceRoutine); // 분리 시 발사 중지
                projectileSequenceRoutine = null;
                isFiringProjectileSequence = false;
                projectileSequenceTarget = null;
                projectileSequencePreferredSide = 0;
                hasProjectileSequenceLastAimPoint = false;
                StopSustainedMuzzleVfx(true);
                StopFireLoopSfx();
            }

            if (!active)
            {
                ClearSawTargetLock(); // 비활성 시 톱날 대상 해제
                StopTrebuchetFireMotion(); // 분리/비활성 시 숟가락 모션 복구
                ResetFireRecoilPose(); // 분리/비활성 시 반동 중간 pose 복구
                StopSustainedMuzzleVfx(true);
                StopFireLoopSfx();
            }
        }

        public override void TickWeapon(float deltaTime) // 무기 갱신
        {
            if (!CanUseWeapon())
            {
                ClearSawTargetLock(); // 사용 불가 시 대상 해제
                StopSustainedMuzzleVfx(true);
                StopFireLoopSfx();
                return; // 발사 불가
            }

            fireTimer -= deltaTime * GetSupportAttackSpeedMultiplier(); // 쿨타임 감소
            UpdateLoadedProjectileReloadVisuals(); // 재장전 표시 복구
            if (isFiringProjectileSequence)
            {
                UpdateProjectileSequenceAim(deltaTime); // 채널링 중 느린 재조준
                return; // 순차 발사 진행 중
            }

            if (!TryFindTarget(out EnemyController target))
            {
                return; // 대상 없음
            }

            bool aimed = AimHeadAtTarget(target, deltaTime); // 머리 조준
            if (fireTimer > 0f)
            {
                return; // 조준만 유지
            }

            if (AttackProfile.RequireAimBeforeFire && !aimed)
            {
                return; // 아직 조준 중
            }

            if (Fire(target))
            {
                ClearSawTargetLock(); // 발사 후 다음 후보 준비
                ResetCooldown(); // 다음 공격 준비
            }
        }

        private bool CanUseWeapon() // 작동 가능 확인
        {
            return IsWeaponActive && Segment != null && Segment.Owner != null && AttackProfile != null; // 연결 상태
        }

        private DamageData CreateDamageData(Vector3 position) // 피해값 생성
        {
            CoreStatData coreStats = CoreStatProvider.GetCurrentOrDefault(); // 코어 스탯
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId()); // 무기 강화
            float baseDamage = weaponBonus.ResolveBaseDamage(AttackProfile.BaseDamage); // 프로필 + 무기 강화
            float commonDamage = CoreStatProvider.GetCommonBaseDamageBonusOrDefault(GetEffectiveSegmentId(), AttackProfile.BaseDamage); // 공통카드 기초 피해 보너스
            float damage = GetUpgrade().ApplyDamage(baseDamage + commonDamage + coreStats.FlatDamageBonus); // 최종 피해
            damage *= SupportSegmentRuntimeBuffs.GetFinalDamageMultiplier(Segment.ChainIndex); // 지원형 최종 피해 버프
            return DamageData.Create(damage, GetDamageType(), Segment.ChainIndex, position, gameObject); // 전달값
        }

        private DamageType GetDamageType() // 피해 종류
        {
            if (AttackProfile.UseDamageTypeOverride)
            {
                return AttackProfile.DamageTypeOverride; // 프로필 지정 타입
            }

            if (AttackProfile.MoveType == SegmentAttackMoveType.Laser)
            {
                return DamageType.Laser; // 레이저
            }

            if (AttackProfile.MoveType == SegmentAttackMoveType.ChainLightning)
            {
                return DamageType.Electric; // 전기
            }

            return AttackProfile.ImpactType == SegmentAttackImpactType.ExplosionArea ? DamageType.Explosion : DamageType.Projectile; // 투사체/폭발
        }

        private void ResetCooldown() // 쿨타임 재설정
        {
            //전찬우 수정-0622
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId()); // 무기 강화 쿨감
            float cooldown = weaponBonus.ResolveCooldown(AttackProfile.Cooldown); // 기준 쿨타임
            float baseInterval = Mathf.Max(0.05f, GetRandomizedCooldown(cooldown)); // 기준 쿨타임 ±10%
            float coreInterval = CoreStatProvider.GetCurrentOrDefault().ApplyFireInterval(baseInterval); // 코어 공속
            fireTimer = GetUpgrade().ApplyFireInterval(coreInterval); // 세그먼트 공속
            fireIntervalDuration = fireTimer; // 진행률 계산 기준
            loadedProjectilesRestored = !ShouldUseLoadedProjectileVisuals(); // 장전 표시 복구 대기
        }

        private float GetEffectiveSearchRange() // 무기 강화 + 세그먼트 강화가 반영된 탐색 거리
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return GetUpgrade().ApplyRange(weaponBonus.ResolveSearchRange(AttackProfile.SearchRange));
        }

        private float GetEffectiveSideConeAngle() // 무기 강화가 반영된 좌우 부채꼴 각도
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return weaponBonus.ResolveSideConeAngle(AttackProfile.SideConeAngle);
        }

        private int GetEffectiveMaxChainDepth() // 무기 강화가 반영된 연쇄 단계
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return weaponBonus.ResolveMaxChainDepth(AttackProfile.MaxChainDepth);
        }

        private float GetEffectiveChainRange() // 무기 강화 + 세그먼트 강화가 반영된 연쇄 거리
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return GetUpgrade().ApplyRange(weaponBonus.ResolveChainRange(AttackProfile.ChainRange));
        }

        private float GetEffectiveChainDamageFalloff() // 무기 강화가 반영된 체인 피해 유지율
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return weaponBonus.ResolveChainDamageFalloff(AttackProfile.ChainDamageFalloff);
        }

        private float GetEffectiveLaserDuration() // 무기 강화가 반영된 지속 시간
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return weaponBonus.ResolveLaserDuration(AttackProfile.LaserDuration);
        }

        private float GetEffectiveLaserTickInterval() // 무기 강화가 반영된 틱 간격
        {
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId());
            return weaponBonus.ResolveLaserTickInterval(AttackProfile.LaserTickInterval);
        }

        private float GetSupportAttackSpeedMultiplier() // 지원형 공격속도 버프
        {
            if (Segment == null)
            {
                return 1f;
            }

            return SupportSegmentRuntimeBuffs.GetFinalAttackSpeedMultiplier(Segment.ChainIndex); // 지원형 쿨타임 감소 배율
        }
    }
}
