using UnityEngine;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ParticleSystemWarmStart : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float warmStartSeconds = 0.5f; // 시작 건너뛰기
        [SerializeField] private bool playAfterWarmStart = true; // 시뮬레이션 후 재생

        private void OnEnable()
        {
            WarmStart();
        }

        private void WarmStart()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true); // 하위 파티클 포함
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i]; // 대상
                if (warmStartSeconds > 0f)
                {
                    system.Simulate(warmStartSeconds, true, true, true); // 초반부 건너뛰기
                }

                if (playAfterWarmStart)
                {
                    system.Play(true); // 이어서 재생
                }
            }
        }
    }
}
