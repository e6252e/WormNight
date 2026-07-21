// 안건준 추가 - 0622
using System;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum NexusScreenFeedbackKind
    {
        ShieldHit,
        HealthHit,
        Heal
    }

    public sealed class NexusController : MonoBehaviour // 넥서스 체력 입구
    {
        public static NexusController Active { get; private set; } // 현재 넥서스

        [Header("Health")]
        [Min(1)] public int MaxHealth = 100; // 최대 체력
        [Min(0)] public int CurrentHealth = 100; // 현재 체력

        [Header("Shield")]
        [Min(0)] public int MaxShield = 50; // 최대 보호막
        [Min(0)] public int CurrentShield = 50; // 현재 보호막
        public bool StartWithFullShield = true; // 시작 시 보호막 채우기
        [Min(0f)] public float ShieldRegenDelay = 5f; // 마지막 피해 후 보호막 회복 대기 시간
        [Min(0f)] public float ShieldRegenPerSecond = 5f; // 초당 보호막 회복량

        [Header("Debug")]
        public bool IsInvincible; // 디버그 무적

        [Header("VFX")]
        public bool ConfigureAttachedNexusVfx = true; // 부착된 넥서스 VFX 설정
        public bool ShowLightningGroundVfx = true; // 바닥 번개 VFX 표시
        public bool ShowManaWallShieldVfx = true; // 보호막 ManaWall VFX 표시

        [Header("Start Segment Choice Tickets")]
        public bool SpawnSegmentChoiceTicketsOnStart = true; // 시작 시 선택권 드랍
        [Min(0)] public int StartingSegmentChoiceTicketCount = 3; // 시작 선택권 개수
        [Min(1)] public int StartingSegmentChoiceTicketAmount = 1; // 픽업당 선택권 횟수
        [Min(0f)] public float StartingSegmentChoiceTicketMinRadius = 6.2f; // 최소 드랍 반경
        [Min(0f)] public float StartingSegmentChoiceTicketMaxRadius = 11.2f; // 최대 드랍 반경

        public bool IsDead { get; private set; } // 사망 여부
        public float HealthRatio => MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)CurrentHealth / MaxHealth); // HUD 비율
        public float ShieldRatio => MaxShield <= 0 ? 0f : Mathf.Clamp01((float)CurrentShield / MaxShield); // 보호막 HUD 비율

        public event Action<int, int> HealthChanged; // 현재/최대 체력
        public event Action<int, int> ShieldChanged; // 현재/최대 보호막
        public event Action<NexusController> StateChanged; // 전체 상태 변경
        public event Action<NexusScreenFeedbackKind, int> ScreenFeedbackRequested; // 피격/회복 화면 연출
        public event Action<NexusController> Died; // 사망 알림

        private float lastDamageTime = -999f; // 마지막 피해 시각
        private float shieldRegenBank; // 보호막 소수 회복 누적
        private bool shieldRegenStartSfxPlayed;

        private void Awake() // 등록
        {
            Active = this; // 현재 인스턴스
            if (StartWithFullShield && CurrentShield <= 0 && MaxShield > 0)
            {
                CurrentShield = MaxShield; // 시작 보호막 보정
            }

            ClampState(); // 상태 보정
            IsDead = CurrentHealth <= 0; // 초기 사망 상태
            ConfigureSceneVfx(); // 부착 VFX 설정
        }

        private void Start() // 시작 보상 드랍
        {
            SpawnStartingSegmentChoiceTickets(); // 테스트 선택권 배치
        }

        private void Update() // 보호막 회복
        {
            RegenerateShield(); // 보호막 자동 회복
        }

        private void OnDestroy() // 해제
        {
            if (Active == this)
            {
                Active = null; // 참조 제거
            }
        }

        public void ResetHealth() // 체력 초기화
        {
            CurrentHealth = MaxHealth; // 최대 체력 복구
            IsDead = false; // 사망 해제
            NotifyHealthChanged(); // 변경 알림
        }

        public void ResetShield(bool fillToMax) // 보호막 초기화
        {
            CurrentShield = fillToMax ? MaxShield : Mathf.Clamp(CurrentShield, 0, MaxShield); // 보호막 보정
            shieldRegenBank = 0f; // 회복 누적 초기화
            shieldRegenStartSfxPlayed = false;
            NotifyShieldChanged(); // 변경 알림
        }

        public void ApplyDamage(int amount) // 피해 입구
        {
            if (IsDead || amount <= 0 || IsInvincible)
            {
                return; // 처리 없음
            }

            lastDamageTime = Time.time; // 회복 대기 갱신
            shieldRegenBank = 0f; // 피해 직후 소수 회복 누적 제거
            shieldRegenStartSfxPlayed = false;

            int remainingDamage = amount; // 남은 피해량
            int shieldDamage = 0; // 보호막 실제 피해
            if (CurrentShield > 0)
            {
                int previousShield = CurrentShield;
                shieldDamage = Mathf.Min(CurrentShield, remainingDamage); // 보호막 흡수량
                CurrentShield -= shieldDamage; // 보호막 감소
                remainingDamage -= shieldDamage; // 남은 피해 계산
                NotifyShieldChanged(); // 보호막 변경 알림
                if (previousShield > 0 && CurrentShield <= 0)
                {
                    PlayShieldBreakSfx();
                }
                else if (shieldDamage > 0)
                {
                    PlayNexusSfx(GameplaySfxCue.ShieldHit);
                }
            }

            if (remainingDamage <= 0)
            {
                if (shieldDamage > 0)
                {
                    NotifyScreenFeedback(NexusScreenFeedbackKind.ShieldHit, shieldDamage); // 보호막 피격 연출
                }

                return; // 체력 피해 없음
            }

            int previousHealth = CurrentHealth; // 실제 체력 피해 계산용
            CurrentHealth = Mathf.Max(0, CurrentHealth - remainingDamage); // 체력 감소
            int healthDamage = previousHealth - CurrentHealth; // 실제 체력 피해
            NotifyHealthChanged(); // 변경 알림
            if (healthDamage > 0)
            {
                NotifyScreenFeedback(NexusScreenFeedbackKind.HealthHit, healthDamage); // 체력 피격 연출
            }

            if (CurrentHealth <= 0)
            {
                IsDead = true; // 사망 표시
                Died?.Invoke(this); // 사망 알림
            }
        }

        public void Heal(int amount) // 회복 입구
        {
            if (amount <= 0 || IsDead)
            {
                return; // 처리 없음
            }

            int previousHealth = CurrentHealth; // 실제 회복량 계산용
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount); // 체력 회복
            int healedAmount = CurrentHealth - previousHealth; // 실제 회복량
            NotifyHealthChanged(); // 변경 알림
            if (healedAmount > 0)
            {
                PlayNexusSfx(GameplaySfxCue.NexusHeal);
                NotifyScreenFeedback(NexusScreenFeedbackKind.Heal, healedAmount); // 회복 연출
            }
        }

        public void AddShield(int amount) // 보호막 추가
        {
            if (amount <= 0 || IsDead || MaxShield <= 0)
            {
                return; // 처리 없음
            }

            CurrentShield = Mathf.Min(MaxShield, CurrentShield + amount); // 보호막 회복
            NotifyShieldChanged(); // 변경 알림
        }

        public void IncreaseMaxShield(int amount, bool fillAddedShield) // 최대 보호막 증가
        {
            if (amount <= 0 || IsDead)
            {
                return; // 처리 없음
            }

            MaxShield = Mathf.Max(0, MaxShield + amount); // 최대 보호막 증가
            if (fillAddedShield)
            {
                CurrentShield = Mathf.Min(MaxShield, CurrentShield + amount); // 증가분만큼 현재 보호막도 채움
            }
            else
            {
                CurrentShield = Mathf.Clamp(CurrentShield, 0, MaxShield); // 현재값 보정
            }

            NotifyShieldChanged(); // 변경 알림
        }

        // 안건준 추가 - 0622
        public void IncreaseMaxHealth(int amount) // 최대 체력 증가 + 증가분만큼 현재 체력 회복
        {
            if (amount <= 0) // 0 이하 보너스 무시
            {
                return;
            }

            MaxHealth = Mathf.Max(1, MaxHealth + amount); // 최대 체력 상승
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount); // 증가분만큼 현재 체력도 채움
            NotifyHealthChanged(); // HUD 등 갱신 알림
        }

        public static bool TryApplyDamage(Transform target, int amount) // 외부 피해 연결
        {
            NexusController nexus = target != null ? target.GetComponentInParent<NexusController>() : null; // 대상 검색
            if (nexus == null)
            {
                nexus = Active; // 등록 넥서스 fallback
            }

            if (nexus == null)
            {
                return false; // 넥서스 없음
            }

            nexus.ApplyDamage(amount); // 피해 적용
            return true; // 호출 성공
        }

        private void SpawnStartingSegmentChoiceTickets() // 시작 선택권 드랍
        {
            if (!SpawnSegmentChoiceTicketsOnStart || StartingSegmentChoiceTicketCount <= 0 || Active != this)
            {
                return; // 처리 없음
            }

            int amount = Mathf.Max(1, StartingSegmentChoiceTicketAmount); // 픽업당 횟수
            for (int i = 0; i < StartingSegmentChoiceTicketCount; i++)
            {
                Vector3 dropPosition = transform.position + CreateStartingSegmentChoiceTicketOffset(i, StartingSegmentChoiceTicketCount); // 넥서스 주변
                RewardDropService.SpawnSegmentChoiceTicket(amount, dropPosition); // 기존 월드드랍 경로
            }
        }

        private Vector3 CreateStartingSegmentChoiceTicketOffset(int index, int count) // 시작 선택권 위치
        {
            float maxRadius = Mathf.Max(StartingSegmentChoiceTicketMinRadius, StartingSegmentChoiceTicketMaxRadius); // 반경 보정
            float minRadius = Mathf.Clamp(StartingSegmentChoiceTicketMinRadius, 0f, maxRadius); // 최소 보정
            float radius = UnityEngine.Random.Range(minRadius, maxRadius); // 무작위 거리
            float baseAngle = count > 0 ? 360f * index / count : 0f; // 균등 분산
            float angle = baseAngle + UnityEngine.Random.Range(-42f, 42f); // 위치 흔들림
            return Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius; // 수평 오프셋
        }

        private void RegenerateShield() // 보호막 자동 회복
        {
            if (IsDead || MaxShield <= 0 || CurrentShield >= MaxShield || ShieldRegenPerSecond <= 0f)
            {
                return; // 회복 없음
            }

            if (Time.time < lastDamageTime + ShieldRegenDelay)
            {
                return; // 피해 후 대기 중
            }

            shieldRegenBank += ShieldRegenPerSecond * Time.deltaTime; // 소수 회복 누적
            int regenAmount = Mathf.FloorToInt(shieldRegenBank); // 정수 회복량
            if (regenAmount <= 0)
            {
                return; // 아직 회복량 부족
            }

            shieldRegenBank -= regenAmount; // 누적 회복량 소비
            int previousShield = CurrentShield; // 이전 보호막
            CurrentShield = Mathf.Min(MaxShield, CurrentShield + regenAmount); // 보호막 회복
            if (CurrentShield != previousShield)
            {
                if (!shieldRegenStartSfxPlayed)
                {
                    PlayNexusSfx(GameplaySfxCue.ShieldRegenStart);
                    shieldRegenStartSfxPlayed = true;
                }

                NotifyShieldChanged(); // 변경 알림
                if (CurrentShield >= MaxShield)
                {
                    shieldRegenStartSfxPlayed = false;
                }
            }
        }

        private void ClampState() // 인스펙터 값 보정
        {
            MaxHealth = Mathf.Max(1, MaxHealth); // 최대 체력 보정
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth); // 현재 체력 보정
            MaxShield = Mathf.Max(0, MaxShield); // 최대 보호막 보정
            CurrentShield = Mathf.Clamp(CurrentShield, 0, MaxShield); // 현재 보호막 보정
            ShieldRegenDelay = Mathf.Max(0f, ShieldRegenDelay); // 대기 시간 보정
            ShieldRegenPerSecond = Mathf.Max(0f, ShieldRegenPerSecond); // 회복량 보정
            StartingSegmentChoiceTicketCount = Mathf.Max(0, StartingSegmentChoiceTicketCount); // 시작 선택권 개수 보정
            StartingSegmentChoiceTicketAmount = Mathf.Max(1, StartingSegmentChoiceTicketAmount); // 선택권 횟수 보정
            StartingSegmentChoiceTicketMinRadius = Mathf.Max(0f, StartingSegmentChoiceTicketMinRadius); // 최소 반경 보정
            StartingSegmentChoiceTicketMaxRadius = Mathf.Max(StartingSegmentChoiceTicketMinRadius, StartingSegmentChoiceTicketMaxRadius); // 최대 반경 보정
        }

        private void NotifyHealthChanged() // 체력 변경 알림
        {
            HealthChanged?.Invoke(CurrentHealth, MaxHealth); // 체력 전용 알림
            StateChanged?.Invoke(this); // 전체 상태 알림
        }

        private void NotifyShieldChanged() // 보호막 변경 알림
        {
            ShieldChanged?.Invoke(CurrentShield, MaxShield); // 보호막 전용 알림
            StateChanged?.Invoke(this); // 전체 상태 알림
        }

        private void NotifyScreenFeedback(NexusScreenFeedbackKind kind, int amount) // 화면 연출 알림
        {
            ScreenFeedbackRequested?.Invoke(kind, Mathf.Max(0, amount));
        }

        private void PlayShieldBreakSfx()
        {
            PlayNexusSfx(GameplaySfxCue.ShieldBreak);
        }

        private void PlayNexusSfx(GameplaySfxCue cue)
        {
            if (GameplaySfxEmitter.TryPlay(transform, cue))
            {
                return;
            }

            GameplaySfxEmitter.TryPlayCatalogAt(cue, transform.position);
        }

        private void ConfigureSceneVfx() // 넥서스 부착 VFX 설정
        {
            if (!ConfigureAttachedNexusVfx)
            {
                return; // 처리 없음
            }

            NexusVfxController vfxController = GetComponent<NexusVfxController>(); // 부착 VFX
            if (vfxController == null)
            {
                return; // 씬에 붙어 있지 않으면 생성하지 않음
            }

            Transform lightning = transform.Find(NexusVfxController.LightningGroundName); // 번개 자식
            Transform manaWall = transform.Find(NexusVfxController.ManaWallShieldName); // 마나월 자식
            vfxController.Configure(
                this,
                lightning != null ? lightning.gameObject : null,
                manaWall != null ? manaWall.gameObject : null,
                ShowLightningGroundVfx,
                ShowManaWallShieldVfx); // 설정 전달
        }

#if UNITY_EDITOR
        private void OnValidate() // 인스펙터 편집 보정
        {
            ClampState(); // 값 보정
        }
#endif
    }
}
