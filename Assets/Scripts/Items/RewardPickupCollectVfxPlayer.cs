using UnityEngine;

namespace TeamProject01.Gameplay
{
    internal static class RewardPickupCollectVfxPlayer // 보상 픽업 획득 VFX 공용 재생
    {
        private const string DefaultPickupVfxResourcePath = "RewardPickups/VFX_Pickup_Cast"; // Resources 기본 VFX
        private const float DefaultPickupVfxLifetime = 3f;
        private const float DefaultPickupVfxScale = 1f;

        private static GameObject cachedPickupVfxPrefab;
        private static bool loadAttempted;
        private static bool missingWarningLogged;

        public static void Play(Vector3 position) // 월드 위치 기준 획득 VFX
        {
            GameObject prefab = ResolvePickupVfxPrefab();
            if (prefab == null)
            {
                LogMissingPrefabOnce();
                return;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity); // 풀 반환 이후에도 남도록 독립 생성
            instance.name = "Reward_Pickup_Cast_VFX";
            instance.transform.localScale = Vector3.one * DefaultPickupVfxScale;
            DisableRuntimeColliders(instance);
            PlayParticles(instance);
            Object.Destroy(instance, ResolveLifetime(instance, DefaultPickupVfxLifetime)); // 잔여 파티클 정리
        }

        private static GameObject ResolvePickupVfxPrefab()
        {
            if (cachedPickupVfxPrefab != null)
            {
                return cachedPickupVfxPrefab;
            }

            if (!loadAttempted)
            {
                loadAttempted = true;
                cachedPickupVfxPrefab = Resources.Load<GameObject>(DefaultPickupVfxResourcePath); // Resources fallback
            }

            return cachedPickupVfxPrefab;
        }

        private static float ResolveLifetime(GameObject root, float fallback)
        {
            if (root == null)
            {
                return fallback;
            }

            float lifetime = fallback;
            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particle.main;
                if (main.loop)
                {
                    continue; // 루프형은 기본 수명으로 정리
                }

                float particleLifetime = main.duration + main.startDelay.constantMax + main.startLifetime.constantMax;
                lifetime = Mathf.Max(lifetime, particleLifetime + 0.25f);
            }

            return Mathf.Max(0.1f, lifetime);
        }

        private static void PlayParticles(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Play(true); // 즉시 재생
            }
        }

        private static void DisableRuntimeColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false; // 보상 수집 충돌과 분리
            }
        }

        private static void LogMissingPrefabOnce()
        {
            if (missingWarningLogged)
            {
                return;
            }

            missingWarningLogged = true;
            Debug.LogWarning("[RewardPickupCollectVfx] Resources/RewardPickups/VFX_Pickup_Cast prefab을 찾지 못했습니다.");
        }
    }
}
