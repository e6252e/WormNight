using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime
    {
        private void UpdateArcProjectile() // 곡사 이동
        {
            arcTimer += Time.deltaTime; // 진행
            float t = Mathf.Clamp01(arcTimer / arcDuration); // 비율
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t); // 직선 보간
            if (!useVerticalImpactDrop)
            {
                position.y += Mathf.Sin(t * Mathf.PI) * profile.ArcHeight; // 포물선 높이
            }

            Vector3 previous = transform.position; // 이전 위치
            transform.position = position; // 이동
            Vector3 moveDirection = position - previous; // 이동 방향
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = ResolveProjectileRotation(moveDirection); // 방향
            }

            if (!ShouldRollAfterArcLanding() && !useVerticalImpactDrop && TryExplodeOnGroundContact(previous, position))
            {
                return; // 지형을 파고들면 즉시 폭발
            }

            if (t >= 1f)
            {
                if (ShouldRollAfterArcLanding())
                {
                    BeginLandingRoll(endPosition); // SG03 돌은 착지 후 일정 거리 굴러간 뒤 처리
                    return;
                }

                ApplyImpactAt(endPosition, target); // 도착 처리
                return;
            }

            if (ShouldRollAfterArcLanding())
            {
                return; // 투석기 돌은 비행 중 충돌하지 않고 바닥 착지 후 처리
            }

            if (useVerticalImpactDrop)
            {
                return; // 낙하형 지점 타격은 바닥 착탄 때만 피해 처리
            }

            TryApplyHitAt(transform.position); // 비행 중 명중
        }

        // 프로필에서 착지 후 굴러가기를 켠 곡사 투사체인지 확인
        private bool ShouldRollAfterArcLanding()
        {
            return profile != null
                && profile.MoveType == SegmentAttackMoveType.ArcProjectile
                && profile.RollAfterArcLanding
                && weaponBonus.ResolveLandingRollDistance(profile.LandingRollDistance) > 0f
                && weaponBonus.ResolveLandingRollDuration(profile.LandingRollDuration) > 0f;
        }

        // 곡사 도착 지점에서 바닥 구르기 상태로 전환
        private void BeginLandingRoll(Vector3 landingPosition)
        {
            isRollingAfterArcLanding = true; // 구르기 상태 시작
            landingRollTimer = 0f; // 진행 시간 초기화
            landingRollDuration = weaponBonus.ResolveLandingRollDuration(profile.LandingRollDuration); // 프로필+강화 시간 보정
            landingRollDirection = ResolveLandingRollDirection(); // 수평 구르기 방향
            landingRollSpinAxis = Vector3.Cross(Vector3.up, landingRollDirection); // 굴러가는 축
            if (landingRollSpinAxis.sqrMagnitude <= 0.0001f)
            {
                landingRollSpinAxis = Vector3.right; // 회전축 fallback
            }

            landingRollSpinAxis.Normalize(); // 회전축 정규화
            landingRollStartPosition = landingPosition; // 착지 위치
            landingRollEndPosition = landingRollStartPosition + landingRollDirection * weaponBonus.ResolveLandingRollDistance(profile.LandingRollDistance); // 종료 위치
            transform.position = landingRollStartPosition; // 바닥 위치 보정
            ApplyLandingImpactDamage(landingRollStartPosition); // 착지 순간 작은 범위 피해
            if (landingRollDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(landingRollDirection, Vector3.up); // 굴러갈 방향으로 정렬
            }
        }

        // 착지 후 돌이 일정 거리 굴러가며 지나가는 적에게 피해를 준다
        private void UpdateLandingRoll()
        {
            landingRollTimer += Time.deltaTime; // 진행 시간 증가
            float t = Mathf.Clamp01(landingRollTimer / landingRollDuration); // 진행률
            float eased = 1f - (1f - t) * (1f - t); // 살짝 감속하는 느낌
            transform.position = Vector3.Lerp(landingRollStartPosition, landingRollEndPosition, eased); // 바닥 이동
            float spinAmount = profile.LandingRollSpinSpeed * Time.deltaTime; // 이번 프레임 회전량
            if (spinAmount > 0f)
            {
                transform.Rotate(landingRollSpinAxis, spinAmount, Space.World); // 돌 굴러가는 회전
            }

            ApplyLandingRollDamage(transform.position); // 구르는 동안 접촉 피해
            if (t >= 1f)
            {
                isRollingAfterArcLanding = false; // 구르기 종료
                Destroy(gameObject); // 끝 폭발 없이 제거
            }
        }

        // 돌이 착지 후 어느 방향으로 굴러갈지 수평 방향 계산
        private Vector3 ResolveLandingRollDirection()
        {
            Vector3 rollDirection = endPosition - startPosition; // 곡사가 날아온 수평 방향 우선
            rollDirection.y = 0f; // 바닥 이동만 사용
            if (rollDirection.sqrMagnitude > 0.0001f)
            {
                return rollDirection.normalized; // 진행 방향 사용
            }

            rollDirection = direction; // 발사 방향 fallback
            rollDirection.y = 0f; // 바닥 이동만 사용
            if (rollDirection.sqrMagnitude > 0.0001f)
            {
                return rollDirection.normalized; // 발사 방향 사용
            }

            rollDirection = transform.forward; // 모델 방향 fallback
            rollDirection.y = 0f; // 바닥 이동만 사용
            return rollDirection.sqrMagnitude > 0.0001f ? rollDirection.normalized : Vector3.forward; // 최종 fallback
        }
    }
}
