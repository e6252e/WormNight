using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private bool TryStartTrebuchetFireMotion(EnemyController target)
        {
            SegmentTrebuchetFireMotion motion = ResolveTrebuchetFireMotion(); // 모션 컴포넌트
            if (motion == null || !motion.CanPlayMotion || motion.IsPlaying)
            {
                return false; // 투석기 모션 없음
            }

            projectileSequenceRoutine = StartCoroutine(FireTrebuchetMotionSequence(target, motion)); // 회전 후 발사
            return true; // 시작 성공
        }

        // 숟가락이 90도 발사 지점에 도달했을 때 실제 투사체를 생성
        private IEnumerator FireTrebuchetMotionSequence(EnemyController initialTarget, SegmentTrebuchetFireMotion motion)
        {
            isFiringProjectileSequence = true; // 중복 발사 방지
            hasProjectileSequenceLastAimPoint = false; // 이전 조준점 제거
            projectileSequenceTarget = ResolveSequenceTarget(initialTarget); // 모션 시작 시점 대상
            UpdateProjectileSequenceLastAimPoint(projectileSequenceTarget); // 모션 중 대상 사망 대비
            UpdateProjectileSequencePreferredSide(projectileSequenceTarget); // 선호 방향 저장
            CacheLoadedProjectileVisuals(); // 표시 목록 갱신
            bool releasedProjectile = false; // 발사 콜백 실행 여부
            bool queuedThrowSway = false; // 팔로우스루 이후 실행할 흔들림 예약 여부
            Vector3 queuedThrowSwayDirection = Vector3.zero; // 예약된 던지는 방향
            Transform queuedThrowSwayMuzzle = null; // 예약된 포구

            PlayTrebuchetFireStartSfx(ResolveMuzzle());
            yield return motion.PlayReleaseMotion(() =>
            {
                if (!CanUseWeapon())
                {
                    return; // 분리/비활성
                }

                EnemyController target = ResolveSequenceTarget(projectileSequenceTarget); // 현재 대상
                projectileSequenceTarget = target; // 다음 조준 기준
                UpdateProjectileSequenceLastAimPoint(target); // 새 대상 또는 기존 조준점 유지
                UpdateProjectileSequencePreferredSide(target); // 선호 방향 갱신
                Transform muzzle = ResolveMuzzle(); // 포구
                int count = Mathf.Max(1, AttackProfile.ProjectileCount); // 발사 수
                float spread = Mathf.Max(0f, AttackProfile.SpreadAngle); // 산탄 각도

                for (int i = 0; i < count; i++)
                {
                    Vector3 spawnPosition = GetProjectileSpawnPosition(i, muzzle); // 숟가락 돌 위치 우선
                    DamageData damage = CreateDamageData(spawnPosition); // 피해값
                    Vector3 fireDirection = GetProjectileSequenceFireDirection(target, spawnPosition); // 실제 발사 방향
                    bool useFallbackImpactPoint = TryGetProjectileSequenceFallbackImpactPoint(target, out Vector3 fallbackImpactPoint); // 대상 없음 fallback
                    PlayMuzzleVfx(muzzle); // 발사 VFX
                    if (i == 0)
                    {
                        queuedThrowSway = true; // 팔로우스루가 끝난 뒤 한 번만 흔들림
                        queuedThrowSwayDirection = fireDirection; // 발사 순간 방향 저장
                        queuedThrowSwayMuzzle = muzzle; // 발사 순간 포구 저장
                    }

                    FireSingleProjectile(target, spawnPosition, damage, i, count, spread, muzzle, true, fireDirection, useFallbackImpactPoint, fallbackImpactPoint); // 투사체 생성
                    HideLoadedProjectileVisual(i); // 숟가락 위 돌 숨김
                }

                ResetCooldown(); // 발사 순간부터 쿨타임 진행
                releasedProjectile = true; // 발사 완료
            }, () =>
            {
                if (queuedThrowSway && CanUseWeapon())
                {
                    PlayTrebuchetThrowSway(queuedThrowSwayDirection, queuedThrowSwayMuzzle, motion); // 끝까지 휘두른 뒤 던진 방향으로 흔들림
                }
            });

            if (!releasedProjectile && CanUseWeapon())
            {
                ResetCooldown(); // 예외적으로 발사 콜백이 못 돌았을 때 무한 대기 방지
            }

            projectileSequenceRoutine = null; // 코루틴 해제
            isFiringProjectileSequence = false; // 발사 완료
            projectileSequenceTarget = null; // 대상 초기화
            projectileSequencePreferredSide = 0; // 선호 방향 초기화
            hasProjectileSequenceLastAimPoint = false; // fallback 초기화
        }

        private void CacheTrebuchetFireMotion()
        {
            trebuchetFireMotion = GetComponentInChildren<SegmentTrebuchetFireMotion>(true); // 세그먼트 안에서 검색
            if (trebuchetFireMotion != null)
            {
                trebuchetFireMotion.CaptureBasePoseIfNeeded(); // 현재 프리팹 기준 자세 저장
            }
        }

        // 필요할 때 투석기 모션 컴포넌트 재검색
        private SegmentTrebuchetFireMotion ResolveTrebuchetFireMotion()
        {
            if (trebuchetFireMotion == null)
            {
                CacheTrebuchetFireMotion(); // 런타임 교체 후 재검색
            }

            return trebuchetFireMotion;
        }

        // 비활성/분리 때 투석기 숟가락 회전 상태를 복구
        private void StopTrebuchetFireMotion()
        {
            SegmentTrebuchetFireMotion motion = ResolveTrebuchetFireMotion(); // 모션 컴포넌트
            if (motion != null)
            {
                motion.StopMotion(true); // 기준 자세로 복구
            }
        }
    }
}
