using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BossDiamondAnimationEventRelay : MonoBehaviour
    {
        private BossDiamondSiegeAttack diamondSiegeAttack; // 부모 Boss01에 붙어 있는 다이아몬드 공격 Script

        private void Awake()
        {
            diamondSiegeAttack = GetComponentInParent<BossDiamondSiegeAttack>(); // Animation Event를 실제 공격 Script로 전달하기 위해 부모에서 찾는다.
        }

        public void OnDiamondAnimationFire()
        {
            if (diamondSiegeAttack == null)
            {
                diamondSiegeAttack = GetComponentInParent<BossDiamondSiegeAttack>();
            }

            if (diamondSiegeAttack == null)
            {
                return;
            }

            diamondSiegeAttack.OnDiamondAnimationFire();
        }
    }
}