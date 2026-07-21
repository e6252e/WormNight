using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GoldActionMeteorImpact : MonoBehaviour
    {
        [Header("Sockets")]
        public Transform MeteorBodyRoot;
        public Transform MeteorModelSocket;
        public Transform MeteorTailVfxSocket;
        public Transform TelegraphVfxSocket;
        public Transform ExplosionVfxSocket;

        [Header("Optional Prefab Replacements")]
        public GameObject MeteorModelPrefab;
        public GameObject[] MeteorModelPrefabs;
        public bool ClearMeteorModelSocketBeforeSpawn = true;
        public GameObject MeteorTailVfxPrefab;
        public GameObject TelegraphVfxPrefab;
        public GameObject ExplosionVfxPrefab;
        public GameObject[] ExplosionVfxPrefabs;

        [Header("Position")]
        [Min(0f)] public float TelegraphGroundHeight = 0.035f;
        [Min(0f)] public float MeteorGroundHeight = 0.6f;
        [Min(0.05f)] public float ExplosionVfxLifetime = 0.65f;
        [Min(0f)] public float ExplosionVfxRadiusScaleMultiplier = 1f;
        [Min(0f)] public float MeteorSpinSpeed = 210f;

        [Header("Tail VFX Alignment")]
        public bool KeepTailVfxWorldAligned = true;
        public Vector3 TailVfxWorldOffset = new Vector3(0f, 1.35f, 0f);
        public Vector3 TailVfxWorldEuler;

        private readonly List<EnemyController> enemyBuffer = new List<EnemyController>(64);
        private QuarterViewCamera shakeCamera;
        private float impactShakeDuration;
        private float impactShakeAmplitude;
        private float impactShakeFrequency;
        private bool initializedVisuals;

        public void Play(
            Vector3 impactPosition,
            float fallHeight,
            float fallDuration,
            float impactRadius,
            float damage,
            float knockbackDistance,
            float knockbackDuration,
            float staggerDuration,
            QuarterViewCamera cameraToShake,
            float impactShakeDuration,
            float impactShakeAmplitude,
            float impactShakeFrequency)
        {
            shakeCamera = cameraToShake;
            this.impactShakeDuration = impactShakeDuration;
            this.impactShakeAmplitude = impactShakeAmplitude;
            this.impactShakeFrequency = impactShakeFrequency;

            EnsureSockets();
            PrepareTailVfxSocket();
            EnsureVisualPrefabs();
            StartCoroutine(MeteorRoutine(impactPosition, fallHeight, fallDuration, impactRadius, damage, knockbackDistance, knockbackDuration, staggerDuration));
        }

        private IEnumerator MeteorRoutine(
            Vector3 impactPosition,
            float fallHeight,
            float fallDuration,
            float impactRadius,
            float damage,
            float knockbackDistance,
            float knockbackDuration,
            float staggerDuration)
        {
            Vector3 rootPosition = GroundService.ProjectToGround(impactPosition, 0f);
            Vector3 telegraphPosition = GroundService.ProjectToGround(impactPosition, TelegraphGroundHeight);
            Vector3 meteorEndPosition = GroundService.ProjectToGround(impactPosition, MeteorGroundHeight);
            Vector3 meteorStartPosition = meteorEndPosition + Vector3.up * fallHeight;

            transform.position = rootPosition;
            SetSocketWorldPosition(TelegraphVfxSocket, telegraphPosition);
            SetSocketWorldPosition(ExplosionVfxSocket, telegraphPosition);
            SetSocketWorldPosition(MeteorBodyRoot, meteorStartPosition);
            UpdateTailVfxSocket(meteorStartPosition);
            SetAreaSocketScale(TelegraphVfxSocket, impactRadius);
            SetSocketActive(TelegraphVfxSocket, true);
            SetSocketActive(ExplosionVfxSocket, false);

            float elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fallDuration));
                float eased = t * t;

                if (MeteorBodyRoot != null)
                {
                    MeteorBodyRoot.position = Vector3.Lerp(meteorStartPosition, meteorEndPosition, eased);
                    MeteorBodyRoot.Rotate(Vector3.forward, MeteorSpinSpeed * Time.deltaTime, Space.Self);
                    MeteorBodyRoot.Rotate(Vector3.up, MeteorSpinSpeed * 0.45f * Time.deltaTime, Space.Self);
                    UpdateTailVfxSocket(MeteorBodyRoot.position);
                }

                yield return null;
            }

            SetSocketActive(MeteorTailVfxSocket, false);
            SetSocketActive(MeteorBodyRoot, false);
            SetSocketActive(TelegraphVfxSocket, false);
            ApplyImpact(telegraphPosition, impactRadius, damage, knockbackDistance, knockbackDuration, staggerDuration);
            SetExplosionSocketScale(ExplosionVfxSocket, impactRadius);
            SpawnExplosionVfx();
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.Explosion, telegraphPosition, true);
            SetSocketActive(ExplosionVfxSocket, true);
            shakeCamera?.AddShake(impactShakeDuration, impactShakeAmplitude, impactShakeFrequency);
            Destroy(gameObject, ExplosionVfxLifetime);
        }

        private void ApplyImpact(Vector3 impactPosition, float impactRadius, float damage, float knockbackDistance, float knockbackDuration, float staggerDuration)
        {
            enemyBuffer.Clear();
            EnemyController.CollectActiveInRange(impactPosition, impactRadius, enemyBuffer);

            for (int i = 0; i < enemyBuffer.Count; i++)
            {
                EnemyController enemy = enemyBuffer[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                Vector3 hitPosition = enemy.transform.position + Vector3.up * 0.6f;
                DamageData meteorDamage = DamageData.Create(damage, DamageType.Explosion, -1, hitPosition, gameObject);
                enemy.ApplyDamage(meteorDamage);

                Vector3 direction = enemy.transform.position - impactPosition;
                direction.y = 0f;
                MonsterFeedbackData feedback = MonsterFeedbackData.Create(
                    impactPosition,
                    direction,
                    hitPosition,
                    knockbackDistance,
                    knockbackDuration,
                    staggerDuration,
                    -1,
                    DamageType.Explosion,
                    gameObject);

                MonsterFeedbackApi.TryApplyFeedback(enemy, feedback);
            }
        }

        private void EnsureSockets()
        {
            MeteorBodyRoot = ResolveOrCreateSocket(MeteorBodyRoot, "MeteorBodyRoot", transform, Vector3.zero);
            MeteorModelSocket = ResolveOrCreateSocket(MeteorModelSocket, "MeteorModelSocket", MeteorBodyRoot, Vector3.zero);
            MeteorTailVfxSocket = ResolveOrCreateSocket(MeteorTailVfxSocket, "MeteorTailVfxSocket", KeepTailVfxWorldAligned ? transform : MeteorBodyRoot, TailVfxWorldOffset);
            TelegraphVfxSocket = ResolveOrCreateSocket(TelegraphVfxSocket, "TelegraphVfxSocket", transform, Vector3.zero);
            ExplosionVfxSocket = ResolveOrCreateSocket(ExplosionVfxSocket, "ExplosionVfxSocket", transform, Vector3.zero);
        }

        private void PrepareTailVfxSocket()
        {
            if (!KeepTailVfxWorldAligned || MeteorTailVfxSocket == null)
            {
                return;
            }

            MeteorTailVfxSocket.SetParent(transform, true);
            MeteorTailVfxSocket.rotation = Quaternion.Euler(TailVfxWorldEuler);
        }

        private void EnsureVisualPrefabs()
        {
            if (initializedVisuals)
            {
                return;
            }

            if (ClearMeteorModelSocketBeforeSpawn)
            {
                ClearSocketChildren(MeteorModelSocket);
            }

            InstantiateIfAssigned(SelectMeteorModelPrefab(), MeteorModelSocket);
            InstantiateIfAssigned(MeteorTailVfxPrefab, MeteorTailVfxSocket);
            InstantiateIfAssigned(TelegraphVfxPrefab, TelegraphVfxSocket);

            if (ExplosionVfxPrefab != null)
            {
                ClearSocketChildren(ExplosionVfxSocket);
            }

            initializedVisuals = true;
        }

        private void SpawnExplosionVfx()
        {
            GameObject explosionPrefab = SelectExplosionVfxPrefab();
            if (explosionPrefab == null || ExplosionVfxSocket == null)
            {
                return;
            }

            ClearSocketChildren(ExplosionVfxSocket);
            GameObject explosion = Instantiate(explosionPrefab, ExplosionVfxSocket.position, ExplosionVfxSocket.rotation);
            explosion.transform.localScale = ExplosionVfxSocket.lossyScale; // 부모 삭제와 분리해 끝까지 재생
            MakeParticleSystemsOneShot(explosion);
            Destroy(explosion, CalculateParticleLifetime(explosion, ExplosionVfxLifetime));
        }

        private GameObject SelectExplosionVfxPrefab()
        {
            if (ExplosionVfxPrefabs != null && ExplosionVfxPrefabs.Length > 0)
            {
                int start = Random.Range(0, ExplosionVfxPrefabs.Length);
                for (int i = 0; i < ExplosionVfxPrefabs.Length; i++)
                {
                    GameObject candidate = ExplosionVfxPrefabs[(start + i) % ExplosionVfxPrefabs.Length];
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            return ExplosionVfxPrefab;
        }

        private GameObject SelectMeteorModelPrefab()
        {
            if (MeteorModelPrefabs != null && MeteorModelPrefabs.Length > 0)
            {
                int start = Random.Range(0, MeteorModelPrefabs.Length);
                for (int i = 0; i < MeteorModelPrefabs.Length; i++)
                {
                    GameObject candidate = MeteorModelPrefabs[(start + i) % MeteorModelPrefabs.Length];
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            return MeteorModelPrefab;
        }

        private static void InstantiateIfAssigned(GameObject prefab, Transform parent)
        {
            if (prefab == null || parent == null)
            {
                return;
            }

            Instantiate(prefab, parent);
        }

        private static void ClearSocketChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static void MakeParticleSystemsOneShot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem.MainModule main = particles[i].main;
                main.loop = false; // 메테오 폭발은 루프 VFX도 1회성으로 마감
            }
        }

        private static float CalculateParticleLifetime(GameObject root, float fallbackLifetime)
        {
            float lifetime = Mathf.Max(0.05f, fallbackLifetime);
            if (root == null)
            {
                return lifetime;
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem.MainModule main = particles[i].main;
                if (main.loop)
                {
                    continue;
                }

                float startDelay = GetMaxCurveValue(main.startDelay);
                float startLifetime = GetMaxCurveValue(main.startLifetime);
                lifetime = Mathf.Max(lifetime, startDelay + main.duration + startLifetime);
            }

            return lifetime + 0.25f;
        }

        private static float GetMaxCurveValue(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return curve.constantMax;
                case ParticleSystemCurveMode.Curve:
                    return GetCurveMaxValue(curve.curve) * curve.curveMultiplier;
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Max(GetCurveMaxValue(curve.curveMin), GetCurveMaxValue(curve.curveMax)) * curve.curveMultiplier;
                default:
                    return 0f;
            }
        }

        private static float GetCurveMaxValue(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0f;
            }

            float max = 0f;
            for (int i = 0; i < curve.length; i++)
            {
                max = Mathf.Max(max, curve.keys[i].value);
            }

            return max;
        }

        private void UpdateTailVfxSocket(Vector3 meteorPosition)
        {
            if (MeteorTailVfxSocket == null || !KeepTailVfxWorldAligned)
            {
                return;
            }

            MeteorTailVfxSocket.position = meteorPosition + TailVfxWorldOffset;
            MeteorTailVfxSocket.rotation = Quaternion.Euler(TailVfxWorldEuler);
        }

        private static Transform ResolveOrCreateSocket(Transform current, string socketName, Transform parent, Vector3 localPosition)
        {
            if (current != null)
            {
                return current;
            }

            Transform found = parent != null ? parent.Find(socketName) : null;
            if (found != null)
            {
                return found;
            }

            GameObject socket = new GameObject(socketName);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            return socket.transform;
        }

        private static void SetSocketWorldPosition(Transform socket, Vector3 position)
        {
            if (socket != null)
            {
                socket.position = position;
            }
        }

        private static void SetAreaSocketScale(Transform socket, float radius)
        {
            if (socket != null)
            {
                float diameter = Mathf.Max(0.1f, radius * 2f);
                socket.localScale = new Vector3(diameter, 1f, diameter);
            }
        }

        private void SetExplosionSocketScale(Transform socket, float radius)
        {
            if (socket != null)
            {
                float scale = Mathf.Max(0.01f, radius * Mathf.Max(0f, ExplosionVfxRadiusScaleMultiplier));
                socket.localScale = Vector3.one * scale;
            }
        }

        private static void SetSocketActive(Transform socket, bool active)
        {
            if (socket != null)
            {
                socket.gameObject.SetActive(active);
            }
        }
    }
}
