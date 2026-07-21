using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public static class EnemyShieldRegistry // 활성화된 범위 방어막을 관리한다.
    {
        private struct ShieldEntry
        {
            public Transform Owner; // 방어막 중심 몬스터
            public float Radius; // 방어막 반경
        }

        private static readonly List<ShieldEntry> activeShields = new List<ShieldEntry>(8); // 활성 방어막 목록

        public static void Register(Transform owner, float radius) // 방어막을 등록하거나 반경을 갱신한다.
        {
            if (owner == null)
            {
                return;
            }

            float resolvedRadius = Mathf.Max(0.1f, radius);

            for (int i = 0; i < activeShields.Count; i++)
            {
                ShieldEntry shield = activeShields[i];

                if (shield.Owner != owner)
                {
                    continue;
                }

                shield.Radius = resolvedRadius;
                activeShields[i] = shield;
                return;
            }

            ShieldEntry newShield = new ShieldEntry
            {
                Owner = owner,
                Radius = resolvedRadius
            };

            activeShields.Add(newShield);
        }

        public static void Unregister(Transform owner) // 해당 몬스터의 방어막을 해제한다.
        {
            if (owner == null)
            {
                return;
            }

            activeShields.RemoveAll(shield => shield.Owner == owner);
        }

        public static bool CanApplyDamage(EnemyController target, DamageData damage) // 방어막 규칙에 따라 피해 허용 여부를 반환한다.
        {
            Cleanup();

            if (target == null)
            {
                return false;
            }

            Vector3 targetPosition = target.transform.position;
            GameObject sourceObject = damage.SourceObject;
            bool targetIsProtected = false;

            for (int i = 0; i < activeShields.Count; i++)
            {
                ShieldEntry shield = activeShields[i];

                if (!IsInsideShield(targetPosition, shield))
                {
                    continue;
                }

                targetIsProtected = true;

                if (sourceObject == null)
                {
                    continue;
                }

                if (IsInsideShield(sourceObject.transform.position, shield))
                {
                    return true;
                }
            }

            return !targetIsProtected;
        }

        private static bool IsInsideShield(Vector3 position, ShieldEntry shield) // 위치가 방어막 범위 안인지 확인한다.
        {
            if (shield.Owner == null)
            {
                return false;
            }

            Vector3 offset = position - shield.Owner.position;
            offset.y = 0.0f;

            return offset.sqrMagnitude <= shield.Radius * shield.Radius;
        }

        private static void Cleanup() // 사라지거나 비활성화된 방어막을 정리한다.
        {
            activeShields.RemoveAll(shield => shield.Owner == null || !shield.Owner.gameObject.activeInHierarchy);
        }
    }
}