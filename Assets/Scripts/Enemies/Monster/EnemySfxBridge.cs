using UnityEngine;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemySfxBridge : MonoBehaviour
    {
        [SerializeField] private GameplaySfxCue deathCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue jumpLandingCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue suicideExplosionCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue slowZoneCastCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue rangedAttackCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue segmentCutCastCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue segmentCutLaunchCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue obstacleSummonCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue meleeAttackCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue shieldLoopCue = GameplaySfxCue.None;
        [SerializeField] private GameplaySfxCue portalTeleportCue = GameplaySfxCue.None;

        private EnemyController controller;
        private EnemyJump jump;
        private EnemySuicideCharger suicideCharger;
        private EnemySlowZoneThrower slowZoneThrower;
        private EnemyRangedAttack rangedAttack;
        private EnemySegmentCutCaster segmentCutCaster;
        private EnemyObstacleSummoner obstacleSummoner;
        private EnemyMeleeAttack meleeAttack;
        private EnemyAreaShield areaShield;
        private EnemyPortalTotemCaster portalTotemCaster;

        private bool deathPlayed;
        private bool shieldLoopPlaying;

        public void ConfigureCues(
            GameplaySfxCue death,
            GameplaySfxCue jumpLanding,
            GameplaySfxCue suicideExplosion,
            GameplaySfxCue slowZoneCast,
            GameplaySfxCue ranged,
            GameplaySfxCue segmentCutCast,
            GameplaySfxCue segmentCutLaunch,
            GameplaySfxCue obstacleSummon,
            GameplaySfxCue melee,
            GameplaySfxCue shieldLoop,
            GameplaySfxCue portalTeleport)
        {
            deathCue = death;
            jumpLandingCue = jumpLanding;
            suicideExplosionCue = suicideExplosion;
            slowZoneCastCue = slowZoneCast;
            rangedAttackCue = ranged;
            segmentCutCastCue = segmentCutCast;
            segmentCutLaunchCue = segmentCutLaunch;
            obstacleSummonCue = obstacleSummon;
            meleeAttackCue = melee;
            shieldLoopCue = shieldLoop;
            portalTeleportCue = portalTeleport;
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopShieldLoop();
        }

        private void CacheComponents()
        {
            controller = GetComponent<EnemyController>();
            jump = GetComponent<EnemyJump>();
            suicideCharger = GetComponent<EnemySuicideCharger>();
            slowZoneThrower = GetComponent<EnemySlowZoneThrower>();
            rangedAttack = GetComponent<EnemyRangedAttack>();
            segmentCutCaster = GetComponent<EnemySegmentCutCaster>();
            obstacleSummoner = GetComponent<EnemyObstacleSummoner>();
            meleeAttack = GetComponent<EnemyMeleeAttack>();
            areaShield = GetComponent<EnemyAreaShield>();
            portalTotemCaster = GetComponent<EnemyPortalTotemCaster>();
        }

        private void Subscribe()
        {
            if (controller != null)
            {
                controller.DeathStarted += HandleDeathStarted;
            }

            if (jump != null)
            {
                jump.Landed += HandleJumpLanded;
            }

            if (suicideCharger != null)
            {
                suicideCharger.Exploded += HandleSuicideExploded;
            }

            if (slowZoneThrower != null)
            {
                slowZoneThrower.ThrowStarted += HandleSlowZoneThrowStarted;
            }

            if (rangedAttack != null)
            {
                rangedAttack.AttackPerformed += HandleRangedAttackPerformed;
            }

            if (segmentCutCaster != null)
            {
                segmentCutCaster.CastStarted += HandleSegmentCutCastStarted;
                segmentCutCaster.ProjectileLaunched += HandleSegmentCutProjectileLaunched;
            }

            if (obstacleSummoner != null)
            {
                obstacleSummoner.ObstacleSummoned += HandleObstacleSummoned;
            }

            if (meleeAttack != null)
            {
                meleeAttack.AttackPerformed += HandleMeleeAttackPerformed;
            }

            if (areaShield != null)
            {
                areaShield.ShieldStateChanged += HandleShieldStateChanged;
            }

            if (portalTotemCaster != null)
            {
                portalTotemCaster.Teleported += HandlePortalTeleported;
            }
        }

        private void Unsubscribe()
        {
            if (controller != null)
            {
                controller.DeathStarted -= HandleDeathStarted;
            }

            if (jump != null)
            {
                jump.Landed -= HandleJumpLanded;
            }

            if (suicideCharger != null)
            {
                suicideCharger.Exploded -= HandleSuicideExploded;
            }

            if (slowZoneThrower != null)
            {
                slowZoneThrower.ThrowStarted -= HandleSlowZoneThrowStarted;
            }

            if (rangedAttack != null)
            {
                rangedAttack.AttackPerformed -= HandleRangedAttackPerformed;
            }

            if (segmentCutCaster != null)
            {
                segmentCutCaster.CastStarted -= HandleSegmentCutCastStarted;
                segmentCutCaster.ProjectileLaunched -= HandleSegmentCutProjectileLaunched;
            }

            if (obstacleSummoner != null)
            {
                obstacleSummoner.ObstacleSummoned -= HandleObstacleSummoned;
            }

            if (meleeAttack != null)
            {
                meleeAttack.AttackPerformed -= HandleMeleeAttackPerformed;
            }

            if (areaShield != null)
            {
                areaShield.ShieldStateChanged -= HandleShieldStateChanged;
            }

            if (portalTotemCaster != null)
            {
                portalTotemCaster.Teleported -= HandlePortalTeleported;
            }
        }

        private void HandleDeathStarted(EnemyController changedController)
        {
            PlayDeathOnce();
        }

        private void PlayDeathOnce()
        {
            if (deathPlayed)
            {
                return;
            }

            deathPlayed = true;
            PlayAt(deathCue, transform.position, true);
        }

        private void HandleJumpLanded(Vector3 position)
        {
            PlayAt(jumpLandingCue, position, true);
        }

        private void HandleSuicideExploded()
        {
            PlayAt(suicideExplosionCue, transform.position, true);
        }

        private void HandleSlowZoneThrowStarted()
        {
            PlayAt(slowZoneCastCue, transform.position, true);
        }

        private void HandleRangedAttackPerformed()
        {
            PlayAt(rangedAttackCue, transform.position, true);
        }

        private void HandleSegmentCutCastStarted()
        {
            PlayAt(segmentCutCastCue, transform.position, true);
        }

        private void HandleSegmentCutProjectileLaunched()
        {
            PlayAt(segmentCutLaunchCue, transform.position, true);
        }

        private void HandleObstacleSummoned(Vector3 position)
        {
            PlayAt(obstacleSummonCue, position, true);
        }

        private void HandleMeleeAttackPerformed()
        {
            PlayAt(meleeAttackCue, transform.position, true);
        }

        private void HandleShieldStateChanged(EnemyAreaShield shield, bool active)
        {
            if (shieldLoopCue == GameplaySfxCue.None)
            {
                return;
            }

            if (active)
            {
                if (shieldLoopPlaying)
                {
                    return;
                }

                shieldLoopPlaying = GameplaySfxEmitter.TryStartLoop(transform, shieldLoopCue);
                if (!shieldLoopPlaying)
                {
                    PlayAt(shieldLoopCue, transform.position, true);
                }

                return;
            }

            StopShieldLoop();
        }

        private void HandlePortalTeleported(Vector3 position)
        {
            PlayAt(portalTeleportCue, position, true);
        }

        private void StopShieldLoop()
        {
            if (!shieldLoopPlaying)
            {
                return;
            }

            GameplaySfxEmitter.TryStopLoop(transform, shieldLoopCue);
            shieldLoopPlaying = false;
        }

        private void PlayAt(GameplaySfxCue cue, Vector3 position, bool detached)
        {
            if (cue == GameplaySfxCue.None)
            {
                return;
            }

            if (GameplaySfxEmitter.TryPlayAt(transform, cue, position, detached))
            {
                return;
            }

            GameplaySfxEmitter.TryPlay(transform, cue);
        }
    }
}
