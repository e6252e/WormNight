using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyPortalMagicCircleVisual : MonoBehaviour // 포탈 마법진의 시각 효과를 담당
    {
        [Header("Pulse Setting")]
        [Min(0.0f)]
        [SerializeField] private float pulseAmount = 0.08f;// 마법진 크기가 기본 크기에서 얼마나 커졌다 작아질지 정한다.

        [Min(0.1f)]
        [SerializeField] private float pulseSpeed = 2.0f;// 마법진 크기가 반복해서 변하는 속도를 정한다.

        private Vector3 initialScale;// 마법진의 처음 크기를 저장한다.

        private void Awake()
        {
            initialScale = transform.localScale;// Inspector에서 설정한 마법진 기본 크기를 저장한다.
        }

        private void Update()
        {
            float pulse = 1.0f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;// 시간에 따라 커졌다 작아지는 배율을 계산한다.

            transform.localScale = new Vector3(
                initialScale.x * pulse,
                initialScale.y,
                initialScale.z * pulse);// 바닥 방향인 X와 Z 크기만 반복해서 변경한다.
        }
    }
}