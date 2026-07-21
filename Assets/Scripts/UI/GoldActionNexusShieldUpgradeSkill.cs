using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class GoldActionNexusShieldUpgradeSkill : MonoBehaviour // 5번 액션 HUD 넥서스 실드 강화
    {
        private const string DefaultVfxResourcePath = "ActionHud/VFX_Heal_Cast"; // 힐과 동일 VFX

        [Header("Scene References")]
        public NexusController Nexus; // 대상 넥서스
        public NexusVfxController NexusVfx; // 보호막 VFX 제어
        public Transform EffectRoot; // VFX 생성 루트

        [Header("Shield Stat")]
        [Min(1)] public int ShieldIncreasePerUpgrade = 30; // 1회 강화당 실드 증가
        public bool FillAddedShield = true; // 증가분 즉시 충전

        [Header("Shield Visual")]
        [Min(0.01f)] public float ManaWallYScaleIncrease = 2f; // 1회 강화당 Y 증가

        [Header("VFX")]
        public GameObject UpgradeVfxPrefab; // VFX_Heal_Cast
        [Min(0f)] public float VfxYOffset = 0.2f; // 넥서스 기준 높이 보정
        [Min(0.01f)] public float VfxScale = 1f; // VFX 크기
        [Min(0.1f)] public float VfxLifetime = 4f; // VFX 제거 시간

        public bool CanUpgradeShieldVisual() // 실드 강화 가능 여부
        {
            EnsureNexusReferences(); // 넥서스/보호막 참조
            return Nexus != null
                && !Nexus.IsDead
                && NexusVfx != null
                && ShieldIncreasePerUpgrade > 0
                && ManaWallYScaleIncrease > 0f
                && NexusVfx.HasManaWallShieldEffect(); // Nexus_ManaWall_Shield 존재
        }

        public bool Play(int upgradeLevel) // 액션 HUD 5번 발동
        {
            EnsureReferences(); // 런타임 참조 보장

            if (!CanUpgradeShieldVisual())
            {
                Debug.LogWarning("[GoldActionShieldUpgrade] Nexus 또는 Nexus_ManaWall_Shield를 찾지 못해 실드 강화를 적용할 수 없습니다.", this);
                return false; // 적용 실패
            }

            bool applied = NexusVfx.AddManaWallShieldYScale(ManaWallYScaleIncrease); // 보호막 높이 증가
            if (!applied)
            {
                Debug.LogWarning("[GoldActionShieldUpgrade] Nexus_ManaWall_Shield를 찾지 못했습니다.", this);
                return false; // 골드 소모 후 호출되지 않도록 실패
            }

            Nexus.IncreaseMaxShield(ShieldIncreasePerUpgrade, FillAddedShield); // 실제 실드 수치 증가
            SpawnUpgradeVfx(); // 강화 VFX
            Debug.Log($"[GoldActionShieldUpgrade] Shield upgrade: Lv{Mathf.Max(1, upgradeLevel)}, +Shield {ShieldIncreasePerUpgrade}, +Y {ManaWallYScaleIncrease:0.##}", this);
            return true; // 발동 성공
        }

        private void SpawnUpgradeVfx() // VFX 생성
        {
            GameObject prefab = ResolveUpgradeVfxPrefab(); // VFX prefab
            if (prefab == null || Nexus == null)
            {
                Debug.LogWarning("[GoldActionShieldUpgrade] VFX_Heal_Cast prefab을 찾지 못했습니다.", this);
                return; // VFX 없음
            }

            Vector3 position = Nexus.transform.position + Vector3.up * VfxYOffset; // 넥서스 위치
            Transform root = EffectRoot != null ? EffectRoot : transform; // 생성 루트
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, root); // VFX 생성
            instance.name = "Nexus_ShieldUpgrade_Cast_VFX"; // 런타임 이름
            instance.transform.localScale = Vector3.one * VfxScale; // 크기 보정
            DisableRuntimeColliders(instance); // 충돌 방지
            PlayParticles(instance); // 즉시 재생
            Destroy(instance, VfxLifetime); // 자동 제거
        }

        private GameObject ResolveUpgradeVfxPrefab() // VFX prefab 찾기
        {
            if (UpgradeVfxPrefab != null)
            {
                return UpgradeVfxPrefab; // 인스펙터 우선
            }

            UpgradeVfxPrefab = Resources.Load<GameObject>(DefaultVfxResourcePath); // Resources fallback
            return UpgradeVfxPrefab; // 결과
        }

        private void EnsureReferences() // 참조 자동 보강
        {
            EnsureNexusReferences(); // 넥서스 참조
            EnsureEffectRoot(); // VFX 루트
        }

        private void EnsureNexusReferences() // 넥서스/보호막 참조 보강
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

            if (NexusVfx == null && Nexus != null)
            {
                NexusVfx = Nexus.GetComponent<NexusVfxController>(); // 부착 VFX
            }
        }

        private void EnsureEffectRoot() // VFX 루트 보강
        {
            if (EffectRoot == null)
            {
                GameObject root = GameObject.Find("GoldActionNexusShieldUpgradeRoot"); // 기존 루트
                if (root == null)
                {
                    root = new GameObject("GoldActionNexusShieldUpgradeRoot"); // 새 루트
                }

                EffectRoot = root.transform; // 루트 저장
            }
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
