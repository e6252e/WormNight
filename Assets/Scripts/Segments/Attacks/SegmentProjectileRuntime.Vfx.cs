using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime
    {
        private void PlayHitVfx(Vector3 position) // 명중 VFX
        {
            if (profile.HitVfxPrefab == null)
            {
                return; // 지정 없음
            }

            Transform socket = ResolveHitVfxSocket(); // 기준점
            Vector3 spawnPosition = socket != null ? socket.position : position; // 위치
            Quaternion rotation = socket != null ? socket.rotation : Quaternion.identity; // 방향
            SegmentAttackVfxPlayer.Play(profile.HitVfxPrefab, spawnPosition, rotation, profile.HitVfxLifetime); // 공용 생성
        }

        private Transform ResolveHitVfxSocket() // 명중 VFX 기준점
        {
            if (hitVfxSocket != null)
            {
                return hitVfxSocket; // 캐시
            }

            hitVfxSocket = FindChildRecursive(transform, "VFX_Hit"); // 정식 이름
            if (hitVfxSocket == null)
            {
                hitVfxSocket = FindChildRecursive(transform, "HitVFX"); // fallback
            }

            return hitVfxSocket;
        }

        private void PlayExplosionVfx(Vector3 position) // 폭발 VFX
        {
            PlayExplosionVfx(position, GetExplosionRadius()); // 강화 반경 사용
        }

        private void PlayExplosionVfx(Vector3 position, float radius) // 지정 반경으로 폭발 VFX
        {
            float lifetime = profile.ExplosionVfxLifetime > 0f ? profile.ExplosionVfxLifetime : profile.ExplosionLifetime; // 제거 시간
            float alpha = profile != null ? Mathf.Clamp01(profile.ExplosionVfxAlpha) : 0.28f; // 프로필 투명도
            SegmentAttackVfxPlayer.PlayExplosion(profile.ExplosionVfxPrefab, position, radius, lifetime, alpha); // 공용 폭발 VFX
        }

        private static Transform FindChildRecursive(Transform root, string childName) // 이름 검색
        {
            if (root == null)
            {
                return null; // 검색 불가
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i); // 하위
                if (child.name == childName)
                {
                    return child; // 발견
                }

                Transform found = FindChildRecursive(child, childName); // 재귀
                if (found != null)
                {
                    return found; // 발견
                }
            }

            return null; // 없음
        }
    }
}
