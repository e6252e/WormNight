using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyCrowdBlocker : MonoBehaviour
    {
        private const float GridCellSize = 2.0f;
        private static readonly List<EnemyCrowdBlocker> ActiveBlockers = new List<EnemyCrowdBlocker>(512);
        private static readonly Dictionary<CellKey, List<EnemyCrowdBlocker>> Grid = new Dictionary<CellKey, List<EnemyCrowdBlocker>>(256);
        private static readonly List<CellKey> UsedCells = new List<CellKey>(256);

        private static int builtGridFrame = -1;
        private static float maxActiveRadius = 0.5f;

        public static bool GlobalEnabled = true;

        [SerializeField] private bool enableCrowdBlock = true;
        [SerializeField, Min(0.05f)] private float blockRadius = 0.2f;
        [SerializeField] private Transform centerTransform;
        [SerializeField] private Vector3 centerOffset;
        [SerializeField, Range(0.0f, 1.0f)] private float pushStrength = 1.0f;
        [SerializeField, Min(0.0f)] private float maxCorrectionPerFrame = 0.25f;
        [SerializeField, Range(0.0f, 1.0f)] private float sideSpreadStrength = 0.65f;
        [SerializeField, Min(0.0f)] private float blockedSidePushPerFrame = 0.08f;
        [SerializeField, Range(0.0f, 1.0f)] private float pressureTransferRatio = 0.7f;
        [SerializeField, Min(0.0f)] private float maxPendingPushPerFrame = 0.24f;
        [SerializeField, Min(0.0f)] private float pushSmoothingSpeed = 10.0f;
        [SerializeField, Min(0.0f)] private float pushDecaySpeed = 6.0f;
        [SerializeField] private bool drawGizmo = true;

        private EnemyController controller;
        private Vector3 pendingPush;
        private Vector3 smoothedPush;
        private bool isCrowdMoving;

        public float BlockRadius
        {
            get
            {
                return Mathf.Max(0.05f, blockRadius);
            }
        }

        private bool IsBlockingEnabled
        {
            get
            {
                return enableCrowdBlock && isActiveAndEnabled && (controller == null || !controller.IsDead);
            }
        }

        private void Awake()
        {
            controller = GetComponentInParent<EnemyController>();
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<EnemyController>();
            }

            ClearPendingPush();
            isCrowdMoving = false;

            if (!ActiveBlockers.Contains(this))
            {
                ActiveBlockers.Add(this);
                InvalidateGrid();
            }
        }

        private void OnDisable()
        {
            ClearPendingPush();
            isCrowdMoving = false;

            if (ActiveBlockers.Remove(this))
            {
                InvalidateGrid();
            }
        }

        private void OnValidate()
        {
            blockRadius = Mathf.Max(0.05f, blockRadius);
            pushStrength = Mathf.Clamp01(pushStrength);
            maxCorrectionPerFrame = Mathf.Max(0.0f, maxCorrectionPerFrame);
            sideSpreadStrength = Mathf.Clamp01(sideSpreadStrength);
            blockedSidePushPerFrame = Mathf.Max(0.0f, blockedSidePushPerFrame);
            pressureTransferRatio = Mathf.Clamp01(pressureTransferRatio);
            maxPendingPushPerFrame = Mathf.Max(0.0f, maxPendingPushPerFrame);
            pushSmoothingSpeed = Mathf.Max(0.0f, pushSmoothingSpeed);
            pushDecaySpeed = Mathf.Max(0.0f, pushDecaySpeed);
            InvalidateGrid();
        }

        public void Configure(float radius, Transform center, float strength, float maxCorrection)
        {
            blockRadius = Mathf.Max(0.05f, radius);
            centerTransform = center;
            pushStrength = Mathf.Clamp01(strength);
            maxCorrectionPerFrame = Mathf.Max(0.0f, maxCorrection);
            enableCrowdBlock = true;
            InvalidateGrid();
        }

        public void SetCrowdMoving(bool moving)
        {
            isCrowdMoving = moving;
        }

        public void ClearPendingPush()
        {
            pendingPush = Vector3.zero;
            smoothedPush = Vector3.zero;
        }

        public static Vector3 ResolvePosition(EnemyCrowdBlocker mover, Vector3 currentPosition, Vector3 desiredPosition, float fallbackRadius)
        {
            if (!GlobalEnabled || mover == null || !mover.IsBlockingEnabled)
            {
                return desiredPosition;
            }

            RebuildGridIfNeeded();

            Vector3 requestedDesired = desiredPosition;
            desiredPosition = mover.ConsumePendingPush(desiredPosition, out Vector3 consumedPush);
            Vector3 originalDesired = desiredPosition;
            Vector3 result = desiredPosition;
            float moverRadius = Mathf.Max(0.05f, mover.BlockRadius > 0.0f ? mover.BlockRadius : fallbackRadius);
            Vector3 desiredCenter = mover.GetCenterForPosition(result);
            CellKey centerCell = GetCell(desiredCenter);
            int cellRange = Mathf.Max(1, Mathf.CeilToInt((moverRadius + maxActiveRadius) / GridCellSize));

            for (int z = -cellRange; z <= cellRange; z++)
            {
                for (int x = -cellRange; x <= cellRange; x++)
                {
                    CellKey key = new CellKey(centerCell.X + x, centerCell.Z + z);
                    if (!Grid.TryGetValue(key, out List<EnemyCrowdBlocker> blockers))
                    {
                        continue;
                    }

                    for (int i = 0; i < blockers.Count; i++)
                    {
                        EnemyCrowdBlocker other = blockers[i];
                        if (other == null || other == mover || !other.IsBlockingEnabled)
                        {
                            continue;
                        }

                        result = ResolveAgainstBlocker(mover, other, currentPosition, result, moverRadius);
                    }
                }
            }

            Vector3 correction = result - originalDesired;
            correction.y = 0.0f;

            if (correction.sqrMagnitude <= 0.0001f)
            {
                QueueSidePushIfMovementBlocked(mover, currentPosition, requestedDesired, originalDesired, moverRadius);
                QueuePressureIfPendingPushBlocked(mover, currentPosition, consumedPush, originalDesired, moverRadius);
                return originalDesired;
            }

            correction *= mover.pushStrength;

            float maxCorrection = Mathf.Max(0.0f, mover.maxCorrectionPerFrame);
            if (maxCorrection > 0.0f && correction.sqrMagnitude > maxCorrection * maxCorrection)
            {
                correction = correction.normalized * maxCorrection;
            }

            Vector3 resolved = originalDesired + correction;
            resolved.y = desiredPosition.y;
            QueueSidePushIfMovementBlocked(mover, currentPosition, requestedDesired, resolved, moverRadius);
            QueuePressureIfPendingPushBlocked(mover, currentPosition, consumedPush, resolved, moverRadius);
            return resolved;
        }

        private static Vector3 ResolveAgainstBlocker(EnemyCrowdBlocker mover, EnemyCrowdBlocker blocker, Vector3 currentPosition, Vector3 desiredPosition, float moverRadius)
        {
            Vector3 blockerCenter = blocker.GetCenterPosition();
            blockerCenter.y = desiredPosition.y;

            Vector3 currentCenter = mover.GetCenterForPosition(currentPosition);
            currentCenter.y = desiredPosition.y;

            Vector3 desiredCenter = mover.GetCenterForPosition(desiredPosition);
            desiredCenter.y = desiredPosition.y;

            float minDistance = moverRadius + blocker.BlockRadius;
            Vector3 closest = GetClosestPointOnMove(currentCenter, desiredCenter, blockerCenter);
            Vector3 offset = closest - blockerCenter;
            offset.y = 0.0f;

            if (offset.sqrMagnitude >= minDistance * minDistance)
            {
                return desiredPosition;
            }

            Vector3 normal = GetPushNormal(mover, blocker, offset, currentCenter - blockerCenter);
            Vector3 resolvedCenter = blockerCenter + normal * minDistance;
            Vector3 resolved = resolvedCenter - mover.GetWorldCenterOffset();
            resolved.y = desiredPosition.y;

            return resolved;
        }

        private static void QueueSidePushIfMovementBlocked(EnemyCrowdBlocker mover, Vector3 currentPosition, Vector3 requestedDesired, Vector3 resolvedPosition, float moverRadius)
        {
            if (!mover.isCrowdMoving || mover.blockedSidePushPerFrame <= 0.0f)
            {
                return;
            }

            Vector3 intendedMove = requestedDesired - currentPosition;
            intendedMove.y = 0.0f;
            float intendedDistance = intendedMove.magnitude;
            if (intendedDistance <= 0.0001f)
            {
                return;
            }

            Vector3 moveDirection = intendedMove / intendedDistance;
            Vector3 actualMove = resolvedPosition - currentPosition;
            actualMove.y = 0.0f;

            float forwardProgress = Vector3.Dot(actualMove, moveDirection);
            if (forwardProgress > intendedDistance * 0.35f)
            {
                return;
            }

            float blockedPressure = Mathf.Clamp01((intendedDistance - Mathf.Max(0.0f, forwardProgress)) / intendedDistance);
            Vector3 moverCenter = mover.GetCenterForPosition(currentPosition);
            moverCenter.y = currentPosition.y;
            Vector3 sideAxis = new Vector3(-moveDirection.z, 0.0f, moveDirection.x);
            float searchDistance = moverRadius + maxActiveRadius + Mathf.Max(0.35f, mover.maxPendingPushPerFrame * 2.0f);
            float sideLimit = moverRadius + maxActiveRadius + 0.25f;
            CellKey centerCell = GetCell(moverCenter);
            int cellRange = Mathf.Max(1, Mathf.CeilToInt(searchDistance / GridCellSize));

            for (int z = -cellRange; z <= cellRange; z++)
            {
                for (int x = -cellRange; x <= cellRange; x++)
                {
                    CellKey key = new CellKey(centerCell.X + x, centerCell.Z + z);
                    if (!Grid.TryGetValue(key, out List<EnemyCrowdBlocker> blockers))
                    {
                        continue;
                    }

                    for (int i = 0; i < blockers.Count; i++)
                    {
                        EnemyCrowdBlocker blocker = blockers[i];
                        if (blocker == null || blocker == mover || !blocker.IsBlockingEnabled)
                        {
                            continue;
                        }

                        Vector3 blockerCenter = blocker.GetCenterPosition();
                        blockerCenter.y = moverCenter.y;
                        Vector3 toBlocker = blockerCenter - moverCenter;
                        toBlocker.y = 0.0f;

                        float forwardDistance = Vector3.Dot(toBlocker, moveDirection);
                        if (forwardDistance <= 0.0f || forwardDistance > searchDistance)
                        {
                            continue;
                        }

                        float sideDistance = Vector3.Dot(toBlocker, sideAxis);
                        if (Mathf.Abs(sideDistance) > sideLimit)
                        {
                            continue;
                        }

                        Vector3 sideDirection = Mathf.Abs(sideDistance) > 0.01f
                            ? sideAxis * Mathf.Sign(sideDistance)
                            : GetStableSignedDirection(mover, blocker, sideAxis);

                        float distanceFactor = 1.0f - Mathf.Clamp01(forwardDistance / searchDistance);
                        float pushMagnitude = mover.blockedSidePushPerFrame * Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(mover.sideSpreadStrength)) * Mathf.Max(0.35f, distanceFactor) * blockedPressure;
                        blocker.QueuePendingPush(sideDirection * pushMagnitude, true);
                    }
                }
            }
        }

        private static void QueuePressureIfPendingPushBlocked(EnemyCrowdBlocker mover, Vector3 currentPosition, Vector3 consumedPush, Vector3 resolvedPosition, float moverRadius)
        {
            consumedPush.y = 0.0f;
            float intendedDistance = consumedPush.magnitude;
            if (intendedDistance <= 0.0001f || mover.pressureTransferRatio <= 0.0f)
            {
                return;
            }

            Vector3 pressureDirection = consumedPush / intendedDistance;
            Vector3 actualMove = resolvedPosition - currentPosition;
            actualMove.y = 0.0f;

            float progress = Vector3.Dot(actualMove, pressureDirection);
            if (progress > intendedDistance * 0.45f)
            {
                return;
            }

            float blockedPressure = Mathf.Clamp01((intendedDistance - Mathf.Max(0.0f, progress)) / intendedDistance);
            float transferredMagnitude = intendedDistance * mover.pressureTransferRatio * blockedPressure;
            if (transferredMagnitude <= 0.0001f)
            {
                return;
            }

            QueueDirectionalPressure(mover, currentPosition, pressureDirection, moverRadius, transferredMagnitude);
        }

        private static void QueueDirectionalPressure(EnemyCrowdBlocker mover, Vector3 currentPosition, Vector3 pressureDirection, float moverRadius, float pushMagnitude)
        {
            Vector3 moverCenter = mover.GetCenterForPosition(currentPosition);
            moverCenter.y = currentPosition.y;

            float searchDistance = moverRadius + maxActiveRadius + Mathf.Max(0.25f, mover.maxPendingPushPerFrame);
            float sideLimit = moverRadius + maxActiveRadius + 0.2f;
            Vector3 sideAxis = new Vector3(-pressureDirection.z, 0.0f, pressureDirection.x);
            CellKey centerCell = GetCell(moverCenter);
            int cellRange = Mathf.Max(1, Mathf.CeilToInt(searchDistance / GridCellSize));

            for (int z = -cellRange; z <= cellRange; z++)
            {
                for (int x = -cellRange; x <= cellRange; x++)
                {
                    CellKey key = new CellKey(centerCell.X + x, centerCell.Z + z);
                    if (!Grid.TryGetValue(key, out List<EnemyCrowdBlocker> blockers))
                    {
                        continue;
                    }

                    for (int i = 0; i < blockers.Count; i++)
                    {
                        EnemyCrowdBlocker blocker = blockers[i];
                        if (blocker == null || blocker == mover || !blocker.IsBlockingEnabled)
                        {
                            continue;
                        }

                        Vector3 blockerCenter = blocker.GetCenterPosition();
                        blockerCenter.y = moverCenter.y;
                        Vector3 toBlocker = blockerCenter - moverCenter;
                        toBlocker.y = 0.0f;

                        float forwardDistance = Vector3.Dot(toBlocker, pressureDirection);
                        if (forwardDistance <= 0.0f || forwardDistance > searchDistance)
                        {
                            continue;
                        }

                        float sideDistance = Mathf.Abs(Vector3.Dot(toBlocker, sideAxis));
                        if (sideDistance > sideLimit)
                        {
                            continue;
                        }

                        float distanceFactor = 1.0f - Mathf.Clamp01(forwardDistance / searchDistance);
                        blocker.QueuePendingPush(pressureDirection * pushMagnitude * Mathf.Max(0.35f, distanceFactor), true);
                    }
                }
            }
        }

        private Vector3 ConsumePendingPush(Vector3 desiredPosition, out Vector3 consumedPush)
        {
            if (pendingPush.sqrMagnitude <= 0.0001f && smoothedPush.sqrMagnitude <= 0.000001f)
            {
                consumedPush = Vector3.zero;
                return desiredPosition;
            }

            Vector3 push = pendingPush;
            pendingPush = Vector3.zero;
            push.y = 0.0f;

            float maxPush = Mathf.Max(0.0f, maxPendingPushPerFrame);
            if (maxPush > 0.0f && push.sqrMagnitude > maxPush * maxPush)
            {
                push = push.normalized * maxPush;
            }

            float smoothingSpeed = push.sqrMagnitude > 0.0001f ? pushSmoothingSpeed : pushDecaySpeed;
            float smoothing = smoothingSpeed > 0.0f ? 1.0f - Mathf.Exp(-smoothingSpeed * Time.deltaTime) : 1.0f;
            smoothedPush = Vector3.Lerp(smoothedPush, push, smoothing);
            smoothedPush.y = 0.0f;

            if (smoothedPush.sqrMagnitude <= 0.000001f)
            {
                smoothedPush = Vector3.zero;
                consumedPush = Vector3.zero;
                return desiredPosition;
            }

            Vector3 resolved = desiredPosition + smoothedPush;
            resolved.y = desiredPosition.y;
            consumedPush = smoothedPush;
            return resolved;
        }

        private void QueuePendingPush(Vector3 push, bool allowMoving = false)
        {
            if (!IsBlockingEnabled || (!allowMoving && isCrowdMoving))
            {
                return;
            }

            push.y = 0.0f;
            if (push.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            pendingPush += push;
            pendingPush.y = 0.0f;

            float maxStoredPush = Mathf.Max(0.0f, maxPendingPushPerFrame) * 2.0f;
            if (maxStoredPush > 0.0f && pendingPush.sqrMagnitude > maxStoredPush * maxStoredPush)
            {
                pendingPush = pendingPush.normalized * maxStoredPush;
            }
        }

        private static Vector3 GetClosestPointOnMove(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 move = end - start;
            move.y = 0.0f;
            if (move.sqrMagnitude <= 0.0001f)
            {
                return end;
            }

            Vector3 offset = point - start;
            offset.y = 0.0f;
            float t = Mathf.Clamp01(Vector3.Dot(offset, move) / move.sqrMagnitude);
            return start + move * t;
        }

        private static Vector3 GetStableSignedDirection(EnemyCrowdBlocker mover, EnemyCrowdBlocker blocker, Vector3 axis)
        {
            axis.y = 0.0f;
            if (axis.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            axis.Normalize();
            unchecked
            {
                int hash = mover.GetStableId() * 83492791 ^ blocker.GetStableId() * 297121507;
                return (hash & 1) == 0 ? axis : -axis;
            }
        }

        private static Vector3 GetPushNormal(EnemyCrowdBlocker mover, EnemyCrowdBlocker blocker, Vector3 primary, Vector3 fallback)
        {
            primary.y = 0.0f;
            if (primary.sqrMagnitude > 0.0001f)
            {
                return primary.normalized;
            }

            fallback.y = 0.0f;
            if (fallback.sqrMagnitude > 0.0001f)
            {
                return fallback.normalized;
            }

            float angle = GetStableAngle(mover, blocker) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
        }

        private static float GetStableAngle(EnemyCrowdBlocker mover, EnemyCrowdBlocker blocker)
        {
            int moverId = mover.GetStableId();
            int blockerId = blocker.GetStableId();
            unchecked
            {
                int hash = moverId * 73856093 ^ blockerId * 19349663;
                return Mathf.Abs(hash % 360);
            }
        }

        private int GetStableId()
        {
            return controller != null ? controller.EnemyId : GetInstanceID();
        }

        private Vector3 GetCenterPosition()
        {
            return transform.position + GetWorldCenterOffset();
        }

        private Vector3 GetCenterForPosition(Vector3 rootPosition)
        {
            return rootPosition + GetWorldCenterOffset();
        }

        private Vector3 GetWorldCenterOffset()
        {
            if (centerTransform != null)
            {
                return centerTransform.position - transform.position;
            }

            return transform.TransformVector(centerOffset);
        }

        private static void RebuildGridIfNeeded()
        {
            if (builtGridFrame == Time.frameCount)
            {
                return;
            }

            CleanupActiveList();
            ClearGrid();
            maxActiveRadius = 0.05f;

            for (int i = 0; i < ActiveBlockers.Count; i++)
            {
                EnemyCrowdBlocker blocker = ActiveBlockers[i];
                if (blocker == null || !blocker.IsBlockingEnabled)
                {
                    continue;
                }

                AddToGrid(blocker);
                maxActiveRadius = Mathf.Max(maxActiveRadius, blocker.BlockRadius);
            }

            builtGridFrame = Time.frameCount;
        }

        private static void AddToGrid(EnemyCrowdBlocker blocker)
        {
            CellKey key = GetCell(blocker.GetCenterPosition());
            if (!Grid.TryGetValue(key, out List<EnemyCrowdBlocker> blockers))
            {
                blockers = new List<EnemyCrowdBlocker>(8);
                Grid.Add(key, blockers);
            }

            if (blockers.Count == 0)
            {
                UsedCells.Add(key);
            }

            blockers.Add(blocker);
        }

        private static void ClearGrid()
        {
            for (int i = 0; i < UsedCells.Count; i++)
            {
                if (Grid.TryGetValue(UsedCells[i], out List<EnemyCrowdBlocker> blockers))
                {
                    blockers.Clear();
                }
            }

            UsedCells.Clear();
        }

        private static CellKey GetCell(Vector3 position)
        {
            return new CellKey(
                Mathf.FloorToInt(position.x / GridCellSize),
                Mathf.FloorToInt(position.z / GridCellSize));
        }

        private static void CleanupActiveList()
        {
            ActiveBlockers.RemoveAll(blocker => blocker == null);
        }

        private static void InvalidateGrid()
        {
            builtGridFrame = -1;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
            {
                return;
            }

            Gizmos.color = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            Gizmos.DrawWireSphere(GetCenterPosition(), Mathf.Max(0.05f, blockRadius));
        }

        private readonly struct CellKey : System.IEquatable<CellKey>
        {
            public readonly int X;
            public readonly int Z;

            public CellKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(CellKey other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is CellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }
    }
}
