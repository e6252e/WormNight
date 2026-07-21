using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private const float HeadVisualLeanDebugMultiplier = 3f;

        private void ResetPath()
        {
            path.Clear();

            Vector3 anchorPosition = GetPathAnchorPosition();
            Vector3 anchorForward = GetPathAnchorForward();
            float starterExtraDistance = EnableStarterSegment ? Mathf.Max(0f, GetEffectiveStarterSegmentDistance() - SegmentSpacing) : 0f;
            float requiredDistance = Mathf.Max((MaxSegmentCount + 4) * SegmentSpacing + starterExtraDistance, 24f);
            float sampleStep = Mathf.Max(MinPathSampleDistance * 4f, 0.25f);

            for (float distance = requiredDistance; distance >= 0f; distance -= sampleStep)
            {
                path.Add(anchorPosition - anchorForward * distance);
            }

            if (path.Count == 0 || HorizontalDistance(path[path.Count - 1], anchorPosition) > 0.001f)
            {
                path.Add(anchorPosition);
            }
        }

        private void SamplePathIfNeeded()
        {
            Vector3 anchorPosition = GetPathAnchorPosition();
            if (path.Count == 0)
            {
                path.Add(anchorPosition);
                return;
            }

            Vector3 last = path[path.Count - 1];
            if (HorizontalDistance(last, anchorPosition) >= MinPathSampleDistance)
            {
                path.Add(anchorPosition);
            }
        }

        private void UpdateHeadVisual(float deltaTime)
        {
            if (HeadVisual == null)
            {
                return;
            }

            Vector3 targetLocalPosition = new Vector3(0f, VisualCenterHeight + knockbackVisualHeight, 0f);
            HeadVisual.localPosition = Vector3.Lerp(
                HeadVisual.localPosition,
                targetLocalPosition,
                ExpLerpFactor(18f, deltaTime));

            Quaternion targetRotation = GetHeadVisualTargetRotation();
            if (HasActiveKnockbackVisualRotation())
            {
                HeadVisual.localRotation = targetRotation;
                return;
            }

            HeadVisual.localRotation = Quaternion.Slerp(HeadVisual.localRotation, targetRotation, ExpLerpFactor(18f, deltaTime));
        }

        private Quaternion GetHeadVisualTargetRotation()
        {
            float pitchAngle = 0f;
            float rollAngle = -GetHeadVisualTurnLeanAmount() * HeadVisualLean * HeadVisualLeanDebugMultiplier;
            if (HasActiveKnockbackVisualRotation())
            {
                Vector3 localKnockbackDirection = transform.InverseTransformDirection(knockbackDirection);
                float tumbleAngle = knockbackElapsedTime * 900f;
                pitchAngle += -localKnockbackDirection.z * tumbleAngle;
                rollAngle += localKnockbackDirection.x * tumbleAngle;
            }

            return Quaternion.Euler(pitchAngle, 0f, rollAngle);
        }

        private float GetHeadVisualTurnLeanAmount()
        {
            float maxTurnSpeed = Mathf.Max(1f, GetEffectiveTurnSpeed());
            return Mathf.Clamp(currentTurnVelocity / maxTurnSpeed, -1f, 1f);
        }

        private bool HasActiveKnockbackVisualRotation()
        {
            return knockbackTimeRemaining > 0f && knockbackTotalTime > 0f;
        }

        private void UpdateSegments(float deltaTime)
        {
            SyncSegmentGroundChecks();
            float moveFactor = ExpLerpFactor(SegmentFollowResponse, deltaTime);
            float turnFactor = ExpLerpFactor(SegmentTurnResponse, deltaTime);

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                if (TryGetJointBasedSegmentPose(segment, i, turnFactor, false, out Vector3 socketPosition, out Quaternion socketRotation))
                {
                    segment.SetPositionAndRotation(socketPosition, socketRotation);
                    continue;
                }

                GetPoseBehindHead(GetSegmentDistanceBehindHead(i), out Vector3 targetPosition, out Vector3 targetForward);
                targetPosition = SnapSegmentToGround(i, targetPosition);

                segment.position = Vector3.Lerp(segment.position, targetPosition, moveFactor);

                if (targetForward.sqrMagnitude > 0.0001f)
                {
                    Vector3 horizontalForward = FlattenSegmentForward(targetForward, segment.forward);
                    Quaternion targetRotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
                    segment.rotation = Quaternion.Slerp(segment.rotation, targetRotation, turnFactor);
                }
            }
        }

        private void SnapSegmentsToPath()
        {
            SyncSegmentGroundChecks();
            for (int i = 0; i < segments.Count; i++)
            {
                SnapSegmentToPath(segments[i], i);
            }
        }

        private void SnapSegmentToPath(Transform segment, int segmentIndex)
        {
            if (segment == null)
            {
                return;
            }

            if (TryGetJointBasedSegmentPose(segment, segmentIndex, 1f, true, out Vector3 socketPosition, out Quaternion socketRotation))
            {
                segment.SetPositionAndRotation(socketPosition, socketRotation);
                return;
            }

            GetPoseBehindHead(GetSegmentDistanceBehindHead(segmentIndex), out Vector3 targetPosition, out Vector3 targetForward);
            targetPosition = SnapSegmentToGround(segmentIndex, targetPosition);
            Vector3 horizontalForward = FlattenSegmentForward(targetForward, transform.forward);
            segment.SetPositionAndRotation(targetPosition, Quaternion.LookRotation(horizontalForward, Vector3.up));
        }

        private bool TryGetJointBasedSegmentPose(
            Transform segment,
            int segmentIndex,
            float turnFactor,
            bool snapRotation,
            out Vector3 targetPosition,
            out Quaternion targetRotation)
        {
            targetPosition = segment != null ? segment.position : Vector3.zero;
            targetRotation = segment != null ? segment.rotation : Quaternion.identity;
            if (!UseJointBasedSegmentLayout() || !TryGetSegmentJointSockets(segment, out Transform frontSocket, out _))
            {
                return false;
            }

            float frontDistance = GetSegmentFrontDistanceBehindHead(segmentIndex);
            float segmentLength = GetSegmentJointLength(segment);
            Vector3 frontTarget = GetSegmentFrontSocketTarget(segmentIndex, frontDistance);
            GetPoseBehindHead(frontDistance + segmentLength, out Vector3 rearTarget, out Vector3 rearForward);

            Vector3 desiredForward = FlattenSegmentForward(frontTarget - rearTarget, rearForward);
            Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, Vector3.up);
            targetRotation = snapRotation ? desiredRotation : Quaternion.Slerp(segment.rotation, desiredRotation, turnFactor);
            targetPosition = GetSocketAlignedRootPosition(segment, frontSocket, targetRotation, frontTarget);
            targetPosition = SnapSegmentToGround(segmentIndex, targetPosition);
            return true;
        }

        private Vector3 GetSegmentFrontSocketTarget(int segmentIndex, float frontDistance)
        {
            if (segmentIndex <= 0)
            {
                return GetPathAnchorPosition();
            }

            int previousIndex = segmentIndex - 1;
            Transform previous = previousIndex >= 0 && previousIndex < segments.Count ? segments[previousIndex] : null;
            if (TryFindSegmentSocket(previous, RearSocketName, out Transform rearSocket))
            {
                return rearSocket.position;
            }

            GetPoseBehindHead(frontDistance, out Vector3 targetPosition, out _);
            return targetPosition;
        }

        private float GetSegmentFrontDistanceBehindHead(int segmentIndex)
        {
            if (!UseJointBasedSegmentLayout())
            {
                return GetSegmentDistanceBehindHead(segmentIndex);
            }

            float distance = 0f;
            int safeIndex = Mathf.Clamp(segmentIndex, 0, segments.Count);
            for (int i = 0; i < safeIndex; i++)
            {
                distance += GetSegmentJointLength(segments[i]);
            }

            return distance;
        }

        private float GetSegmentDistanceBehindHead(int segmentIndex)
        {
            int safeIndex = Mathf.Max(0, segmentIndex);
            if (HasActiveStarterSegment)
            {
                float starterDistance = GetEffectiveStarterSegmentDistance();
                if (safeIndex == 0)
                {
                    return starterDistance;
                }

                return starterDistance + safeIndex * SegmentSpacing;
            }

            return (safeIndex + 1) * SegmentSpacing;
        }

        private void GetPoseBehindHead(float distanceBehindHead, out Vector3 position, out Vector3 forward)
        {
            Vector3 previous = GetPathAnchorPosition();
            float accumulated = 0f;

            for (int i = path.Count - 1; i >= 0; i--)
            {
                Vector3 current = path[i];
                float length = HorizontalDistance(previous, current);

                if (length <= 0.0001f)
                {
                    previous = current;
                    continue;
                }

                if (accumulated + length >= distanceBehindHead)
                {
                    float t = (distanceBehindHead - accumulated) / length;
                    position = Vector3.Lerp(previous, current, t);
                    forward = previous - current;
                    return;
                }

                accumulated += length;
                previous = current;
            }

            float remaining = distanceBehindHead - accumulated;
            forward = GetPathAnchorForward();
            position = previous - forward * remaining;
        }

        private bool UseJointBasedSegmentLayout()
        {
            return GetHeadJoint() != null;
        }

        private Transform GetHeadJoint()
        {
            if (HeadJoint != null)
            {
                return HeadJoint;
            }

            Transform directJoint = transform.Find(HeadJointName);
            if (directJoint != null)
            {
                HeadJoint = directJoint;
            }

            return HeadJoint;
        }

        private Vector3 GetPathAnchorPosition()
        {
            Transform joint = GetHeadJoint();
            return joint != null ? joint.position : transform.position;
        }

        private Vector3 GetPathAnchorForward()
        {
            Transform joint = GetHeadJoint();
            Vector3 forward = joint != null ? joint.forward : transform.forward;
            return FlattenSegmentForward(forward, transform.forward);
        }

        private bool TryGetSegmentJointSockets(Transform segment, out Transform frontSocket, out Transform rearSocket)
        {
            bool hasFront = TryFindSegmentSocket(segment, FrontSocketName, out frontSocket);
            bool hasRear = TryFindSegmentSocket(segment, RearSocketName, out rearSocket);
            return hasFront && hasRear;
        }

        private bool TryFindSegmentSocket(Transform segment, string socketName, out Transform socket)
        {
            socket = segment != null ? FindChildRecursive(segment, socketName) : null;
            return socket != null;
        }

        private float GetSegmentJointLength(Transform segment)
        {
            if (TryGetSegmentJointSockets(segment, out Transform frontSocket, out Transform rearSocket))
            {
                float socketDistance = HorizontalDistance(frontSocket.position, rearSocket.position);
                if (socketDistance > 0.01f)
                {
                    return socketDistance;
                }
            }

            return Mathf.Max(0.1f, SegmentSpacing);
        }

        private Vector3 GetSocketAlignedRootPosition(
            Transform segment,
            Transform socket,
            Quaternion targetRotation,
            Vector3 targetSocketPosition)
        {
            Vector3 localSocket = segment.InverseTransformPoint(socket.position);
            Vector3 scaledLocalSocket = Vector3.Scale(localSocket, segment.lossyScale);
            return targetSocketPosition - targetRotation * scaledLocalSocket;
        }

        private Vector3 GetSegmentSocketLocalPointOrFallback(Transform segment, string socketName, Vector3 fallback)
        {
            if (TryFindSegmentSocket(segment, socketName, out Transform socket))
            {
                return segment.InverseTransformPoint(socket.position);
            }

            return fallback;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 FlattenSegmentForward(Vector3 forward, Vector3 fallback)
        {
            forward.y = 0.0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                return forward.normalized;
            }

            fallback.y = 0.0f;
            if (fallback.sqrMagnitude > 0.0001f)
            {
                return fallback.normalized;
            }

            return Vector3.forward;
        }

        private void PrunePath()
        {
            int overflow = path.Count - PathSampleLimit;
            if (overflow > 0)
            {
                path.RemoveRange(0, overflow);
            }
        }
    }
}
