using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class StageStartBonusApplier : MonoBehaviour // StageScene 시작 보너스 적용
    {
        public CoreStatProvider CoreStats; // 코어 스탯
        public NexusController Nexus; // 넥서스
        public ConvoyController Convoy; // 컨보이
        public RunStartBonusData FallbackBonus; // 테스트용 기본값
        public bool UseFallbackWhenContextEmpty = true; // 컨텍스트 없을 때 fallback

        private IEnumerator Start() // 씬 시작 후 적용
        {
            yield return null; // 다른 Start 이후 적용
            ApplyConfiguredBonus(); // 보너스 반영
        }

        public void ApplyConfiguredBonus() // 현재 설정 적용
        {
            RunStartBonusData bonus; // 적용값
            if (!RunLoadoutContext.TryGetStartBonus(out bonus) && UseFallbackWhenContextEmpty)
            {
                bonus = FallbackBonus; // 테스트 값
            }

            ApplyBonus(bonus); // 실제 반영
        }

        public void ApplyBonus(RunStartBonusData bonus) // 보너스 적용
        {
            EnsureReferences(); // 참조 보강
            ApplyNexusBonus(bonus); // 넥서스
            ApplyCoreBonus(bonus); // 코어
            ApplyConvoyBonus(bonus); // 컨보이
        }

        private void EnsureReferences() // 참조 찾기
        {
            if (CoreStats == null)
            {
                CoreStats = CoreStatProvider.Active; // 코어
            }

            if (Nexus == null)
            {
                Nexus = NexusController.Active; // 넥서스
            }

            if (Convoy == null)
            {
                Convoy = FindFirstObjectByType<ConvoyController>(); // 컨보이
            }
        }

        private void ApplyNexusBonus(RunStartBonusData bonus) // 넥서스 반영
        {
            if (Nexus == null)
            {
                return; // 대상 없음
            }

            if (bonus.NexusMaxHealthPercentBonus != 0f)
            {
                int maxHealth = Mathf.Max(1, Mathf.RoundToInt(Nexus.MaxHealth * (1f + bonus.NexusMaxHealthPercentBonus))); // 체력 보정
                Nexus.MaxHealth = maxHealth; // 최대 체력
                Nexus.ResetHealth(); // 현재 체력 동기화
            }

            if (bonus.NexusRegenPerMinuteBonus > 0f)
            {
                NexusRunRegen regen = Nexus.GetComponent<NexusRunRegen>(); // 회복 컴포넌트
                if (regen == null)
                {
                    regen = Nexus.gameObject.AddComponent<NexusRunRegen>(); // 런타임 추가
                }

                regen.Nexus = Nexus; // 대상
                regen.RegenPerMinute += bonus.NexusRegenPerMinuteBonus; // 회복량
            }
        }

        private void ApplyCoreBonus(RunStartBonusData bonus) // 코어 스탯 반영
        {
            if (CoreStats == null)
            {
                return; // 대상 없음
            }

            float baseTurnSpeed = Convoy != null ? Convoy.TurnSpeed : 0f; // 회전 기준
            CoreStats.ApplyRunStartBonus(bonus, baseTurnSpeed); // 코어 반영
        }

        private void ApplyConvoyBonus(RunStartBonusData bonus) // 컨보이 반영
        {
            if (Convoy == null)
            {
                return; // 대상 없음
            }

            // Convoy.ApplySelectedWormStarterSegment(bonus.SelectedWormId); // ConvoyController 원본 스타터 중복 방지

            if (bonus.StartingSegmentBonus > 0)
            {
                Convoy.AddSegments(bonus.StartingSegmentBonus, true); // 시작 세그먼트 추가
            }
        }
    }
}
