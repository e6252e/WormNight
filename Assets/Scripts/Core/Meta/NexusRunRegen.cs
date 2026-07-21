using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class NexusRunRegen : MonoBehaviour // 넥서스 런 회복
    {
        public NexusController Nexus; // 회복 대상
        [Min(0f)] public float RegenPerMinute; // 분당 회복량

        private float regenBank; // 소수 누적

        private void Awake() // 참조 보강
        {
            if (Nexus == null)
            {
                Nexus = GetComponent<NexusController>(); // 같은 오브젝트
            }
        }

        private void Update() // 회복 루프
        {
            if (Nexus == null || Nexus.IsDead || RegenPerMinute <= 0f)
            {
                return; // 회복 없음
            }

            regenBank += RegenPerMinute * Time.deltaTime / 60f; // 분당 → 초당
            int healAmount = Mathf.FloorToInt(regenBank); // 정수 회복
            if (healAmount <= 0)
            {
                return; // 아직 부족
            }

            regenBank -= healAmount; // 소비
            Nexus.Heal(healAmount); // 회복
        }
    }
}
