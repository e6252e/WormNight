using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GoldActionNexusHealSkill : MonoBehaviour // 4번 액션 HUD 넥서스 회복
    {
        private const string DefaultHealVfxResourcePath = "ActionHud/VFX_Heal_Cast"; // Resources 기본 VFX

        [Header("Scene References")]
        public NexusController Nexus; // 회복 대상 넥서스
        public Transform EffectRoot; // VFX 생성 루트

        [Header("Heal")]
        [Min(1)] public int BaseHealAmount = 50; // Lv1 회복량
        [Min(0)] public int HealAmountPerUpgrade = 15; // 강화당 회복 증가

        [Header("VFX")]
        public GameObject HealCastVfxPrefab; // VFX_Heal_Cast
        [Min(0f)] public float VfxYOffset = 0.2f; // 넥서스 기준 높이 보정
        [Min(0.01f)] public float VfxScale = 1f; // VFX 크기
        [Min(0.1f)] public float VfxLifetime = 4f; // VFX 제거 시간

        public bool CanHeal() // 회복 가능 여부
        {
            EnsureNexusReference(); // 넥서스 참조 보장
            return Nexus != null && !Nexus.IsDead && Nexus.CurrentHealth < Nexus.MaxHealth; // 체력 부족
        }

        public bool Play(int upgradeLevel) // 액션 HUD 4번 발동
        {
            EnsureReferences(); // 런타임 참조 보장

            if (!CanHeal())
            {
                Debug.Log("[GoldActionNexusHeal] Nexus 체력이 이미 가득 차 있거나 회복할 수 없습니다.", this);
                return false; // 골드/쿨타임 소모 방지
            }

            int level = Mathf.Max(1, upgradeLevel); // 강화 레벨
            int healAmount = GetHealAmount(level); // 회복량
            int before = Nexus.CurrentHealth; // 회복 전
            Nexus.Heal(healAmount); // 실제 회복
            int healed = Mathf.Max(0, Nexus.CurrentHealth - before); // 실제 회복량

            if (healed <= 0)
            {
                return false; // 회복 없음
            }

            SpawnHealVfx(); // 넥서스 회복 VFX
            Debug.Log($"[GoldActionNexusHeal] Nexus heal cast: Lv{level}, healed {healed}", this);
            return true; // 발동 성공
        }

        private void SpawnHealVfx() // VFX 생성
        {
            GameObject prefab = ResolveHealVfxPrefab(); // VFX prefab
            if (prefab == null || Nexus == null)
            {
                Debug.LogWarning("[GoldActionNexusHeal] VFX_Heal_Cast prefab을 찾지 못했습니다.", this);
                return; // VFX 없음
            }

            Vector3 position = Nexus.transform.position + Vector3.up * VfxYOffset; // 넥서스 위치
            Transform root = EffectRoot != null ? EffectRoot : transform; // 생성 루트
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, root); // VFX 생성
            instance.name = "Nexus_Heal_Cast_VFX"; // 런타임 이름
            instance.transform.localScale = Vector3.one * VfxScale; // 크기 보정
            DisableRuntimeColliders(instance); // 충돌 방지
            PlayParticles(instance); // 즉시 재생
            Destroy(instance, VfxLifetime); // 자동 제거
        }

        private GameObject ResolveHealVfxPrefab() // VFX prefab 찾기
        {
            if (HealCastVfxPrefab != null)
            {
                return HealCastVfxPrefab; // 인스펙터 우선
            }

            HealCastVfxPrefab = Resources.Load<GameObject>(DefaultHealVfxResourcePath); // Resources fallback
            return HealCastVfxPrefab; // 결과
        }

        private void EnsureReferences() // 참조 자동 보강
        {
            EnsureNexusReference(); // 넥서스 참조
            EnsureEffectRoot(); // VFX 루트
        }

        private void EnsureNexusReference() // 넥서스 참조 보강
        {
            if (Nexus == null)
            {
                Nexus = NexusController.Active; // 활성 넥서스
            }

            if (Nexus == null)
            {
                GameObject nexusObject = GameObject.Find("Nexus_Core"); // 이름 fallback
                Nexus = nexusObject != null ? nexusObject.GetComponent<NexusController>() : null; // 컴포넌트
            }
        }

        private void EnsureEffectRoot() // VFX 루트 보강
        {
            if (EffectRoot == null)
            {
                GameObject root = GameObject.Find("GoldActionNexusHealRoot"); // 기존 루트
                if (root == null)
                {
                    root = new GameObject("GoldActionNexusHealRoot"); // 새 루트
                }

                EffectRoot = root.transform; // 루트 저장
            }
        }

        private int GetHealAmount(int upgradeLevel) // 강화 반영 회복량
        {
            int level = Mathf.Max(1, upgradeLevel); // 레벨 보정
            return Mathf.Max(1, BaseHealAmount + (level - 1) * HealAmountPerUpgrade); // 회복량
        }

        private static void PlayParticles(GameObject root) // 파티클 재생
        {
            if (root == null)
            {
                return; // 대상 없음
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true); // 하위 파티클
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Play(true); // 재생
            }
        }

        private static void DisableRuntimeColliders(GameObject root) // 장식 충돌 제거
        {
            if (root == null)
            {
                return; // 대상 없음
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true); // 하위 콜라이더
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false; // 게임플레이 충돌 방지
            }
        }
    }
}
