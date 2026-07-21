using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NexusVfxController : MonoBehaviour // 넥서스 부착형 VFX 제어
    {
        public const string LightningGroundName = "Nexus_LightningStrike_Ground";
        public const string ManaWallShieldName = "Nexus_ManaWall_Shield";

        [SerializeField] private NexusController nexus; // 상태 기준 넥서스
        [SerializeField] private GameObject lightningGroundEffect; // 넥서스 바닥 번개
        [SerializeField] private GameObject manaWallShieldEffect; // 넥서스 보호막 벽
        [SerializeField] private bool showLightningGroundEffect = true; // 바닥 번개 표시
        [SerializeField] private bool showManaWallWhileShielded = true; // 보호막 중 마나월 표시

        private Coroutine shieldPulseRoutine; // 보호막 확장 연출
        private Vector3 shieldBaseScale; // 보호막 기본 스케일
        private bool hasShieldBaseScale; // 기본 스케일 기록 여부

        public void Configure(
            NexusController owner,
            GameObject lightningGround,
            GameObject manaWallShield,
            bool showLightning,
            bool showManaWall) // 외부 설정
        {
            nexus = owner != null ? owner : nexus; // 기준 넥서스
            lightningGroundEffect = lightningGround != null ? lightningGround : lightningGroundEffect; // 번개
            manaWallShieldEffect = manaWallShield != null ? manaWallShield : manaWallShieldEffect; // 마나월
            showLightningGroundEffect = showLightning; // 번개 표시
            showManaWallWhileShielded = showManaWall; // 마나월 표시
            PrepareEffects(); // 장식용 보정
            RefreshVisibility(); // 표시 갱신
        }

        private void Awake() // 초기화
        {
            ResolveReferences(); // 참조 찾기
            PrepareEffects(); // 장식용 보정
        }

        private void OnEnable() // 이벤트 연결
        {
            ResolveReferences(); // 참조 보강
            if (nexus != null)
            {
                nexus.ShieldChanged += OnNexusShieldChanged; // 실드 변화
                nexus.StateChanged += OnNexusStateChanged; // 전체 상태 변화
            }

            RefreshVisibility(); // 초기 표시
        }

        private void OnDisable() // 이벤트 해제
        {
            if (nexus != null)
            {
                nexus.ShieldChanged -= OnNexusShieldChanged; // 실드 변화
                nexus.StateChanged -= OnNexusStateChanged; // 전체 상태 변화
            }
        }

        private void Start() // 시작 동기화
        {
            PrepareEffects(); // 파티클 보정
            RefreshVisibility(); // 표시 동기화
        }

        private void Update() // 상태 보정
        {
            RefreshVisibility(); // 보호막 상태 반영
        }

        private void ResolveReferences() // 씬 부착 오브젝트 찾기
        {
            if (nexus == null)
            {
                nexus = GetComponent<NexusController>(); // 같은 오브젝트
            }

            if (lightningGroundEffect == null)
            {
                Transform lightning = transform.Find(LightningGroundName); // 번개 자식
                lightningGroundEffect = lightning != null ? lightning.gameObject : null; // 참조
            }

            if (manaWallShieldEffect == null)
            {
                Transform manaWall = transform.Find(ManaWallShieldName); // 마나월 자식
                manaWallShieldEffect = manaWall != null ? manaWall.gameObject : null; // 참조
            }
        }

        private void PrepareEffects() // 장식용 VFX 보정
        {
            DisableRuntimeColliders(lightningGroundEffect); // 번개 충돌 제거
            DisableRuntimeColliders(manaWallShieldEffect); // 마나월 충돌 제거
            ConfigureParticles(lightningGroundEffect, true); // 번개 반복
            ConfigureParticles(manaWallShieldEffect, true); // 마나월 반복
        }

        private void RefreshVisibility() // 표시 상태 반영
        {
            bool showLightning = showLightningGroundEffect; // 번개 상시 표시
            bool pulseActive = shieldPulseRoutine != null; // 충격파 연출 중
            bool showManaWall = pulseActive || showManaWallWhileShielded && nexus != null && !nexus.IsDead && nexus.CurrentShield > 0; // 보호막 중 표시

            SetEffectVisible(lightningGroundEffect, showLightning); // 번개
            SetEffectVisible(manaWallShieldEffect, showManaWall); // 마나월
        }

        public void PlayShieldShockwavePulse(float xzMultiplier, float yMultiplier, float expandDuration, float holdDuration, float returnDuration) // 보호막 확장 연출
        {
            ResolveReferences(); // 참조 보강
            if (manaWallShieldEffect == null)
            {
                return; // 연출 대상 없음
            }

            CaptureShieldBaseScale(); // 기본 스케일 저장
            if (shieldPulseRoutine != null)
            {
                StopCoroutine(shieldPulseRoutine); // 기존 연출 중단
                ResetShieldPulseScale(); // 스케일 복구
            }

            shieldPulseRoutine = StartCoroutine(ShieldShockwavePulseRoutine(
                Mathf.Max(1f, xzMultiplier),
                Mathf.Max(0.01f, yMultiplier),
                Mathf.Max(0.01f, expandDuration),
                Mathf.Max(0f, holdDuration),
                Mathf.Max(0.01f, returnDuration))); // 새 연출 시작
        }

        public bool HasManaWallShieldEffect() // 보호막 VFX 존재 여부
        {
            ResolveReferences(); // 참조 보강
            return manaWallShieldEffect != null; // 자식 오브젝트 확인
        }

        public bool AddManaWallShieldYScale(float yIncrease) // 보호막 높이 강화
        {
            ResolveReferences(); // 참조 보강
            if (manaWallShieldEffect == null || yIncrease <= 0f)
            {
                return false; // 처리 없음
            }

            CaptureShieldBaseScale(); // 기준 스케일 확보
            shieldBaseScale = new Vector3(shieldBaseScale.x, shieldBaseScale.y + yIncrease, shieldBaseScale.z); // 기본 높이 증가
            if (shieldPulseRoutine == null)
            {
                manaWallShieldEffect.transform.localScale = shieldBaseScale; // 현재 스케일도 즉시 반영
            }

            SetEffectVisible(manaWallShieldEffect, true); // 강화 시 보호막 표시
            RefreshVisibility(); // 표시 상태 갱신
            return true; // 성공
        }

        private IEnumerator ShieldShockwavePulseRoutine(float xzMultiplier, float yMultiplier, float expandDuration, float holdDuration, float returnDuration) // 보호막 팽창 코루틴
        {
            Transform shield = manaWallShieldEffect.transform; // 연출 대상
            Vector3 startScale = shieldBaseScale; // 기본 크기
            Vector3 peakScale = new Vector3(startScale.x * xzMultiplier, startScale.y * yMultiplier, startScale.z * xzMultiplier); // 확장 크기

            SetEffectVisible(manaWallShieldEffect, true); // 충격파 중 표시
            yield return AnimateShieldScale(shield, startScale, peakScale, expandDuration); // 빠른 확장

            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration); // 짧은 유지
            }

            yield return AnimateShieldScale(shield, peakScale, startScale, returnDuration); // 복귀
            ResetShieldPulseScale(); // 스케일 보정
            shieldPulseRoutine = null; // 완료
            RefreshVisibility(); // 보호막 상태 반영
        }

        private IEnumerator AnimateShieldScale(Transform shield, Vector3 from, Vector3 to, float duration) // 스케일 보간
        {
            float elapsed = 0f; // 진행 시간
            while (elapsed < duration && shield != null)
            {
                elapsed += Time.deltaTime; // 시간 누적
                float t = Mathf.Clamp01(elapsed / duration); // 진행률
                float eased = 1f - (1f - t) * (1f - t); // 빠르게 퍼지는 곡선
                shield.localScale = Vector3.LerpUnclamped(from, to, eased); // 스케일 적용
                yield return null; // 다음 프레임
            }

            if (shield != null)
            {
                shield.localScale = to; // 최종값 보정
            }
        }

        private void CaptureShieldBaseScale() // 기본 스케일 기록
        {
            if (hasShieldBaseScale || manaWallShieldEffect == null)
            {
                return; // 이미 기록됨
            }

            shieldBaseScale = manaWallShieldEffect.transform.localScale; // 현재 배치값
            hasShieldBaseScale = true; // 기록 완료
        }

        private void ResetShieldPulseScale() // 보호막 스케일 복구
        {
            if (hasShieldBaseScale && manaWallShieldEffect != null)
            {
                manaWallShieldEffect.transform.localScale = shieldBaseScale; // 원래 크기
            }
        }

        private static void SetEffectVisible(GameObject instance, bool visible) // 표시 전환
        {
            if (instance == null || instance.activeSelf == visible)
            {
                return; // 변화 없음
            }

            instance.SetActive(visible); // 활성 전환
            if (visible)
            {
                PlayParticles(instance); // 다시 재생
            }
        }

        private static void ConfigureParticles(GameObject root, bool loop) // 파티클 반복 설정
        {
            if (root == null)
            {
                return; // 대상 없음
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true); // 하위 파티클
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i]; // 현재 파티클
                ParticleSystem.MainModule main = particle.main; // 메인 모듈
                main.loop = loop; // 반복 여부
                particle.Play(true); // 재생
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

        private static void DisableRuntimeColliders(GameObject root) // 장식 콜라이더 제거
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

        private void OnNexusShieldChanged(int current, int max) // 실드 변화
        {
            RefreshVisibility(); // 표시 갱신
        }

        private void OnNexusStateChanged(NexusController changedNexus) // 상태 변화
        {
            RefreshVisibility(); // 표시 갱신
        }
    }
}
