using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController // 몸통 충격을 실제 머리와 경로까지 전달한다.
    {
        private const float ShockwavePushDuration = 0.055f; // 순간적으로 밀리는 시간
        private const float MinimumPauseDuration = 0.08f; // 최소 멈칫 시간
        private const float MaximumPauseDuration = 10f; // 최대 멈칫 시간

        private const float CoreHalfWidthBySpacing = 3.0f; // 충격 중심의 강한 범위
        private const float ShoulderWidthBySpacing = 4.0f; // 충격 양쪽의 감소 범위
        private const float BulgeProfilePower = 0.75f; // 돌출부 곡선 형태

        private const float HeadMinimumInfluence = 0.18f; // 꼬리 쪽 충격의 머리 영향
        private const float HeadMaximumInfluence = 0.55f; // 머리 쪽 충격의 머리 영향

        private bool segmentShockwaveActive; // 충격파 진행 여부
        private float segmentShockwaveStartTime; // 충격파 시작 시각
        private float segmentShockwavePauseDuration; // 밀린 위치에서 멈추는 시간

        private Vector3 segmentShockwaveStartHeadPosition; // 충격 전 머리 위치
        private Vector3 segmentShockwaveTargetHeadPosition; // 충격 후 머리 위치

        private readonly List<Vector3> segmentShockwaveStartPath = new List<Vector3>(2048); // 충격 전 경로
        private readonly List<Vector3> segmentShockwaveTargetPath = new List<Vector3>(2048); // 충격 후 경로

        private readonly List<Vector3> segmentShockwaveStartPositions = new List<Vector3>(128); // 충격 전 세그먼트 위치
        private readonly List<Vector3> segmentShockwaveTargetPositions = new List<Vector3>(128); // 충격 후 세그먼트 위치

        private readonly List<Quaternion> segmentShockwaveStartRotations = new List<Quaternion>(128); // 충격 전 세그먼트 회전
        private readonly List<Quaternion> segmentShockwaveTargetRotations = new List<Quaternion>(128); // 충격 후 세그먼트 회전

        public int ApplySegmentShockwave(Vector3 center, float radius, float pushDistance, float recoveryDuration) // 머리와 몸통을 밀고 잠깐 멈칫시킨다.
        {
            center.y = 0.0f;

            float safeRadius = Mathf.Max(0.1f, radius);
            float safePushDistance = Mathf.Max(0.0f, pushDistance);

            if (safePushDistance <= 0.0f || segments.Count == 0 || path.Count == 0)
            {
                return 0;
            }

            if (segmentShockwaveActive)
            {
                StopSegmentShockwave();
            }

            int closestSegmentIndex = FindClosestShockwaveSegment(center, safeRadius * safeRadius);

            if (closestSegmentIndex < 0)
            {
                return 0;
            }

            if (!CaptureShockwaveStartState())
            {
                return 0;
            }

            Transform impactSegment = segments[closestSegmentIndex];
            Vector3 pushDirection = impactSegment.position - center;
            pushDirection.y = 0.0f;

            if (pushDirection.sqrMagnitude <= 0.0001f)
            {
                pushDirection = impactSegment.position - transform.position;
                pushDirection.y = 0.0f;
            }

            if (pushDirection.sqrMagnitude <= 0.0001f)
            {
                pushDirection = transform.right;
            }

            pushDirection.Normalize();

            float impactDistanceBehindHead = GetSegmentDistanceBehindHead(closestSegmentIndex);
            float impactRate = segments.Count > 1 ? Mathf.Clamp01((float)closestSegmentIndex / (segments.Count - 1)) : 0.0f;
            float headInfluence = Mathf.Lerp(HeadMaximumInfluence, HeadMinimumInfluence, impactRate);

            Vector3 requestedHeadPosition = segmentShockwaveStartHeadPosition + pushDirection * safePushDistance * headInfluence;
            requestedHeadPosition = SnapHeadToGround(requestedHeadPosition);
            requestedHeadPosition = MonsterInteractionApi.ResolveConvoyPosition(segmentShockwaveStartHeadPosition, requestedHeadPosition, HeadMonsterBlockRadius);

            segmentShockwaveTargetHeadPosition = requestedHeadPosition;

            Vector3 actualHeadDisplacement = segmentShockwaveTargetHeadPosition - segmentShockwaveStartHeadPosition;
            actualHeadDisplacement.y = 0.0f;

            Vector3 impactOffset = impactSegment.position - center;
            impactOffset.y = 0.0f;

            float impactRadius = impactOffset.magnitude;
            float targetRadius = Mathf.Max(0.1f, impactRadius + safePushDistance);
            float coreHalfWidth = Mathf.Max(SegmentSpacing, SegmentSpacing * CoreHalfWidthBySpacing);
            float shoulderWidth = Mathf.Max(SegmentSpacing, SegmentSpacing * ShoulderWidthBySpacing);
            float totalHalfWidth = coreHalfWidth + shoulderWidth;

            BuildShockwaveTargetPath(center, pushDirection, impactDistanceBehindHead, targetRadius, coreHalfWidth, totalHalfWidth, actualHeadDisplacement);

            if (!CaptureShockwaveTargetPose())
            {
                RestoreShockwaveStartState();
                StopSegmentShockwave();
                return 0;
            }

            RestoreShockwaveStartState();

            segmentShockwaveActive = true;
            segmentShockwaveStartTime = Time.time;
            segmentShockwavePauseDuration = Mathf.Clamp(recoveryDuration, MinimumPauseDuration, MaximumPauseDuration);

            return segments.Count;
        }

        private int FindClosestShockwaveSegment(Vector3 center, float radiusSqr) // 착지 지점에서 가장 가까운 세그먼트를 찾는다.
        {
            int closestIndex = -1;
            float closestDistanceSqr = radiusSqr;

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];

                if (!IsValidShockwaveSegment(segment))
                {
                    continue;
                }

                Vector3 offset = segment.position - center;
                offset.y = 0.0f;

                float distanceSqr = offset.sqrMagnitude;

                if (distanceSqr > closestDistanceSqr)
                {
                    continue;
                }

                closestDistanceSqr = distanceSqr;
                closestIndex = i;
            }

            return closestIndex;
        }

        private bool CaptureShockwaveStartState() // 충격 직전 상태를 저장한다.
        {
            segmentShockwaveStartHeadPosition = transform.position;
            segmentShockwaveTargetHeadPosition = transform.position;

            CopyCurrentPath(segmentShockwaveStartPath);
            CopyCurrentPath(segmentShockwaveTargetPath);

            ResizeVectorBuffer(segmentShockwaveStartPositions, segments.Count);
            ResizeVectorBuffer(segmentShockwaveTargetPositions, segments.Count);
            ResizeQuaternionBuffer(segmentShockwaveStartRotations, segments.Count);
            ResizeQuaternionBuffer(segmentShockwaveTargetRotations, segments.Count);

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];

                if (!IsValidShockwaveSegment(segment))
                {
                    return false;
                }

                segmentShockwaveStartPositions[i] = segment.position;
                segmentShockwaveTargetPositions[i] = segment.position;
                segmentShockwaveStartRotations[i] = segment.rotation;
                segmentShockwaveTargetRotations[i] = segment.rotation;
            }

            return true;
        }

        private void BuildShockwaveTargetPath(Vector3 center, Vector3 fallbackDirection, float impactDistanceBehindHead, float targetRadius, float coreHalfWidth, float totalHalfWidth, Vector3 headDisplacement) // 몸통 돌출과 머리 이동을 목표 경로에 저장한다.
        {
            ResizeVectorBuffer(segmentShockwaveTargetPath, segmentShockwaveStartPath.Count);

            Vector3 previousPosition = segmentShockwaveStartHeadPosition;
            float accumulatedDistance = 0.0f;

            for (int pathIndex = segmentShockwaveStartPath.Count - 1; pathIndex >= 0; pathIndex--)
            {
                Vector3 originalPosition = segmentShockwaveStartPath[pathIndex];
                float sectionDistance = Vector3.Distance(previousPosition, originalPosition);
                float pointDistanceBehindHead = accumulatedDistance + sectionDistance;
                float chainDistanceFromImpact = Mathf.Abs(pointDistanceBehindHead - impactDistanceBehindHead);
                Vector3 targetPosition = originalPosition;

                if (chainDistanceFromImpact <= totalHalfWidth)
                {
                    float bulgeInfluence = CalculateBulgeInfluence(chainDistanceFromImpact, coreHalfWidth, totalHalfWidth);
                    Vector3 radialDirection = originalPosition - center;
                    radialDirection.y = 0.0f;

                    float currentRadius = radialDirection.magnitude;

                    if (radialDirection.sqrMagnitude <= 0.0001f)
                    {
                        radialDirection = fallbackDirection;
                        currentRadius = 0.0f;
                    }
                    else
                    {
                        radialDirection.Normalize();
                    }

                    float desiredRadius = Mathf.Lerp(currentRadius, targetRadius, bulgeInfluence);

                    targetPosition = center + radialDirection * desiredRadius;
                    targetPosition.y = originalPosition.y;
                }

                float headPathInfluence = CalculateHeadPathInfluence(pointDistanceBehindHead, impactDistanceBehindHead);

                targetPosition += headDisplacement * headPathInfluence;
                targetPosition.y = originalPosition.y;

                segmentShockwaveTargetPath[pathIndex] = targetPosition;

                accumulatedDistance += sectionDistance;
                previousPosition = originalPosition;
            }
        }

        private float CalculateBulgeInfluence(float chainDistance, float coreHalfWidth, float totalHalfWidth) // 충격 중심에서 멀어질수록 힘을 줄인다.
        {
            if (chainDistance <= coreHalfWidth)
            {
                return 1.0f;
            }

            if (chainDistance >= totalHalfWidth)
            {
                return 0.0f;
            }

            float shoulderWidth = Mathf.Max(0.01f, totalHalfWidth - coreHalfWidth);
            float shoulderRate = Mathf.Clamp01((chainDistance - coreHalfWidth) / shoulderWidth);
            float cosineInfluence = 0.5f + 0.5f * Mathf.Cos(shoulderRate * Mathf.PI);

            return Mathf.Pow(cosineInfluence, BulgeProfilePower);
        }

        private float CalculateHeadPathInfluence(float pointDistanceBehindHead, float impactDistanceBehindHead) // 머리와 충격 지점 사이의 경로를 함께 이동시킨다.
        {
            float safeImpactDistance = Mathf.Max(SegmentSpacing, impactDistanceBehindHead);

            if (pointDistanceBehindHead >= safeImpactDistance)
            {
                return 0.0f;
            }

            float distanceRate = Mathf.Clamp01(pointDistanceBehindHead / safeImpactDistance);

            return Mathf.SmoothStep(1.0f, 0.0f, distanceRate);
        }

        private bool CaptureShockwaveTargetPose() // 목표 경로에서 세그먼트의 최종 위치를 계산한다.
        {
            transform.position = segmentShockwaveTargetHeadPosition;
            ReplaceCurrentPath(segmentShockwaveTargetPath);

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];

                if (!IsValidShockwaveSegment(segment))
                {
                    return false;
                }

                if (TryCaptureStarterShockwaveTargetPose(segment, i))
                {
                    continue;
                }

                GetPoseBehindHead(GetSegmentDistanceBehindHead(i), out Vector3 targetPosition, out Vector3 targetForward);

                targetPosition = SnapSegmentToGround(i, targetPosition);
                targetForward.y = 0.0f;

                segmentShockwaveTargetPositions[i] = targetPosition;
                segmentShockwaveTargetRotations[i] = targetForward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(targetForward.normalized, Vector3.up) : segmentShockwaveStartRotations[i];
            }

            return true;
        }

        private bool TryCaptureStarterShockwaveTargetPose(Transform segment, int segmentIndex)
        {
            if (!HasActiveStarterSegment || segmentIndex != 0)
            {
                return false;
            }

            if (!TryGetJointBasedSegmentPose(segment, segmentIndex, 1f, true, out Vector3 targetPosition, out Quaternion targetRotation))
            {
                return false;
            }

            segmentShockwaveTargetPositions[segmentIndex] = targetPosition;
            segmentShockwaveTargetRotations[segmentIndex] = targetRotation;
            return true;
        }

        private void RestoreShockwaveStartState() // 목표 계산 후 충격 직전 상태로 되돌린다.
        {
            transform.position = segmentShockwaveStartHeadPosition;
            ReplaceCurrentPath(segmentShockwaveStartPath);

            int safeCount = Mathf.Min(segments.Count, Mathf.Min(segmentShockwaveStartPositions.Count, segmentShockwaveStartRotations.Count));

            for (int i = 0; i < safeCount; i++)
            {
                Transform segment = segments[i];

                if (!IsValidShockwaveSegment(segment))
                {
                    continue;
                }

                segment.SetPositionAndRotation(segmentShockwaveStartPositions[i], segmentShockwaveStartRotations[i]);
            }
        }

        private void LateUpdate() // 빠르게 밀고 잠시 멈춘 뒤 정상 이동으로 넘긴다.
        {
            if (!segmentShockwaveActive)
            {
                return;
            }

            if (!IsShockwaveStateValid())
            {
                StopSegmentShockwave();
                return;
            }

            float elapsedTime = Time.time - segmentShockwaveStartTime;

            if (elapsedTime < ShockwavePushDuration)
            {
                float pushRate = Mathf.Clamp01(elapsedTime / ShockwavePushDuration);
                float fastPushRate = 1.0f - Mathf.Pow(1.0f - pushRate, 4.0f);

                ApplyShockwaveState(fastPushRate);
                return;
            }

            if (elapsedTime < ShockwavePushDuration + segmentShockwavePauseDuration)
            {
                ApplyShockwaveState(1.0f);
                return;
            }

            FinishSegmentShockwave();
        }

        private void ApplyShockwaveState(float rate) // 머리, 경로, 세그먼트를 목표 상태까지 이동시킨다.
        {
            if (!IsShockwaveStateValid())
            {
                StopSegmentShockwave();
                return;
            }

            float safeRate = Mathf.Clamp01(rate);

            transform.position = Vector3.Lerp(segmentShockwaveStartHeadPosition, segmentShockwaveTargetHeadPosition, safeRate);

            ApplyInterpolatedPath(safeRate);

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];

                if (!IsValidShockwaveSegment(segment))
                {
                    continue;
                }

                segment.position = Vector3.Lerp(segmentShockwaveStartPositions[i], segmentShockwaveTargetPositions[i], safeRate);
                segment.rotation = Quaternion.Slerp(segmentShockwaveStartRotations[i], segmentShockwaveTargetRotations[i], safeRate);
            }
        }

        private void ApplyInterpolatedPath(float rate) // 충격 전 경로와 목표 경로를 보간한다.
        {
            path.Clear();

            for (int i = 0; i < segmentShockwaveStartPath.Count; i++)
            {
                path.Add(Vector3.Lerp(segmentShockwaveStartPath[i], segmentShockwaveTargetPath[i], rate));
            }
        }

        private void FinishSegmentShockwave() // 최종 위치를 유지하고 충격파 제어를 종료한다.
        {
            if (IsShockwaveStateValid())
            {
                ApplyShockwaveState(1.0f);
            }

            StopSegmentShockwave();
        }

        private void StopSegmentShockwave() // 현재 위치를 유지하고 충격파 상태만 종료한다.
        {
            segmentShockwaveActive = false;
            segmentShockwaveStartTime = 0.0f;
            segmentShockwavePauseDuration = 0.0f;
        }

        private bool IsShockwaveStateValid() // 현재 세그먼트 수와 저장 목록 크기가 같은지 확인한다.
        {
            int segmentCount = segments.Count;

            return segmentShockwaveStartPositions.Count == segmentCount &&
                   segmentShockwaveTargetPositions.Count == segmentCount &&
                   segmentShockwaveStartRotations.Count == segmentCount &&
                   segmentShockwaveTargetRotations.Count == segmentCount &&
                   segmentShockwaveStartPath.Count > 0 &&
                   segmentShockwaveStartPath.Count == segmentShockwaveTargetPath.Count;
        }

        private void CopyCurrentPath(List<Vector3> target) // 현재 경로를 복사한다.
        {
            target.Clear();

            for (int i = 0; i < path.Count; i++)
            {
                target.Add(path[i]);
            }
        }

        private void ReplaceCurrentPath(List<Vector3> source) // 현재 경로를 저장된 경로로 교체한다.
        {
            path.Clear();

            for (int i = 0; i < source.Count; i++)
            {
                path.Add(source[i]);
            }
        }

        private void ResizeVectorBuffer(List<Vector3> buffer, int requiredCount) // 위치 목록 크기를 맞춘다.
        {
            while (buffer.Count < requiredCount)
            {
                buffer.Add(Vector3.zero);
            }

            while (buffer.Count > requiredCount)
            {
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        private void ResizeQuaternionBuffer(List<Quaternion> buffer, int requiredCount) // 회전 목록 크기를 맞춘다.
        {
            while (buffer.Count < requiredCount)
            {
                buffer.Add(Quaternion.identity);
            }

            while (buffer.Count > requiredCount)
            {
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        private bool IsValidShockwaveSegment(Transform segment) // 사용할 수 있는 세그먼트인지 확인한다.
        {
            return segment != null && segment.gameObject.activeInHierarchy;
        }
    }
}
