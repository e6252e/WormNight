using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private const int SegmentCutTailCandidateCount = 3; // 전찬우수정-0630 - 절단 몬스터는 꼬리 기준 1~3번째 세그먼트만 노린다.

        private readonly HashSet<Transform> reservedSegmentCutTargets = new HashSet<Transform>();

        public bool TryGetRandomAttachedWeaponSegment(out Transform targetSegment)
        {
            targetSegment = null;

            CleanupReservedSegmentCutTargets();

            List<Transform> tailCandidates = GetAvailableTailSegmentCutTargets();

            if (tailCandidates.Count <= 0)
            {
                return false;
            }

            int randomWeaponIndex = Random.Range(0, tailCandidates.Count);

            targetSegment = tailCandidates[randomWeaponIndex];
            reservedSegmentCutTargets.Add(targetSegment);

            return true;
        }

        public bool HasAvailableSegmentCutTarget()
        {
            CleanupReservedSegmentCutTargets();

            for (int i = segments.Count - 1, checkedCount = 0; i >= GetFirstDetachableSegmentIndex() && checkedCount < SegmentCutTailCandidateCount; i--, checkedCount++)
            {
                if (IsAvailableSegmentCutTarget(segments[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSegmentCutTailFollowTarget(out Transform tailSegment)
        {
            tailSegment = null;

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                Transform segment = segments[i];

                if (segment == null || !segment.gameObject.activeInHierarchy)
                {
                    continue;
                }

                tailSegment = segment;
                return true;
            }

            return false;
        }

        public bool IsAttachedSegmentCutTarget(Transform targetSegment)
        {
            if (targetSegment == null)
            {
                return false;
            }

            int segmentIndex = segments.IndexOf(targetSegment);

            if (segmentIndex < 0)
            {
                return false;
            }

            if (segmentIndex < GetFirstDetachableSegmentIndex())
            {
                return false;
            }

            return IsAttachedWeaponSegment(targetSegment);
        }

        public void ReleaseSegmentCutTarget(Transform targetSegment)
        {
            reservedSegmentCutTargets.Remove(targetSegment);
        }

        public bool IsConvoyHeadCollider(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            Transform hitTransform = other.transform;

            if (hitTransform == transform)
            {
                return true;
            }

            if (HeadVisual != null && (hitTransform == HeadVisual || hitTransform.IsChildOf(HeadVisual)))
            {
                return true;
            }

            return false;
        }

        public bool IsTargetSegmentCollider(Collider other, Transform targetSegment)
        {
            if (other == null || targetSegment == null)
            {
                return false;
            }

            Transform hitTransform = other.transform;

            return hitTransform == targetSegment || hitTransform.IsChildOf(targetSegment);
        }

        public bool TryCutTailFromTargetSegment(Transform targetSegment)
        {
            if (!IsAttachedSegmentCutTarget(targetSegment))
            {
                return false;
            }

            if (tailCutCooldownRemaining > 0.0f)
            {
                return false;
            }

            int segmentIndex = segments.IndexOf(targetSegment);
            Vector3 burstCenter = targetSegment.position;

            CutTailFromIndex(segmentIndex, burstCenter);

            CleanupReservedSegmentCutTargets();

            return true;
        }

        private List<Transform> GetAvailableTailSegmentCutTargets()
        {
            List<Transform> tailCandidates = new List<Transform>(SegmentCutTailCandidateCount);

            for (int i = segments.Count - 1, checkedCount = 0; i >= GetFirstDetachableSegmentIndex() && checkedCount < SegmentCutTailCandidateCount; i--, checkedCount++)
            {
                Transform segment = segments[i];

                if (!IsAvailableSegmentCutTarget(segment))
                {
                    continue;
                }

                tailCandidates.Add(segment);
            }

            return tailCandidates;
        }

        private bool IsAvailableSegmentCutTarget(Transform segment)
        {
            if (!IsAttachedSegmentCutTarget(segment))
            {
                return false;
            }

            return !reservedSegmentCutTargets.Contains(segment);
        }

        private void CleanupReservedSegmentCutTargets()
        {
            reservedSegmentCutTargets.RemoveWhere(segment => !IsAttachedSegmentCutTarget(segment));
        }

        private bool IsAttachedWeaponSegment(Transform segment)
        {
            if (segment == null)
            {
                return false;
            }

            if (!segment.gameObject.activeInHierarchy)
            {
                return false;
            }

            SegmentWeaponBehaviour weapon = segment.GetComponent<SegmentWeaponBehaviour>();

            if (weapon == null)
            {
                weapon = segment.GetComponentInChildren<SegmentWeaponBehaviour>();
            }

            return weapon != null;
        }
    }
}
