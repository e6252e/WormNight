using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum BossPhase
    {
        Dead = 0,
        Normal = 1,
        Aggressive = 2,
        Rage = 3,
        Berserk = 4
    }

    public sealed class BossController : MonoBehaviour
    {
        [Range(0.0f, 1.0f)]
        [SerializeField] private float aggressiveThreshold = 0.75f;

        [Range(0.0f, 1.0f)]
        [SerializeField] private float rageThreshold = 0.50f;

        [Range(0.0f, 1.0f)]
        [SerializeField] private float berserkThreshold = 0.25f;

        private EnemyHealth health;

        public BossPhase CurrentPhase { get; private set; } = BossPhase.Normal;

        public float HealthRatio { get; private set; } = 1.0f;

        public bool IsActionRunning { get; private set; }

        public bool IsDead
        {
            get
            {
                return health == null || health.IsDead;
            }
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void Start()
        {
            RefreshPhase();
        }

        private void Update()
        {
            RefreshPhase();

            if (IsDead)
            {
                IsActionRunning = false;
            }
        }

        public bool TryBeginAction()
        {
            if (IsDead)
            {
                return false;
            }

            if (IsActionRunning)
            {
                return false;
            }

            IsActionRunning = true;
            return true;
        }

        public void EndAction()
        {
            IsActionRunning = false;
        }

        private void RefreshPhase()
        {
            if (health == null)
            {
                HealthRatio = 0.0f;
                CurrentPhase = BossPhase.Dead;
                return;
            }

            if (health.IsDead || health.MaxHp <= 0.0f)
            {
                HealthRatio = 0.0f;
                CurrentPhase = BossPhase.Dead;
                return;
            }

            HealthRatio = Mathf.Clamp01(health.CurrentHp / health.MaxHp);

            if (HealthRatio <= berserkThreshold)
            {
                CurrentPhase = BossPhase.Berserk;
                return;
            }

            if (HealthRatio <= rageThreshold)
            {
                CurrentPhase = BossPhase.Rage;
                return;
            }

            if (HealthRatio <= aggressiveThreshold)
            {
                CurrentPhase = BossPhase.Aggressive;
                return;
            }

            CurrentPhase = BossPhase.Normal;
        }
    }
}