using DG.Tweening;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private void PlayFireRecoil(Vector3 fireDirection, Transform muzzle)
        {
            if (AttackProfile == null)
            {
                return; // 프로필 없음
            }

            float distance = AttackProfile.RecoilDistance; // 데이터에 지정된 이동 반동
            float tiltAngle = AttackProfile.RecoilTiltAngle; // 데이터에 지정된 회전 반동
            ApplyDefaultLightMissileRecoilIfNeeded(ref distance, ref tiltAngle); // SG02 미사일 기본 약반동

            PlayFireRecoil(
                fireDirection,
                muzzle,
                false,
                distance,
                tiltAngle,
                AttackProfile.RecoilKickDuration,
                AttackProfile.RecoilReturnDuration,
                AttackProfile.RecoilSettleDistanceRatio,
                AttackProfile.RecoilSettleTiltRatio,
                AttackProfile.RecoilSettleDuration); // 기존 캐논 반동은 발사 반대 방향
        }

        // 투석기 돌 발사 후에는 반동 반대가 아니라 던진 방향으로 몸체가 쏠리게 재생
        private void PlayTrebuchetThrowSway(Vector3 fireDirection, Transform muzzle, SegmentTrebuchetFireMotion motion)
        {
            if (motion == null || !motion.UseThrowSway)
            {
                return; // 투석기 전용 흔들림 미사용
            }

            PlayFireRecoil(
                fireDirection,
                muzzle,
                true,
                motion.ThrowSwayDistance,
                motion.ThrowSwayTiltAngle,
                motion.ThrowSwayKickDuration,
                motion.ThrowSwayReturnDuration,
                motion.ThrowSwaySettleDistanceRatio,
                motion.ThrowSwaySettleTiltRatio,
                motion.ThrowSwaySettleDuration); // 던진 방향으로 흔들림
        }

        // 캐논 반동과 투석기 전방 흔들림이 같은 트윈 시스템을 공유하도록 방향/값을 외부에서 받음
        private void PlayFireRecoil(
            Vector3 fireDirection,
            Transform muzzle,
            bool pushTowardFireDirection,
            float recoilDistance,
            float recoilTiltAngle,
            float recoilKickDuration,
            float recoilReturnDuration,
            float recoilSettleDistanceRatio,
            float recoilSettleTiltRatio,
            float recoilSettleDuration)
        {
            float distance = Mathf.Max(0f, recoilDistance); // 이동 반동
            float tiltAngle = Mathf.Max(0f, recoilTiltAngle); // 회전 반동
            if (distance <= 0f && tiltAngle <= 0f)
            {
                return; // 미사일처럼 반동 없는 공격
            }

            ResetFireRecoilTweenOnly(); // 기존 트윈 정리
            RestoreFireRecoilPose(); // 이전 반동 중간 pose를 먼저 복구
            CollectFireRecoilTargets(); // 흔들 대상 수집
            if (fireRecoilTargets.Count == 0)
            {
                return; // 대상 없음
            }

            CacheFireRecoilPoses(); // 원래 pose 저장

            Vector3 worldRecoilDirection = GetWorldRecoilDirection(fireDirection, muzzle, pushTowardFireDirection); // 캐논은 반대, 투석기는 던진 방향
            float kickDuration = Mathf.Max(0.01f, recoilKickDuration); // 밀림 시간
            float returnDuration = Mathf.Max(0.01f, recoilReturnDuration); // 복귀 시간
            float settleDistance = distance * Mathf.Max(0f, recoilSettleDistanceRatio); // 원점 반대쪽 되받음 거리
            float settleTilt = tiltAngle * Mathf.Max(0f, recoilSettleTiltRatio); // 원점 반대쪽 되받음 회전
            float settleDuration = Mathf.Max(0.01f, recoilSettleDuration); // 마지막 자리 잡는 시간
            recoilSequence = DOTween.Sequence(); // 새 반동 시퀀스

            for (int i = 0; i < recoilTargetPoses.Count; i++)
            {
                RecoilTargetPose pose = recoilTargetPoses[i]; // 대상 pose
                if (pose.Target == null)
                {
                    continue; // 삭제된 대상
                }

                Vector3 localDirection = GetLocalRecoilDirection(pose.Target, worldRecoilDirection); // 대상 부모 기준 방향
                Vector3 targetPosition = pose.LocalPosition + localDirection * distance; // 반동 위치
                Quaternion targetRotation = GetRecoilRotation(pose.LocalRotation, localDirection, tiltAngle); // 반동 회전
                recoilSequence.Join(pose.Target.DOLocalMove(targetPosition, kickDuration).SetEase(Ease.OutQuad)); // 뒤로 밀림
                recoilSequence.Join(pose.Target.DOLocalRotateQuaternion(targetRotation, kickDuration).SetEase(Ease.OutQuad)); // 살짝 젖음
            }

            bool appendedReturn = false; // 되받음 구간 시작 여부
            for (int i = 0; i < recoilTargetPoses.Count; i++)
            {
                RecoilTargetPose pose = recoilTargetPoses[i]; // 대상 pose
                if (pose.Target == null)
                {
                    continue; // 삭제된 대상
                }

                Vector3 localDirection = GetLocalRecoilDirection(pose.Target, worldRecoilDirection); // 대상 부모 기준 방향
                Vector3 settlePosition = pose.LocalPosition - localDirection * settleDistance; // 원점을 살짝 지나친 위치
                Quaternion settleRotation = GetRecoilRotation(pose.LocalRotation, -localDirection, settleTilt); // 반대 방향으로 살짝 되받는 회전
                Tween moveBack = pose.Target.DOLocalMove(settlePosition, returnDuration).SetEase(Ease.InOutSine); // 바로 원점 대신 되받는 지점으로 복귀
                Tween rotateBack = pose.Target.DOLocalRotateQuaternion(settleRotation, returnDuration).SetEase(Ease.InOutSine); // 회전도 살짝 되받음
                if (!appendedReturn)
                {
                    recoilSequence.Append(moveBack); // 첫 되받음 트윈으로 구간 생성
                    recoilSequence.Join(rotateBack); // 첫 회전 되받음
                    appendedReturn = true; // 되받음 구간 시작됨
                }
                else
                {
                    recoilSequence.Join(moveBack); // 다른 대상도 같은 타이밍에 되받음
                    recoilSequence.Join(rotateBack); // 다른 대상 회전 되받음
                }
            }

            bool appendedSettle = false; // 최종 자리 잡기 구간 시작 여부
            for (int i = 0; i < recoilTargetPoses.Count; i++)
            {
                RecoilTargetPose pose = recoilTargetPoses[i]; // 대상 pose
                if (pose.Target == null)
                {
                    continue; // 삭제된 대상
                }

                Tween moveSettle = pose.Target.DOLocalMove(pose.LocalPosition, settleDuration).SetEase(Ease.OutSine); // 원래 위치로 둔하게 정착
                Tween rotateSettle = pose.Target.DOLocalRotateQuaternion(pose.LocalRotation, settleDuration).SetEase(Ease.OutSine); // 원래 회전으로 둔하게 정착
                if (!appendedSettle)
                {
                    recoilSequence.Append(moveSettle); // 최종 정착 구간 생성
                    recoilSequence.Join(rotateSettle); // 첫 회전 정착
                    appendedSettle = true; // 정착 구간 시작됨
                }
                else
                {
                    recoilSequence.Join(moveSettle); // 다른 대상도 같은 타이밍에 정착
                    recoilSequence.Join(rotateSettle); // 다른 대상 회전 정착
                }
            }

            recoilSequence.OnComplete(() =>
            {
                RestoreFireRecoilPose(); // 미세 오차 보정
                recoilSequence = null; // 시퀀스 해제
            });
        }

        // 기존 반동 트윈만 정리
        private void ResetFireRecoilTweenOnly()
        {
            if (recoilSequence == null)
            {
                return; // 정리할 트윈 없음
            }

            recoilSequence.Kill(false); // 콜백 없이 중단
            recoilSequence = null; // 참조 해제
        }

        // 반동 중간 상태를 원래 위치로 되돌림
        private void ResetFireRecoilPose()
        {
            ResetFireRecoilTweenOnly(); // 트윈 정리
            RestoreFireRecoilPose(); // pose 복구
        }

        // 저장된 반동 대상 pose 복구
        private void RestoreFireRecoilPose()
        {
            for (int i = 0; i < recoilTargetPoses.Count; i++)
            {
                RecoilTargetPose pose = recoilTargetPoses[i]; // 저장 pose
                if (pose.Target == null)
                {
                    continue; // 삭제된 대상
                }

                pose.Target.localPosition = pose.LocalPosition; // 위치 복구
                pose.Target.localRotation = pose.LocalRotation; // 회전 복구
            }
        }

        // 반동 대상의 현재 pose를 기준으로 저장
        private void CacheFireRecoilPoses()
        {
            recoilTargetPoses.Clear(); // 이전 저장값 제거
            for (int i = 0; i < fireRecoilTargets.Count; i++)
            {
                Transform target = fireRecoilTargets[i]; // 반동 대상
                if (target == null)
                {
                    continue; // 삭제된 대상
                }

                recoilTargetPoses.Add(new RecoilTargetPose(target, target.localPosition, target.localRotation)); // 현재 pose 저장
            }
        }

        // 바디와 헤드가 같이 쏠리도록 반동 대상 수집
        private void CollectFireRecoilTargets()
        {
            fireRecoilTargets.Clear(); // 이전 대상 제거
            Transform segmentRoot = Segment != null ? Segment.transform : transform; // 세그먼트 루트
            Transform explicitRoot = FindChildRecursive(segmentRoot, "VisualRecoilRoot"); // 전용 루트
            if (explicitRoot != null)
            {
                fireRecoilTargets.Add(explicitRoot); // 전용 루트가 있으면 하나만 흔듦
                return; // 수집 완료
            }

            Transform visual = FindDirectChild(segmentRoot, "Visual"); // 바디/모델 비주얼
            AddFireRecoilTargetIfNeeded(visual); // 바디 비주얼 추가

            Transform pivot = ResolveHeadYawPivot(); // 헤드 회전축
            Transform headRoot = FindDirectChildContaining(segmentRoot, pivot); // 헤드 직계 루트
            AddFireRecoilTargetIfNeeded(headRoot); // 헤드가 Visual 밖이면 추가

            if (fireRecoilTargets.Count == 0 && pivot != null)
            {
                fireRecoilTargets.Add(pivot); // 최후 fallback: 헤드 피벗만
            }
        }

        // 이미 상위 대상이 흔들리면 중복 추가하지 않음
        private void AddFireRecoilTargetIfNeeded(Transform candidate)
        {
            if (candidate == null)
            {
                return; // 대상 없음
            }

            for (int i = 0; i < fireRecoilTargets.Count; i++)
            {
                Transform existing = fireRecoilTargets[i]; // 기존 대상
                if (existing == null)
                {
                    continue; // 삭제됨
                }

                if (candidate == existing || candidate.IsChildOf(existing))
                {
                    return; // 기존 대상이 이미 후보를 포함
                }
            }

            fireRecoilTargets.Add(candidate); // 새 대상 추가
        }

        // 캐논은 포구 반대 방향, 투석기는 던진 방향을 수평 흔들림 방향으로 변환
        private Vector3 GetWorldRecoilDirection(Vector3 fireDirection, Transform muzzle, bool pushTowardFireDirection)
        {
            Vector3 direction = pushTowardFireDirection ? fireDirection : -fireDirection; // 투석기는 던진 쪽, 캐논은 반대쪽
            direction.y = 0f; // 바디가 땅에서 들리지 않도록 수평화
            if (direction.sqrMagnitude <= 0.0001f && muzzle != null)
            {
                direction = pushTowardFireDirection ? muzzle.forward : -muzzle.forward; // 타겟 방향을 못 구한 경우 포구축 fallback
            }

            direction.y = 0f; // 바디가 땅에서 들리지 않도록 수평화
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Transform segmentRoot = Segment != null ? Segment.transform : transform; // fallback 기준
                direction = pushTowardFireDirection ? segmentRoot.forward : -segmentRoot.forward; // 최종 fallback
                direction.y = 0f; // 수평화
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : (pushTowardFireDirection ? Vector3.forward : -Vector3.forward); // 최종 방향
        }

        // 미사일 데이터에 반동값을 따로 저장하지 않았을 때만 아주 약한 기본 반동을 적용
        private void ApplyDefaultLightMissileRecoilIfNeeded(ref float distance, ref float tiltAngle)
        {
            if (distance > 0f || tiltAngle > 0f)
            {
                return; // 데이터에서 직접 설정한 값 우선
            }

            if (!ShouldUseDefaultLightMissileRecoil())
            {
                return; // 미사일 계열 아님
            }

            distance = DefaultLightMissileRecoilDistance; // 약한 위치 반동
            tiltAngle = DefaultLightMissileRecoilTiltAngle; // 약한 회전 반동
        }

        // SG02처럼 곡사 폭발 로켓이며 장전 투사체 표시를 쓰는 공격만 기본 약반동 적용
        private bool ShouldUseDefaultLightMissileRecoil()
        {
            return AttackProfile != null
                && AttackProfile.MoveType == SegmentAttackMoveType.ArcProjectile
                && AttackProfile.ImpactType == SegmentAttackImpactType.ExplosionArea
                && AttackProfile.UseLoadedProjectileVisuals
                && ResolveTrebuchetFireMotion() == null; // SG03 투석기는 전용 ThrowSway를 사용
        }

        // 월드 반동 방향을 대상 부모 로컬 방향으로 변환
        private static Vector3 GetLocalRecoilDirection(Transform target, Vector3 worldDirection)
        {
            Transform parent = target != null ? target.parent : null; // 부모 기준
            Vector3 localDirection = parent != null ? parent.InverseTransformDirection(worldDirection) : worldDirection; // 로컬 변환
            localDirection.y = 0f; // 로컬에서도 수직 이동 제거
            return localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : -Vector3.forward; // fallback
        }

        // 반동 방향으로 윗부분이 살짝 밀리는 회전 계산
        private static Quaternion GetRecoilRotation(Quaternion baseRotation, Vector3 localDirection, float tiltAngle)
        {
            if (tiltAngle <= 0f)
            {
                return baseRotation; // 회전 반동 없음
            }

            Vector3 tiltAxis = Vector3.Cross(Vector3.up, localDirection); // 반동 방향으로 젖는 축
            if (tiltAxis.sqrMagnitude <= 0.0001f)
            {
                tiltAxis = Vector3.right; // fallback 축
            }

            return baseRotation * Quaternion.AngleAxis(tiltAngle, tiltAxis.normalized); // 기준 회전에 반동 회전 추가
        }


        // 반동 대상의 원래 로컬 pose 저장값
        private readonly struct RecoilTargetPose
        {
            public readonly Transform Target; // 반동 대상
            public readonly Vector3 LocalPosition; // 원래 위치
            public readonly Quaternion LocalRotation; // 원래 회전

            public RecoilTargetPose(Transform target, Vector3 localPosition, Quaternion localRotation)
            {
                Target = target; // 대상 저장
                LocalPosition = localPosition; // 위치 저장
                LocalRotation = localRotation; // 회전 저장
            }
        }
    }
}
