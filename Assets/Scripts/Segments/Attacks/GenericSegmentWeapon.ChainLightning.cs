using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private void FireChainLightning(EnemyController firstTarget, Transform startAnchor, Vector3 startPosition, DamageData damage) // 즉시 체인 번개
        {
            if (!IsTargetUsable(firstTarget))
            {
                return; // 첫 대상 없음
            }

            HashSet<int> hitIds = new HashSet<int>(); // 한 번의 체인 안에서 중복 타격 방지
            Vector3 firstHitPosition = GetEnemyHitPosition(firstTarget); // 첫 명중 위치
            hitIds.Add(firstTarget.EnemyId); // 첫 대상 기록
            SegmentLightningChainVfx.Spawn(startAnchor, startPosition, firstHitPosition, AttackProfile.ChainLineVfxLifetime); // 머즐 -> 첫 대상
            SegmentHitResolver.ApplyDamageAndFeedback(firstTarget, damage, AttackProfile, firstHitPosition, startPosition, SegmentMonsterFeedbackKind.Chain); // 첫 대상 피해 + 피드백
            PlayHitVfx(firstHitPosition); // 첫 명중 VFX
            StartCoroutine(ChainLightningRoutine(firstHitPosition, 1, damage, hitIds)); // 첫 대상 위치에서 확산
        }

        private IEnumerator ChainLightningRoutine(Vector3 fromPosition, int depth, DamageData baseDamage, HashSet<int> hitIds) // 체인 확산
        {
            if (depth > GetEffectiveMaxChainDepth())
            {
                yield break; // 최대 체인 단계 도달
            }

            float delay = Mathf.Max(0f, AttackProfile.ChainDelay); // 단계 지연
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (!CanUseWeapon())
            {
                yield break; // 세그먼트가 분리/비활성화됨
            }

            List<ChainCandidate> targets = SelectChainTargets(fromPosition, hitIds); // 다음 후보
            if (targets.Count == 0)
            {
                yield break; // 더 퍼질 대상 없음
            }

            float damageAmount = CalculateChainDamage(baseDamage.Amount, depth); // 단계별 감쇠 피해
            DamageData chainDamage = DamageData.Create(damageAmount, DamageType.Electric, baseDamage.SourceSegmentIndex, fromPosition, baseDamage.SourceObject); // 체인 피해
            for (int i = 0; i < targets.Count; i++)
            {
                ChainCandidate target = targets[i];
                if (!IsTargetUsable(target.Enemy))
                {
                    continue; // 사라진 대상
                }

                hitIds.Add(target.Id); // 중복 방지
                Vector3 hitPosition = GetEnemyHitPosition(target.Enemy); // 명중 위치
                SegmentLightningChainVfx.Spawn(fromPosition, hitPosition, AttackProfile.ChainLineVfxLifetime); // 몬스터 -> 몬스터
                SegmentHitResolver.ApplyDamageAndFeedback(target.Enemy, chainDamage, AttackProfile, hitPosition, fromPosition, SegmentMonsterFeedbackKind.Chain); // 체인 피해 + 피드백
                PlayHitVfx(hitPosition); // 명중 VFX
                StartCoroutine(ChainLightningRoutine(hitPosition, depth + 1, baseDamage, hitIds)); // 다음 단계 확산
            }
        }

        private List<ChainCandidate> SelectChainTargets(Vector3 fromPosition, HashSet<int> hitIds) // 주변 체인 후보 선택
        {
            List<ChainCandidate> candidates = new List<ChainCandidate>(); // 전체 후보
            float range = GetEffectiveChainRange(); // 체인 거리
            Collider[] hits = Physics.OverlapSphere(fromPosition, range, ~0, QueryTriggerInteraction.Collide); // 주변 콜라이더
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue; // 빈 콜라이더
                }

                EnemyController enemy = hit.GetComponentInParent<EnemyController>(); // 몬스터 확인
                if (!SegmentTargetQuery.IsEnemyUsable(enemy) || hitIds.Contains(enemy.EnemyId) || ContainsChainCandidate(candidates, enemy.EnemyId))
                {
                    continue; // 이미 맞았거나 중복 후보
                }

                Vector3 center = GetEnemyHitPosition(enemy); // 후보 중심
                float distance = Vector3.Distance(fromPosition, center); // 거리
                candidates.Add(new ChainCandidate(enemy, enemy.EnemyId, center, 1f / Mathf.Max(0.1f, distance))); // 가까운 대상 우선
            }

            List<ChainCandidate> selected = new List<ChainCandidate>(); // 최종 선택
            int count = Mathf.Min(Mathf.Max(1, AttackProfile.ChainBranchCount), candidates.Count); // 분기 수
            for (int i = 0; i < count; i++)
            {
                int index = PickWeightedChainCandidate(candidates); // 거리 가중 랜덤
                if (index < 0)
                {
                    break; // 후보 없음
                }

                selected.Add(candidates[index]); // 선택
                candidates.RemoveAt(index); // 중복 선택 방지
            }

            return selected;
        }

        private static bool ContainsChainCandidate(List<ChainCandidate> candidates, int enemyId) // 후보 중복 확인
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Id == enemyId)
                {
                    return true; // 이미 후보에 있음
                }
            }

            return false;
        }

        private static int PickWeightedChainCandidate(List<ChainCandidate> candidates) // 가중 랜덤 선택
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(0f, candidates[i].Weight); // 가중치 합산
            }

            if (totalWeight <= 0f)
            {
                return candidates.Count > 0 ? 0 : -1; // fallback
            }

            float roll = Random.value * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= Mathf.Max(0f, candidates[i].Weight);
                if (roll <= 0f)
                {
                    return i; // 선택
                }
            }

            return candidates.Count - 1; // 부동소수 오차 fallback
        }

        private float CalculateChainDamage(float baseAmount, int depth) // 체인 단계별 피해
        {
            float falloff = GetEffectiveChainDamageFalloff(); // 감쇠율
            float multiplier = Mathf.Pow(falloff, Mathf.Max(1, depth)); // depth 1부터 감쇠
            return Mathf.Max(0f, baseAmount * multiplier); // 최종 피해
        }

        private readonly struct ChainCandidate // 체인 번개 후보
        {
            public readonly EnemyController Enemy; // 대상 몬스터
            public readonly int Id; // 중복 방지 ID
            public readonly Vector3 Center; // 중심 위치
            public readonly float Weight; // 선택 가중치

            public ChainCandidate(EnemyController enemy, int id, Vector3 center, float weight)
            {
                Enemy = enemy;
                Id = id;
                Center = center;
                Weight = weight;
            }
        }
    }
}
