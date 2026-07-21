using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SupportSegmentAbility : SegmentWeaponBehaviour
    {
        private const float MinimumPickupMagnetPullStrength = 225f;
        private const float MinimumPickupMagnetMaxPullSpeed = 110f;
        private const float HolyWaterTargetRetrySeconds = 0.12f;
        private const float FreezeAreaTargetRetrySeconds = 0.2f;
        private const float WormholeTargetRetrySeconds = 0.2f;

        public SegmentSupportAbilityProfile Profile;
        public Transform ActiveVfxRoot;
        public Transform RangeVfxRoot;
        public Transform MuzzleVfxRoot;
        public Transform TargetBodyVfxSocket;

        [Header("Temporary VFX")]
        public bool UseTemporarySupportVfx = true;

        [Header("Active Head Spin")]
        public Transform ActiveHeadRotationRoot;
        [Min(0f)] public float ActiveHeadSpinSpeed = 180f;

        [Header("Reward Pickup Magnet")]
        [Min(0f)] public float PickupMagnetPullStrength = MinimumPickupMagnetPullStrength;
        [Min(0.1f)] public float PickupMagnetMaxPullSpeed = MinimumPickupMagnetMaxPullSpeed;
        [Min(0.05f)] public float PickupMagnetCollectDistance = 0.65f;

        [Header("Freeze Area")]
        [Min(0.1f)] public float FreezeAreaRadius = 4.5f;
        [Min(0.1f)] public float FreezeAreaClusterRadius = 4.5f;
        [Min(1)] public int FreezeAreaClusterMinEnemyCount = 3;
        [Min(0.05f)] public float FreezeAreaVfxLifetime = 5f;
        [Range(0f, 1f)] public float FreezeAreaVfxAlpha = 1f;
        public bool ShowFreezeAreaRadiusVfx = true;

        [Header("Holy Water Cone Area")]
        [Range(1f, 180f)] public float HolyWaterSprayAngle = 36f;
        [Min(1)] public int HolyWaterProjectileCount = 1;
        [Min(0.02f)] public float HolyWaterProjectileInterval = 0.08f;
        [Min(0.1f)] public float HolyWaterProjectileSpeed = 9f;
        [Min(0.05f)] public float HolyWaterProjectileLifetime = 4f;
        [Min(0f)] public float HolyWaterProjectileStartRadius = 0f;
        [Min(0.05f)] public float HolyWaterProjectileEndRadius = 1.75f;
        [Range(0f, 1f)] public float HolyWaterMuzzleInfluenceStrength = 0.3f;
        [Min(0f)] public float HolyWaterConeLength = 0f;
        [Min(0f)] public float HolyWaterAimTurnSpeed = 720f;
        [Range(0f, 45f)] public float HolyWaterFireAngleTolerance = 5f;
        [Min(0.05f)] public float HolyWaterDebuffTickInterval = 0.5f;
        [Min(0f)] public float HolyWaterTargetAimHeight = 0.6f;
        public Color HolyWaterProjectileColor = new Color(0.86f, 0.96f, 1f, 0.34f);

        private float cooldownTimer;
        private float activeTimer;
        private Vector3 freezeAreaCenter;
        private EnemyController holyWaterTarget;
        private EnemyController wormholeTarget;
        private bool hasFreezeAreaCenter;
        private bool freezeAreaApplied;
        private bool holyWaterShotFired;
        private bool wormholeShotFired;
        private float activeHeadSpinAngle;
        private Quaternion activeHeadBaseLocalRotation;
        private bool hasActiveHeadBaseRotation;
        private bool isActive;
        private readonly List<EnemyController> activeEnemyBuffer = new List<EnemyController>(32);

        public bool IsAbilityActive => isActive;

        public override void Configure(ConvoySegmentRuntime segment)
        {
            bool segmentChanged = Segment != segment;
            base.Configure(segment);
            CacheActiveHeadRotationRoot();
            if (segmentChanged)
            {
                ResetCycle();
            }
        }

        public override void SetWeaponActive(bool active)
        {
            base.SetWeaponActive(active);
            if (!active)
            {
                isActive = false;
                activeTimer = 0f;
                ClearFreezeAreaState();
                ClearHolyWaterState();
                ClearWormholeState();
                activeHeadSpinAngle = 0f;
                RestoreActiveHeadRotation();
                SupportSegmentRuntimeBuffs.ClearSource(this);
                SetVfxRootsActive(false);
            }
        }

        public override void TickWeapon(float deltaTime)
        {
            if (!IsWeaponActive || Profile == null)
            {
                return;
            }

            if (isActive)
            {
                TickActiveSupportEffect(deltaTime);
                if (!isActive)
                {
                    return;
                }

                TickActiveHeadSpin(deltaTime);
                activeTimer -= deltaTime;
                if (activeTimer <= 0f)
                {
                    EndActivation();
                }

                return;
            }

            cooldownTimer -= deltaTime;
            if (cooldownTimer <= 0f)
            {
                TryBeginActivation();
            }
        }

        private void TickActiveSupportEffect(float deltaTime)
        {
            if (Profile == null)
            {
                return;
            }

            switch (Profile.AbilityKind)
            {
                case SegmentSupportAbilityKind.FinalDamageBuff:
                case SegmentSupportAbilityKind.FinalAttackSpeedBuff:
                    SupportSegmentRuntimeBuffs.RefreshAllyBuff(this, Profile);
                    break;
                case SegmentSupportAbilityKind.PickupMagnet:
                    ApplyPickupMagnet(deltaTime);
                    break;
                case SegmentSupportAbilityKind.FreezeArea:
                    ApplyFreezeArea();
                    break;
                case SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray:
                    ApplyHolyWaterSpray(deltaTime);
                    break;
                case SegmentSupportAbilityKind.WormholePortal:
                    ApplyWormholePortal(deltaTime);
                    break;
            }

            if (isActive)
            {
                RefreshTemporarySupportVfx();
            }
        }

        private void ResetCycle()
        {
            isActive = false;
            activeTimer = 0f;
            ClearFreezeAreaState();
            ClearHolyWaterState();
            ClearWormholeState();
            activeHeadSpinAngle = 0f;
            RestoreActiveHeadRotation();
            SupportSegmentRuntimeBuffs.ClearSource(this);
            cooldownTimer = Profile != null && Profile.StartsReady ? 0f : GetCooldown();
            SetVfxRootsActive(false);
        }

        private void TryBeginActivation()
        {
            if (IsHolyWaterProfile() && !TryAcquireHolyWaterTarget(out holyWaterTarget))
            {
                cooldownTimer = HolyWaterTargetRetrySeconds;
                SetVfxRootsActive(false);
                return;
            }

            if (IsWormholeProfile() && !TryAcquireWormholeTarget(out wormholeTarget))
            {
                cooldownTimer = WormholeTargetRetrySeconds;
                SetVfxRootsActive(false);
                return;
            }

            if (IsFreezeAreaProfile() && !TryAcquireFreezeAreaCenter(out freezeAreaCenter))
            {
                cooldownTimer = FreezeAreaTargetRetrySeconds;
                SetVfxRootsActive(false);
                return;
            }

            hasFreezeAreaCenter = IsFreezeAreaProfile();
            BeginActivation();
        }

        private void BeginActivation()
        {
            isActive = true;
            activeTimer = GetActiveDurationSeconds();
            freezeAreaApplied = false;
            holyWaterShotFired = false;
            wormholeShotFired = false;
            activeHeadSpinAngle = 0f;
            CacheActiveHeadRotationRoot();
            SetVfxRootsActive(!IsHolyWaterProfile() && !IsFreezeAreaProfile() && !IsWormholeProfile());
            GameplaySfxEmitter.TryPlay(MuzzleVfxRoot != null ? MuzzleVfxRoot : transform, GameplaySfxCue.Activation);
            TickActiveSupportEffect(0f);
        }

        private void EndActivation()
        {
            isActive = false;
            activeTimer = 0f;
            ClearFreezeAreaState();
            ClearHolyWaterState();
            ClearWormholeState();
            activeHeadSpinAngle = 0f;
            RestoreActiveHeadRotation();
            SupportSegmentRuntimeBuffs.ClearSource(this);
            cooldownTimer = GetCooldown();
            SetVfxRootsActive(false);
        }

        private void OnDisable()
        {
            SupportSegmentRuntimeBuffs.ClearSource(this);
        }

        private float GetCooldown()
        {
            return Profile != null ? GetRandomizedCooldown(Profile.Cooldown) : 0f;
        }

        private float GetActiveDurationSeconds()
        {
            if (Profile == null)
            {
                return 0.05f;
            }

            float duration = Mathf.Max(0.05f, Profile.ActiveDurationSeconds);
            return duration;
        }

        private float GetEffectivePickupMagnetPullStrength()
        {
            return Mathf.Max(PickupMagnetPullStrength, MinimumPickupMagnetPullStrength);
        }

        private float GetEffectivePickupMagnetMaxPullSpeed()
        {
            return Mathf.Max(PickupMagnetMaxPullSpeed, MinimumPickupMagnetMaxPullSpeed);
        }

        private void ApplyPickupMagnet(float deltaTime)
        {
            WorldRewardPickup.AttractInRange(
                transform.position,
                Profile.Range,
                GetEffectivePickupMagnetPullStrength(),
                GetEffectivePickupMagnetMaxPullSpeed(),
                PickupMagnetCollectDistance,
                deltaTime);
        }

        private void ApplyFreezeArea()
        {
            if (freezeAreaApplied)
            {
                return; // 장판 1회 적용
            }

            if (!hasFreezeAreaCenter && !TryAcquireFreezeAreaCenter(out freezeAreaCenter))
            {
                EndActivation();
                return; // 대상 없음
            }

            freezeAreaApplied = true;
            hasFreezeAreaCenter = true;
            Vector3 center = GroundService.ProjectToGround(freezeAreaCenter, 0f);
            float radius = Mathf.Max(0.1f, FreezeAreaRadius);
            EnemyController.CollectActiveInRange(center, radius, activeEnemyBuffer, SegmentTargetQuery.IsEnemyUsable);

            float duration = GetEffectDurationSeconds();
            for (int i = 0; i < activeEnemyBuffer.Count; i++)
            {
                EnemySupportDebuffState state = EnemySupportDebuffState.GetOrAdd(activeEnemyBuffer[i]);
                if (state != null)
                {
                    state.ApplyFreeze(duration);
                    if (Profile.EnemyStatusEffect != CombatStatusEffectKind.None)
                    {
                        state.ApplyStatusEffect(
                            Profile.EnemyStatusEffect,
                            Segment != null ? Segment.ChainIndex : -1,
                            gameObject,
                            activeEnemyBuffer[i].transform.position,
                            Profile.EnemyDebuffVfxPrefab,
                            duration);
                    }
                }
            }

            PlayFreezeAreaVfx(center, radius);
            EndActivation();
        }

        private bool TryAcquireFreezeAreaCenter(out Vector3 center)
        {
            center = transform.position;
            if (Profile == null)
            {
                return false; // 프로필 없음
            }

            float searchRange = Mathf.Max(0.1f, Profile.Range);
            bool found = SegmentTargetQuery.TryPickDensestClusterOrRandomTarget(
                transform.position,
                searchRange,
                FreezeAreaClusterRadius,
                FreezeAreaClusterMinEnemyCount,
                SegmentTargetQuery.IsEnemyUsable,
                0.5f,
                out _,
                out Vector3 impactPoint);

            if (!found)
            {
                return false; // 사거리 내 대상 없음
            }

            center = GroundService.ProjectToGround(impactPoint, 0f);
            return true;
        }

        private void PlayFreezeAreaVfx(Vector3 center, float radius)
        {
            GameObject prefab = Profile != null ? Profile.RangeVfxPrefab : null;
            float lifetime = Mathf.Max(0.05f, FreezeAreaVfxLifetime);
            SegmentAttackVfxPlayer.PlayExplosion(prefab, center, radius, lifetime, FreezeAreaVfxAlpha);
            if (ShowFreezeAreaRadiusVfx)
            {
                SupportTemporaryVfx.ShowWorldArea(center, SegmentSupportAbilityKind.FreezeArea, radius, lifetime); // 실제 동결 반경 표시
            }
        }

        private void ApplyHolyWaterSpray(float deltaTime)
        {
            if (holyWaterShotFired)
            {
                return;
            }

            if (!SegmentTargetQuery.IsEnemyUsable(holyWaterTarget) && !TryAcquireHolyWaterTarget(out holyWaterTarget))
            {
                EndActivation();
                return;
            }

            Transform sprayRoot = MuzzleVfxRoot != null ? MuzzleVfxRoot : transform;
            Vector3 origin = sprayRoot.position;
            Vector3 targetPosition = SegmentTargetQuery.GetEnemyHitPosition(holyWaterTarget, holyWaterTarget.transform.position, HolyWaterTargetAimHeight);
            Vector3 targetDirection = targetPosition - origin;
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            targetDirection.Normalize();
            Transform aimRoot = ResolveHolyWaterAimRoot();
            if (!AimHolyWaterAtDirection(aimRoot, sprayRoot, targetDirection, deltaTime))
            {
                return;
            }

            FireHolyWaterCone(origin, targetDirection, aimRoot);
        }

        private void FireHolyWaterCone(Vector3 origin, Vector3 direction, Transform aimRoot)
        {
            Transform projectileRoot = Segment != null && Segment.Owner != null ? Segment.Owner.GetProjectileRoot() : null;
            float coneLength = GetHolyWaterConeLength();
            float lifetime = GetActiveDurationSeconds();
            SetVfxRootsActive(true);

            SupportHolyWaterProjectileRuntime.SpawnCone(
                projectileRoot,
                MuzzleVfxRoot != null ? MuzzleVfxRoot : transform,
                aimRoot,
                origin,
                direction,
                coneLength,
                HolyWaterSprayAngle,
                lifetime,
                Profile != null ? Profile.IncomingDamageMultiplier : 1f,
                GetEffectDurationSeconds(),
                HolyWaterDebuffTickInterval,
                HolyWaterProjectileColor,
                Segment != null ? Segment.ChainIndex : -1,
                gameObject,
                Profile != null ? Profile.EnemyStatusEffect : CombatStatusEffectKind.None,
                Profile != null ? Profile.EnemyDebuffVfxPrefab : null);

            holyWaterShotFired = true;
        }

        private bool TryAcquireHolyWaterTarget(out EnemyController target)
        {
            Transform sprayRoot = MuzzleVfxRoot != null ? MuzzleVfxRoot : transform;
            float range = Mathf.Max(0.1f, GetHolyWaterConeLength());
            return EnemyController.TryFindNearest(sprayRoot.position, range, SegmentTargetQuery.IsEnemyUsable, out target);
        }

        private void ApplyWormholePortal(float deltaTime)
        {
            if (wormholeShotFired)
            {
                return;
            }

            if (!SegmentTargetQuery.IsEnemyUsable(wormholeTarget) && !TryAcquireWormholeTarget(out wormholeTarget))
            {
                EndActivation();
                return;
            }

            Transform muzzle = MuzzleVfxRoot != null ? MuzzleVfxRoot : transform;
            Vector3 origin = muzzle.position;
            Vector3 targetPosition = SegmentTargetQuery.GetEnemyHitPosition(wormholeTarget, wormholeTarget.transform.position, Profile.WormholeTargetAimHeight);
            Vector3 targetDirection = targetPosition - origin;
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            targetDirection.Normalize();
            Transform aimRoot = ResolveWormholeAimRoot();
            if (!AimWormholeAtDirection(aimRoot, muzzle, targetDirection, deltaTime))
            {
                return;
            }

            FireWormholeProjectile(origin, targetDirection);
        }

        private void FireWormholeProjectile(Vector3 origin, Vector3 direction)
        {
            Transform projectileRoot = Segment != null && Segment.Owner != null ? Segment.Owner.GetProjectileRoot() : null;
            SupportWormholeProjectileRuntime.Spawn(
                projectileRoot,
                Profile != null ? Profile.WormholeProjectilePrefab : null,
                origin,
                direction,
                wormholeTarget,
                ResolveNexusPosition(),
                Profile);

            wormholeShotFired = true;
            EndActivation();
        }

        private bool TryAcquireWormholeTarget(out EnemyController target)
        {
            target = null;
            if (Profile == null)
            {
                return false;
            }

            float searchRange = Mathf.Max(0.1f, Profile.Range);
            return SegmentTargetQuery.TryPickNearestToPointTarget(
                transform.position,
                searchRange,
                ResolveNexusPosition(),
                IsWormholeTeleportCandidate,
                Profile.WormholeTargetAimHeight,
                out target);
        }

        private bool IsWormholeTeleportCandidate(EnemyController enemy)
        {
            if (!SegmentTargetQuery.IsEnemyUsable(enemy))
            {
                return false;
            }

            return Profile != null && (Profile.WormholeAffectBosses || enemy.Grade != EnemyGrade.Boss);
        }

        private bool AimWormholeAtDirection(Transform aimRoot, Transform muzzle, Vector3 targetDirection, float deltaTime)
        {
            if (aimRoot == null)
            {
                return true;
            }

            Vector3 currentDirection = GetWormholeCurrentMuzzleDirection(aimRoot, muzzle);
            if (currentDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            currentDirection.Normalize();
            float signedAngle = Vector3.SignedAngle(currentDirection, targetDirection, Vector3.up);
            float maxStep = Profile != null && Profile.WormholeAimTurnSpeed > 0f
                ? Profile.WormholeAimTurnSpeed * Mathf.Max(0f, deltaTime)
                : Mathf.Abs(signedAngle);
            float step = Mathf.Clamp(signedAngle, -maxStep, maxStep);
            aimRoot.Rotate(Vector3.up, step, Space.World);

            currentDirection = GetWormholeCurrentMuzzleDirection(aimRoot, muzzle);
            if (currentDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            currentDirection.Normalize();
            float tolerance = Profile != null ? Profile.WormholeFireAngleTolerance : 0f;
            return Mathf.Abs(Vector3.SignedAngle(currentDirection, targetDirection, Vector3.up)) <= tolerance;
        }

        private Vector3 GetWormholeCurrentMuzzleDirection(Transform aimRoot, Transform muzzle)
        {
            if (muzzle != null)
            {
                Vector3 pivotToMuzzle = muzzle.position - aimRoot.position;
                pivotToMuzzle.y = 0f;
                if (pivotToMuzzle.sqrMagnitude > 0.0001f)
                {
                    return pivotToMuzzle;
                }

                Vector3 muzzleForward = muzzle.forward;
                muzzleForward.y = 0f;
                if (muzzleForward.sqrMagnitude > 0.0001f)
                {
                    return muzzleForward;
                }
            }

            Vector3 rootForward = aimRoot.forward;
            rootForward.y = 0f;
            return rootForward;
        }

        private Transform ResolveWormholeAimRoot()
        {
            Transform yawPivot = FindChildRecursive(transform, "YawPivot");
            if (yawPivot != null)
            {
                return yawPivot;
            }

            CacheActiveHeadRotationRoot();
            return ActiveHeadRotationRoot != null ? ActiveHeadRotationRoot : transform;
        }

        private Vector3 ResolveNexusPosition()
        {
            if (NexusController.Active != null)
            {
                return NexusController.Active.transform.position;
            }

            GameObject nexusObject = GameObject.Find("Nexus_Core");
            if (nexusObject != null)
            {
                return nexusObject.transform.position;
            }

            return Vector3.zero;
        }

        private bool AimHolyWaterAtDirection(Transform aimRoot, Transform muzzle, Vector3 targetDirection, float deltaTime)
        {
            if (aimRoot == null)
            {
                return true;
            }

            Vector3 currentDirection = GetHolyWaterCurrentMuzzleDirection(aimRoot, muzzle);
            if (currentDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            currentDirection.Normalize();
            float signedAngle = Vector3.SignedAngle(currentDirection, targetDirection, Vector3.up);
            float maxStep = HolyWaterAimTurnSpeed <= 0f
                ? Mathf.Abs(signedAngle)
                : HolyWaterAimTurnSpeed * Mathf.Max(0f, deltaTime);
            float step = Mathf.Clamp(signedAngle, -maxStep, maxStep);
            aimRoot.Rotate(Vector3.up, step, Space.World);

            currentDirection = GetHolyWaterCurrentMuzzleDirection(aimRoot, muzzle);
            if (currentDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            currentDirection.Normalize();
            return Mathf.Abs(Vector3.SignedAngle(currentDirection, targetDirection, Vector3.up)) <= HolyWaterFireAngleTolerance;
        }

        private Vector3 GetHolyWaterCurrentMuzzleDirection(Transform aimRoot, Transform muzzle)
        {
            if (muzzle != null)
            {
                Vector3 pivotToMuzzle = muzzle.position - aimRoot.position;
                pivotToMuzzle.y = 0f;
                if (pivotToMuzzle.sqrMagnitude > 0.0001f)
                {
                    return pivotToMuzzle;
                }

                Vector3 muzzleForward = muzzle.forward;
                muzzleForward.y = 0f;
                if (muzzleForward.sqrMagnitude > 0.0001f)
                {
                    return muzzleForward;
                }
            }

            Vector3 rootForward = aimRoot.forward;
            rootForward.y = 0f;
            return rootForward;
        }

        private Transform ResolveHolyWaterAimRoot()
        {
            Transform yawPivot = FindChildRecursive(transform, "YawPivot");
            if (yawPivot != null)
            {
                return yawPivot;
            }

            CacheActiveHeadRotationRoot();
            return ActiveHeadRotationRoot != null ? ActiveHeadRotationRoot : transform;
        }

        private float GetHolyWaterConeLength()
        {
            if (HolyWaterConeLength > 0f)
            {
                return HolyWaterConeLength;
            }

            if (Profile != null && Profile.Range > 0f)
            {
                return Profile.Range;
            }

            return Mathf.Max(0.1f, HolyWaterProjectileSpeed * HolyWaterProjectileLifetime);
        }

        private bool IsHolyWaterProfile()
        {
            return Profile != null && Profile.AbilityKind == SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray;
        }

        private bool IsWormholeProfile()
        {
            return Profile != null && Profile.AbilityKind == SegmentSupportAbilityKind.WormholePortal;
        }

        private bool IsFreezeAreaProfile()
        {
            return Profile != null && Profile.AbilityKind == SegmentSupportAbilityKind.FreezeArea;
        }

        private void ClearFreezeAreaState()
        {
            hasFreezeAreaCenter = false;
            freezeAreaApplied = false;
            freezeAreaCenter = Vector3.zero;
        }

        private void ClearHolyWaterState()
        {
            holyWaterTarget = null;
            holyWaterShotFired = false;
        }

        private void ClearWormholeState()
        {
            wormholeTarget = null;
            wormholeShotFired = false;
        }

        private void RefreshTemporarySupportVfx()
        {
            if (!UseTemporarySupportVfx || Profile == null || Segment == null)
            {
                return;
            }

            Transform sourceVfxRoot = ActiveVfxRoot != null ? ActiveVfxRoot : transform;
            SupportTemporaryVfx.ShowSource(sourceVfxRoot, Profile.AbilityKind);

            if (ShouldShowTemporaryRangeVfx())
            {
                Transform rangeRoot = RangeVfxRoot != null ? RangeVfxRoot : transform;
                SupportTemporaryVfx.ShowRange(rangeRoot, Profile.AbilityKind, Profile.Range);
            }

            if (IsAllyBuffProfile())
            {
                RefreshTemporaryAllyBuffVfx();
            }
        }

        private void RefreshTemporaryAllyBuffVfx()
        {
            if (Segment == null || Segment.Owner == null || Segment.Owner.SegmentRoot == null)
            {
                return;
            }

            Transform root = Segment.Owner.SegmentRoot;
            for (int i = 0; i < root.childCount; i++)
            {
                ConvoySegmentRuntime runtime = root.GetChild(i).GetComponent<ConvoySegmentRuntime>();
                if (runtime == null || !runtime.IsAttached)
                {
                    continue;
                }

                if (!IsSegmentInsideAllyBuffRange(runtime.ChainIndex))
                {
                    continue;
                }

                if (!SupportSegmentRuntimeBuffs.IsWinningSourceForSegment(this, runtime.ChainIndex))
                {
                    SupportBuffBodyVfxState.ClearIfSource(runtime, this);
                    continue;
                }

                Transform targetRoot = FindChildRecursive(runtime.transform, "VFX_BuffBodyRoot");
                SupportTemporaryVfx.ShowBuffTarget(targetRoot != null ? targetRoot : runtime.transform, Profile.AbilityKind);
                SupportBuffBodyVfxState.Show(runtime, this, Profile.TargetBodyVfxPrefab);
            }
        }

        private bool IsAllyBuffProfile()
        {
            return Profile != null
                && (Profile.AbilityKind == SegmentSupportAbilityKind.FinalDamageBuff
                    || Profile.AbilityKind == SegmentSupportAbilityKind.FinalAttackSpeedBuff);
        }

        private bool ShouldShowTemporaryRangeVfx()
        {
            return Profile != null
                && Profile.AbilityKind == SegmentSupportAbilityKind.PickupMagnet;
        }

        private bool IsSegmentInsideAllyBuffRange(int chainIndex)
        {
            if (Segment == null || Profile == null)
            {
                return false;
            }

            int offset = chainIndex - Segment.ChainIndex;
            if (offset == 0)
            {
                return true;
            }

            if (offset > 0)
            {
                return offset <= Mathf.Max(0, Profile.FrontSegmentCount);
            }

            return -offset <= Mathf.Max(0, Profile.BackSegmentCount);
        }

        private float GetEffectDurationSeconds()
        {
            return Profile != null ? Mathf.Max(0.05f, Profile.EffectDurationSeconds) : 0.05f;
        }

        private void TickActiveHeadSpin(float deltaTime)
        {
            if (!ShouldSpinHeadDuringActive())
            {
                return;
            }

            CacheActiveHeadRotationRoot();
            if (ActiveHeadRotationRoot == null)
            {
                return;
            }

            activeHeadSpinAngle = Mathf.Repeat(activeHeadSpinAngle + ActiveHeadSpinSpeed * deltaTime, 360f);
            ActiveHeadRotationRoot.localRotation = activeHeadBaseLocalRotation * Quaternion.Euler(0f, activeHeadSpinAngle, 0f);
        }

        private bool ShouldSpinHeadDuringActive()
        {
            if (Profile == null || Profile.AbilityKind == SegmentSupportAbilityKind.None)
            {
                return false;
            }

            return Profile.AbilityKind != SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray
                && Profile.AbilityKind != SegmentSupportAbilityKind.FreezeArea
                && Profile.AbilityKind != SegmentSupportAbilityKind.WormholePortal
                && ActiveHeadSpinSpeed > 0f;
        }

        private void CacheActiveHeadRotationRoot()
        {
            if (ActiveHeadRotationRoot == null)
            {
                ActiveHeadRotationRoot = ResolveHeadRotationRoot();
            }

            if (ActiveHeadRotationRoot != null && !hasActiveHeadBaseRotation)
            {
                activeHeadBaseLocalRotation = ActiveHeadRotationRoot.localRotation;
                hasActiveHeadBaseRotation = true;
            }
        }

        private void RestoreActiveHeadRotation()
        {
            if (ActiveHeadRotationRoot != null && hasActiveHeadBaseRotation)
            {
                ActiveHeadRotationRoot.localRotation = activeHeadBaseLocalRotation;
            }
        }

        private Transform ResolveHeadRotationRoot()
        {
            Transform directHead = transform.Find("Head");
            return directHead != null ? directHead : FindChildRecursive(transform, "Head");
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void SetVfxRootsActive(bool active)
        {
            if (ActiveVfxRoot != null)
            {
                ActiveVfxRoot.gameObject.SetActive(active);
            }

            if (RangeVfxRoot != null)
            {
                RangeVfxRoot.gameObject.SetActive(active);
            }

            if (MuzzleVfxRoot != null)
            {
                MuzzleVfxRoot.gameObject.SetActive(active);
            }
        }
    }
}
