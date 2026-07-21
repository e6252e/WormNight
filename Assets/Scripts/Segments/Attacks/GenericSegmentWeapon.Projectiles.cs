using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private bool Fire(EnemyController target) // 공격 실행
        {
            if (AttackProfile.MoveType == SegmentAttackMoveType.Laser)
            {
                Transform muzzle = ResolveMuzzle(); // 포구
                Vector3 spawnPosition = muzzle != null ? muzzle.position : transform.position + Vector3.up * AttackProfile.AttackSpawnHeight; // 생성 위치
                DamageData damage = CreateDamageData(spawnPosition); // 피해값
                Vector3 fireDirection = GetFireDirection(target, spawnPosition); // 실제 발사 방향
                PlayMuzzleVfx(muzzle); // 발사 VFX
                PlayFireSfx(muzzle);
                PlayFireRecoil(fireDirection, muzzle); // 포구 로컬축 대신 실제 발사 방향 기준 반동
                FireLaser(target, damage); // 레이저
                return true;
            }

            if (AttackProfile.MoveType == SegmentAttackMoveType.ChainLightning)
            {
                Transform muzzle = ResolveMuzzle(); // 시작 위치
                Vector3 spawnPosition = muzzle != null ? muzzle.position : transform.position + Vector3.up * AttackProfile.AttackSpawnHeight; // 번개 시작점
                DamageData damage = CreateDamageData(spawnPosition); // 피해값
                Vector3 fireDirection = GetFireDirection(target, spawnPosition); // 첫 타겟 방향
                HideLoadedProjectileVisual(0); // 장전 전기 VFX 숨김
                PlayMuzzleVfx(muzzle); // 시전 VFX
                PlayFireSfx(muzzle);
                PlayFireRecoil(fireDirection, muzzle); // 약한 시전 반동
                FireChainLightning(target, muzzle, spawnPosition, damage); // 즉시 체인 번개
                return true;
            }

            if (TryStartTrebuchetFireMotion(target))
            {
                return false; // 투석기 모션 코루틴이 발사/쿨타임을 처리
            }

            if (ShouldFireProjectilesSequentially())
            {
                projectileSequenceRoutine = StartCoroutine(FireProjectileSequence(target)); // 순차 발사
                return false; // 코루틴 종료 후 쿨타임
            }

            Transform projectileMuzzle = ResolveMuzzle(); // 포구
            Vector3 projectileSpawnPosition = projectileMuzzle != null ? projectileMuzzle.position : transform.position + Vector3.up * AttackProfile.AttackSpawnHeight; // 생성 위치
            DamageData projectileDamage = CreateDamageData(projectileSpawnPosition); // 피해값
            Vector3 projectileFireDirection = GetProjectileFireDirection(target, projectileSpawnPosition); // 실제 투사체 발사 방향
            PlayMuzzleVfx(projectileMuzzle); // 발사 VFX
            PlayFireSfx(projectileMuzzle);
            PlayFireRecoil(projectileFireDirection, projectileMuzzle); // 포구 로컬축 대신 실제 투사체 방향 기준 반동
            FireProjectiles(target, projectileSpawnPosition, projectileDamage, projectileMuzzle); // 투사체
            return true;
        }

        private void FireProjectiles(EnemyController target, Vector3 spawnPosition, DamageData damage, Transform muzzle) // 투사체 발사
        {
            int count = Mathf.Max(1, AttackProfile.ProjectileCount); // 발사 수
            float spread = Mathf.Max(0f, AttackProfile.SpreadAngle); // 산탄 각도

            for (int i = 0; i < count; i++)
            {
                Transform projectileMuzzle = ResolveProjectileMuzzle(i, muzzle); // 이번 탄 포구
                Vector3 projectileSpawnPosition = GetProjectileSpawnPosition(i, projectileMuzzle); // 포구별 위치
                DamageData projectileDamage = CreateDamageData(projectileSpawnPosition); // 위치 반영
                FireSingleProjectile(target, projectileSpawnPosition, projectileDamage, i, count, spread, projectileMuzzle); // 개별 발사
                HideLoadedProjectileVisual(i); // 장전 표시 숨김
            }
        }

        private IEnumerator FireProjectileSequence(EnemyController initialTarget) // 순차 투사체 발사
        {
            isFiringProjectileSequence = true; // 중복 발사 방지
            hasProjectileSequenceLastAimPoint = false; // 이전 연사 위치 제거
            projectileSequenceTarget = ResolveSequenceTarget(initialTarget); // 첫 조준 대상
            UpdateProjectileSequenceLastAimPoint(projectileSequenceTarget); // 첫 fallback 위치 저장
            UpdateProjectileSequencePreferredSide(projectileSequenceTarget); // 첫 발사 방향 기억
            CacheLoadedProjectileVisuals(); // 표시 목록 갱신

            int count = Mathf.Max(1, AttackProfile.ProjectileCount); // 발사 수
            float spread = Mathf.Max(0f, AttackProfile.SpreadAngle); // 산탄 각도
            float delay = Mathf.Max(0f, AttackProfile.ProjectileFireDelay); // 발사 간격
            int volleySize = Mathf.Clamp(AttackProfile.ProjectileVolleySize, 1, count); // 한 번에 나가는 묶음 수
            int volleyCount = Mathf.CeilToInt(count / (float)volleySize); // 묶음 개수
            bool useSustainedMuzzleVfx = ShouldUseSustainedMuzzleVfx();
            if (useSustainedMuzzleVfx)
            {
                Transform sustainedMuzzle = ResolveMuzzle();
                StartSustainedMuzzleVfx(sustainedMuzzle);
                PlayFireSfx(sustainedMuzzle);
                PlayFireLoopSfx(sustainedMuzzle);
            }

            for (int volleyIndex = 0; volleyIndex < volleyCount; volleyIndex++)
            {
                if (!CanUseWeapon())
                {
                    break; // 분리/비활성
                }

                bool holdDirectionAfterTargetLoss = ShouldHoldProjectileSequenceDirectionAfterTargetLoss();
                EnemyController target = holdDirectionAfterTargetLoss && !IsTargetUsable(projectileSequenceTarget)
                    ? null
                    : ResolveSequenceTarget(projectileSequenceTarget); // 현재 대상
                projectileSequenceTarget = target; // 다음 틱 조준용 저장
                Transform muzzle = ResolveMuzzle(); // 조준 기준 포구
                if (!holdDirectionAfterTargetLoss || IsTargetUsable(target))
                {
                    UpdateProjectileSequenceLastAimPoint(target); // 살아있는 타겟이면 마지막 사격 위치 갱신
                    UpdateProjectileSequencePreferredSide(target); // 이번 발사 콘 방향 기억
                    AimHeadAtTarget(target, Time.deltaTime, GetFiringHeadTurnSpeedMultiplier()); // 발사 순간에도 느리게 재조준
                }
                int startIndex = volleyIndex * volleySize; // 이번 묶음 시작
                int currentVolleySize = Mathf.Min(volleySize, count - startIndex); // 마지막 묶음 보정
                Transform leadMuzzle = ResolveProjectileMuzzle(startIndex, muzzle); // 이번 묶음 대표 포구
                Vector3 leadSpawnPosition = GetProjectileSpawnPosition(startIndex, leadMuzzle); // 대표 위치
                Vector3 fireDirection = GetProjectileSequenceFireDirection(target, leadSpawnPosition); // 묶음 발사 방향
                if (useSustainedMuzzleVfx)
                {
                    UpdateSustainedMuzzleVfx(leadMuzzle);
                }
                PlayFireRecoil(fireDirection, leadMuzzle); // 순차 발사도 실제 발사 방향 반대로 반동 적용
                for (int localIndex = 0; localIndex < currentVolleySize; localIndex++)
                {
                    int projectileIndex = startIndex + localIndex; // 전체 장전 슬롯 번호
                    Transform projectileMuzzle = ResolveProjectileMuzzle(projectileIndex, leadMuzzle); // 탄별 포구
                    Vector3 spawnPosition = GetProjectileSpawnPosition(projectileIndex, projectileMuzzle); // 장전 위치 우선
                    DamageData damage = CreateDamageData(spawnPosition); // 피해값
                    Vector3 projectileDirection = GetProjectileSequenceFireDirection(target, spawnPosition); // 타겟 사망 시 무기별 fallback 방향
                    bool useFallbackImpactPoint = TryGetProjectileSequenceFallbackImpactPoint(target, out Vector3 fallbackImpactPoint); // 곡사 fallback 착탄점
                    if (!useSustainedMuzzleVfx)
                    {
                        PlayMuzzleVfx(projectileMuzzle); // 탄별 포구 VFX
                        PlayFireSfx(projectileMuzzle);
                    }
                    FireSingleProjectile(target, spawnPosition, damage, localIndex, currentVolleySize, spread, projectileMuzzle, true, projectileDirection, useFallbackImpactPoint, fallbackImpactPoint); // 묶음 내 산탄
                    HideLoadedProjectileVisual(projectileIndex); // 사용한 장전탄 숨김
                }

                if (volleyIndex < volleyCount - 1 && delay > 0f)
                {
                    yield return new WaitForSeconds(delay); // 다음 묶음 지연
                }
            }

            StopSustainedMuzzleVfx(false);
            StopFireLoopSfx();
            ClearSawTargetLock(); // 연사 종료 후 다음 후보 준비
            ResetCooldown(); // 전탄 발사 후 쿨타임
            projectileSequenceRoutine = null; // 코루틴 해제
            isFiringProjectileSequence = false; // 발사 완료
            projectileSequenceTarget = null; // 대상 초기화
            projectileSequencePreferredSide = 0; // 선호 방향 초기화
            hasProjectileSequenceLastAimPoint = false; // fallback 위치 초기화
        }

        private void FireSingleProjectile(EnemyController target, Vector3 spawnPosition, DamageData damage, int projectileIndex, int projectileCount, float spread, Transform muzzle, bool useDirectionOverride = false, Vector3 directionOverride = default, bool useImpactPointOverride = false, Vector3 impactPointOverride = default) // 단일 투사체
        {
            Vector3 baseDirection = useDirectionOverride && directionOverride.sqrMagnitude > 0.0001f
                ? directionOverride.normalized
                : GetProjectileFireDirection(target, spawnPosition); // 기준 방향
            float startAngle = projectileCount <= 1 ? 0f : -spread * 0.5f; // 시작 각도
            float step = projectileCount <= 1 ? 0f : spread / (projectileCount - 1); // 각도 간격
            float angle = startAngle + step * projectileIndex; // 이번 탄 각도
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection; // 산탄 방향
            WeaponStatBonusData weaponBonus = CoreStatProvider.GetWeaponStatBonusOrDefault(GetEffectiveSegmentId()); // 무기 강화
            Transform flameInfluenceAnchor = AttackProfile != null && AttackProfile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere ? muzzle : null;
            if (ShouldUseResolvedImpactPoint())
            {
                SegmentProjectileRuntime.SpawnAtPoint(Segment.Owner.GetProjectileRoot(), AttackProfile.ProjectilePrefab, spawnPosition, direction, resolvedImpactPoint, AttackProfile, damage, weaponBonus, flameInfluenceAnchor); // 지점 타격 투사체
                return;
            }

            if (useImpactPointOverride)
            {
                SegmentProjectileRuntime.SpawnAtPoint(Segment.Owner.GetProjectileRoot(), AttackProfile.ProjectilePrefab, spawnPosition, direction, impactPointOverride, AttackProfile, damage, weaponBonus, flameInfluenceAnchor); // 마지막 조준 위치 타격
                return;
            }

            SegmentProjectileRuntime.Spawn(Segment.Owner.GetProjectileRoot(), AttackProfile.ProjectilePrefab, spawnPosition, direction, target, AttackProfile, damage, weaponBonus, flameInfluenceAnchor); // 공통 투사체
        }

        private EnemyController ResolveSequenceTarget(EnemyController initialTarget) // 순차 발사 대상
        {
            if (AttackProfile != null && AttackProfile.TargetPriorityMode == SegmentTargetPriorityMode.DensestClusterOrRandom)
            {
                return TryFindTarget(out EnemyController clusterTarget) ? clusterTarget : null; // 매 묶음마다 밀집 지점 재탐색
            }

            if (IsTargetUsable(initialTarget))
            {
                float range = GetEffectiveSearchRange(); // 사거리
                if (Vector3.Distance(transform.position, initialTarget.transform.position) <= range && IsTargetInAttackArea(initialTarget)) // 순차 발사 중에도 공격 범위 형태 유지
                {
                    return initialTarget; // 기존 대상 유지
                }
            }

            return TryFindProjectileSequenceTargetBySide(out EnemyController target) ? target : null; // 새 대상 fallback
        }

        private void UpdateProjectileSequenceLastAimPoint(EnemyController target) // 순차 발사 마지막 조준 위치 갱신
        {
            if (ShouldUseResolvedImpactPoint())
            {
                projectileSequenceLastAimPoint = resolvedImpactPoint; // 밀집 지점 타격 fallback
                hasProjectileSequenceLastAimPoint = true;
                return;
            }

            if (!IsTargetUsable(target))
            {
                return; // 갱신 대상 없음
            }

            projectileSequenceLastAimPoint = target.transform.position + Vector3.up * AttackProfile.TargetAimHeight; // 마지막 타겟 위치
            hasProjectileSequenceLastAimPoint = true;
        }

        private Vector3 GetProjectileSequenceFireDirection(EnemyController target, Vector3 spawnPosition) // 순차 발사 방향
        {
            if (IsTargetUsable(target))
            {
                return GetProjectileFireDirection(target, spawnPosition); // 살아있는 타겟 우선
            }

            if (ShouldHoldProjectileSequenceDirectionAfterTargetLoss())
            {
                return GetProjectileFireDirection(target, spawnPosition); // 화염방사기는 마지막 위치가 아니라 현재 포구 방향 유지
            }

            if (hasProjectileSequenceLastAimPoint)
            {
                Vector3 direction = projectileSequenceLastAimPoint - spawnPosition; // 마지막 사격 위치
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }

            return GetProjectileFireDirection(target, spawnPosition); // 최후 fallback
        }

        private bool TryGetProjectileSequenceFallbackImpactPoint(EnemyController target, out Vector3 impactPoint) // 곡사 무기용 마지막 위치 fallback
        {
            impactPoint = default;
            if (AttackProfile == null || AttackProfile.MoveType != SegmentAttackMoveType.ArcProjectile)
            {
                return false; // 직선/화염/톱날은 방향 fallback 사용
            }

            if (IsTargetUsable(target) || !hasProjectileSequenceLastAimPoint)
            {
                return false; // 살아있는 대상 또는 fallback 없음
            }

            impactPoint = projectileSequenceLastAimPoint; // 마지막 유효 조준점
            return true;
        }

        private void UpdateProjectileSequenceAim(float deltaTime) // 발사 중 느린 재조준
        {
            if (AttackProfile == null)
            {
                return; // 프로필 없음
            }

            if (ShouldHoldProjectileSequenceDirectionAfterTargetLoss() && !IsTargetUsable(projectileSequenceTarget))
            {
                if (IsSustainedMuzzleVfxActive())
                {
                    Transform muzzle = ResolveMuzzle();
                    UpdateSustainedMuzzleVfx(muzzle);
                }

                return; // 화염방사기는 타겟이 죽어도 발사 중 머리 회전/재탐색을 하지 않음
            }

            projectileSequenceTarget = ResolveSequenceTarget(projectileSequenceTarget); // 순차 발사 간격마다 재탐색
            UpdateProjectileSequenceLastAimPoint(projectileSequenceTarget); // 새 타겟이 있으면 fallback 갱신
            UpdateProjectileSequencePreferredSide(projectileSequenceTarget); // 조준 방향 갱신
            AimHeadAtTarget(projectileSequenceTarget, deltaTime, GetFiringHeadTurnSpeedMultiplier()); // 느리게 따라감
            if (IsSustainedMuzzleVfxActive())
            {
                Transform muzzle = ResolveMuzzle();
                UpdateSustainedMuzzleVfx(muzzle);
            }
        }

        private bool TryFindProjectileSequenceTargetBySide(out EnemyController target) // 같은 쪽 콘 우선 재탐색
        {
            target = null;
            if (AttackProfile == null
                || !AttackProfile.ContinueAimingDuringProjectileSequence
                || AttackProfile.AttackAreaMode != SegmentAttackAreaMode.SideCones)
            {
                return TryFindTarget(out target); // 일반 무기는 기존 방식
            }

            int preferredSide = NormalizeSideSign(projectileSequencePreferredSide);
            if (TryFindTargetInSideCone(preferredSide, out target))
            {
                return true; // 방금 발사하던 쪽 우선
            }

            return TryFindTargetInSideCone(-preferredSide, out target); // 없으면 반대편
        }

        private void UpdateProjectileSequencePreferredSide(EnemyController target) // 현재 타겟 기준 선호 콘 갱신
        {
            if (AttackProfile == null || AttackProfile.AttackAreaMode != SegmentAttackAreaMode.SideCones || !IsTargetUsable(target))
            {
                return; // 갱신 대상 없음
            }

            projectileSequencePreferredSide = GetTargetSideSign(target); // 이번 발사 방향 저장
        }

        private float GetFiringHeadTurnSpeedMultiplier() // 발사 중 회전 배율
        {
            return AttackProfile != null ? Mathf.Clamp(AttackProfile.FiringHeadTurnSpeedMultiplier, 0.01f, 1f) : 1f;
        }

        private bool ShouldHoldProjectileSequenceDirectionAfterTargetLoss() // 화염방사기 타겟 소실 처리
        {
            return AttackProfile != null
                && AttackProfile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere
                && AttackProfile.UseMuzzleDirectionDuringProjectileSequence;
        }

        private Vector3 GetProjectileFireDirection(EnemyController target, Vector3 spawnPosition) // 투사체 방향 선택
        {
            if (ShouldUseResolvedImpactPoint())
            {
                Vector3 directionToPoint = resolvedImpactPoint - spawnPosition;
                if (directionToPoint.sqrMagnitude > 0.0001f)
                {
                    return directionToPoint.normalized; // 지점 타격 무기는 저장된 착탄 지점으로 발사
                }
            }

            if (AttackProfile != null && AttackProfile.UseMuzzleDirectionDuringProjectileSequence && isFiringProjectileSequence)
            {
                Transform muzzle = ResolveMuzzle();
                Transform pivot = ResolveHeadYawPivot();
                Vector3 direction = GetCurrentMuzzleDirection(pivot, muzzle);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized; // 채널링 중에는 조준에 쓰는 현재 포구 방향
                }
            }

            return GetFireDirection(target, spawnPosition); // 기존 타겟 방향
        }

        private bool ShouldUseResolvedImpactPoint() // 지점 타격 사용 여부
        {
            return AttackProfile != null
                && AttackProfile.TargetPriorityMode == SegmentTargetPriorityMode.DensestClusterOrRandom
                && hasResolvedImpactPoint;
        }
    }
}
