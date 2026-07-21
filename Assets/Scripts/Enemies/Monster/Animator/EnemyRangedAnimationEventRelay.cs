using UnityEngine;

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyRangedAnimationEventRelay : MonoBehaviour 
    {
        private EnemyRangedAttack rangedAttack; 

        private void Awake()
        {
            rangedAttack = GetComponentInParent<EnemyRangedAttack>();
        }

        public void ReleaseProjectile() 
        {
            if (rangedAttack == null)
            {
                rangedAttack = GetComponentInParent<EnemyRangedAttack>(); 
            }

            if (rangedAttack == null) 
            {
                return; 
            }

            rangedAttack.ReleasePendingAttack(); 
        }
    }
}