using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class SegmentCutMagicEffect : MonoBehaviour // 선택된 무기 세그먼트에 표시하는 절단 마법 경고 효과
    {
        [Header("Effect Reference")]
        [SerializeField] private GameObject targetMarker; // 마법진과 지속 표시 VFX가 들어 있는 TargetMarker

        private ParticleSystem[] warningParticleSystems; // TargetMarker 내부의 모든 Particle System

        private void Awake()
        {
            if (targetMarker == null)
            {
                warningParticleSystems = new ParticleSystem[0]; // TargetMarker가 없다면 빈 배열을 사용한다.
                return;
            }

            warningParticleSystems = targetMarker.GetComponentsInChildren<ParticleSystem>(true); // 모든 자식 Particle System을 찾는다.

            for (int i = 0; i < warningParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = warningParticleSystems[i];

                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                main.stopAction = ParticleSystemStopAction.None; // 파티클 종료 시 자식 오브젝트가 자동 삭제되지 않게 한다.
            }

            targetMarker.SetActive(false); // 생성 직후에는 표시를 숨긴다.
        }

        public void ShowWarning() // 절단 대상으로 지정됐을 때 표시를 시작한다.
        {
            if (targetMarker == null)
            {
                return;
            }

            targetMarker.SetActive(true); // 마법진과 PersistentIndicator를 함께 활성화한다.

            for (int i = 0; i < warningParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = warningParticleSystems[i];

                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); // 이전 파티클을 정리한다.
                particleSystem.Play(false); // 마법진 시작 연출을 한 번 재생한다.
            }
        }

        public void Cancel() // 투사체가 적중하거나 사라질 때 표시 전체를 제거한다.
        {
            Destroy(gameObject); // 마법진과 PersistentIndicator를 함께 제거한다.
        }
    }
}