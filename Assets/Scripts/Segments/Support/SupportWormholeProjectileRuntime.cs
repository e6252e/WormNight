using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SupportWormholeProjectileRuntime : MonoBehaviour
    {
        private static Material fallbackProjectileMaterial; // 임시 투사체 재질

        private readonly List<EnemyController> enemyBuffer = new List<EnemyController>(32);
        private SegmentSupportAbilityProfile profile;
        private EnemyController target;
        private Vector3 direction;
        private Vector3 lastTargetPosition;
        private Vector3 nexusPosition;
        private float lifeTimer;
        private bool detonated;

        public static SupportWormholeProjectileRuntime Spawn(
            Transform parent,
            GameObject prefab,
            Vector3 position,
            Vector3 fireDirection,
            EnemyController target,
            Vector3 nexusPosition,
            SegmentSupportAbilityProfile profile)
        {
            GameObject instance;
            if (prefab != null)
            {
                Quaternion rotation = ResolveRotation(fireDirection);
                instance = Instantiate(prefab, position, rotation, parent);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                instance.name = "SG56_WormholeProjectile_Runtime";
                instance.transform.SetParent(parent, true);
                instance.transform.position = position;
                instance.transform.localScale = Vector3.one * 0.45f;
                Destroy(instance.GetComponent<Collider>());

                Renderer renderer = instance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = GetFallbackProjectileMaterial();
                }
            }

            DisableColliders(instance);
            SupportWormholeProjectileRuntime runtime = instance.GetComponent<SupportWormholeProjectileRuntime>();
            if (runtime == null)
            {
                runtime = instance.AddComponent<SupportWormholeProjectileRuntime>();
            }

            runtime.Configure(fireDirection, target, nexusPosition, profile);
            return runtime;
        }

        private void Configure(Vector3 fireDirection, EnemyController target, Vector3 nexusPosition, SegmentSupportAbilityProfile profile)
        {
            this.profile = profile;
            this.target = target;
            this.nexusPosition = GroundService.ProjectToGround(nexusPosition, 0f);
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : transform.forward;
            lastTargetPosition = ResolveTargetPosition();
            lifeTimer = profile != null ? Mathf.Max(0.1f, profile.WormholeProjectileLifetime) : 0.1f;
            detonated = false;
        }

        private void Update()
        {
            if (detonated || profile == null)
            {
                Destroy(gameObject);
                return;
            }

            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            lastTargetPosition = ResolveTargetPosition();
            Vector3 toTarget = lastTargetPosition - transform.position;
            if (toTarget.sqrMagnitude <= GetHitRadius() * GetHitRadius())
            {
                Detonate(lastTargetPosition);
                return;
            }

            if (TryHitNearbyEnemy(out Vector3 hitPosition))
            {
                Detonate(hitPosition);
                return;
            }

            float step = Mathf.Max(0.1f, profile.WormholeProjectileSpeed) * Time.deltaTime;
            if (toTarget.sqrMagnitude <= step * step)
            {
                transform.position = lastTargetPosition;
                Detonate(lastTargetPosition);
                return;
            }

            direction = toTarget.normalized;
            transform.position += direction * step;
            transform.rotation = ResolveRotation(direction);
        }

        private Vector3 ResolveTargetPosition()
        {
            if (SegmentTargetQuery.IsEnemyUsable(target))
            {
                return SegmentTargetQuery.GetEnemyHitPosition(target, target.transform.position, profile != null ? profile.WormholeTargetAimHeight : 0.6f);
            }

            return lastTargetPosition;
        }

        private bool TryHitNearbyEnemy(out Vector3 hitPosition)
        {
            hitPosition = transform.position;
            EnemyController.CollectActiveInRange(transform.position, GetHitRadius(), enemyBuffer, IsTeleportCandidate);
            if (enemyBuffer.Count == 0)
            {
                return false;
            }

            EnemyController hitEnemy = enemyBuffer[0];
            hitPosition = SegmentTargetQuery.GetEnemyHitPosition(hitEnemy, hitEnemy.transform.position, profile.WormholeTargetAimHeight);
            return true;
        }

        private void Detonate(Vector3 position)
        {
            if (detonated)
            {
                return;
            }

            detonated = true;
            Vector3 center = GroundService.ProjectToGround(position, 0f);
            float radius = Mathf.Max(0.1f, profile.WormholeExplosionRadius);
            PlayBlackHoleVfx(center, radius, true);
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.Explosion, center, true);

            EnemyController.CollectActiveInRange(center, radius, enemyBuffer, IsTeleportCandidate);
            int maxTargets = Mathf.Max(1, profile.WormholeMaxTeleportTargets);
            int movedCount = 0;
            Vector3 destination = CalculateSharedTeleportDestination(center);
            for (int i = 0; i < enemyBuffer.Count && movedCount < maxTargets; i++)
            {
                EnemyController enemy = enemyBuffer[i];
                if (!IsTeleportCandidate(enemy))
                {
                    continue;
                }

                if (MonsterInteractionApi.TeleportMonster(enemy, destination))
                {
                    movedCount++;
                }
            }

            if (movedCount > 0)
            {
                PlayBlackHoleVfx(destination, radius, false);
            }

            Destroy(gameObject);
        }

        private Vector3 CalculateSharedTeleportDestination(Vector3 blackHoleCenter)
        {
            Vector3 fromNexus = blackHoleCenter - nexusPosition;
            fromNexus.y = 0f;
            if (fromNexus.sqrMagnitude <= 0.0001f)
            {
                fromNexus = direction;
                fromNexus.y = 0f;
            }

            if (fromNexus.sqrMagnitude <= 0.0001f)
            {
                float randomAngle = Random.Range(0f, 360f);
                fromNexus = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            }

            float minDistance = Mathf.Max(0f, profile.WormholeTeleportMinNexusDistance);
            float maxDistance = Mathf.Max(minDistance, profile.WormholeTeleportMaxNexusDistance);
            float distance = Random.Range(minDistance, maxDistance);
            return GroundService.ProjectToGround(nexusPosition + fromNexus.normalized * distance, 0f);
        }

        private bool IsTeleportCandidate(EnemyController enemy)
        {
            if (!SegmentTargetQuery.IsEnemyUsable(enemy))
            {
                return false;
            }

            return profile != null && (profile.WormholeAffectBosses || enemy.Grade != EnemyGrade.Boss);
        }

        private float GetHitRadius()
        {
            return profile != null ? Mathf.Max(0.05f, profile.WormholeProjectileHitRadius) : 0.05f;
        }

        private void PlayBlackHoleVfx(Vector3 center, float radius, bool showDebugRadius)
        {
            SegmentAttackVfxPlayer.PlayExplosion(profile.WormholeImpactVfxPrefab, center, radius, profile.WormholeVfxLifetime, profile.WormholeVfxAlpha);
            if (showDebugRadius && profile.WormholeShowDebugRadius)
            {
                SupportTemporaryVfx.ShowWorldArea(center, SegmentSupportAbilityKind.WormholePortal, radius, profile.WormholeVfxLifetime);
            }
        }

        private static Quaternion ResolveRotation(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static void DisableColliders(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static Material GetFallbackProjectileMaterial()
        {
            if (fallbackProjectileMaterial != null)
            {
                return fallbackProjectileMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            fallbackProjectileMaterial = new Material(shader)
            {
                name = "Runtime_SG56_WormholeProjectile"
            };

            Color color = new Color(0.32f, 0.68f, 1f, 0.86f);
            if (fallbackProjectileMaterial.HasProperty("_BaseColor"))
            {
                fallbackProjectileMaterial.SetColor("_BaseColor", color);
            }

            if (fallbackProjectileMaterial.HasProperty("_Color"))
            {
                fallbackProjectileMaterial.SetColor("_Color", color);
            }

            return fallbackProjectileMaterial;
        }
    }
}
