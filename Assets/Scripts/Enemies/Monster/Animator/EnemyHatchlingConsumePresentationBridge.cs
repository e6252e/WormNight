using System.Collections;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class EnemyHatchlingConsumePresentationBridge : MonoBehaviour // 해츨링 포식 연출을 담당하는 Bridge
    {
        private const string BurrowStateName = "Eat_Burrow"; // 땅속으로 숨는 Animator State 이름
        private const string SpawnStateName = "Eat_Spawn"; // 대상 근처에서 나타나는 Animator State 이름
        private const string BiteStateName = "Eat_Bite"; // 먹는 Animator State 이름

        [Header("Animator")]
        [SerializeField] private Animator animator; // 포식 애니메이션을 재생할 Animator

        [SerializeField] private string consumingBoolName = "IsConsuming"; // 포식 중 Hit 전환을 막기 위한 Animator Bool 이름
        [SerializeField] private string hitTriggerName = "Hit"; // 포식 중 쌓인 Hit Trigger를 지우기 위한 Animator Trigger 이름

        [Header("Visual Offset")]
        [SerializeField] private Transform visualOffsetRoot; // Burrow와 Spawn 때 모델만 위아래로 움직일 시각 루트

        [Min(0.0f)]
        [SerializeField] private float burrowVisualDownOffset = 0.8f; // Burrow 때 모델을 추가로 아래로 내릴 거리

        [Min(0.0f)]
        [SerializeField] private float burrowAnimationDuration = 1.2f; // Burrow 연출 시간

        [Min(0.0f)]
        [SerializeField] private float spawnAnimationDuration = 1.2f; // Spawn 연출 시간

        [Min(0.0f)]
        [SerializeField] private float biteVfxDelay = 0.2f; // Bite 시작 후 Consume VFX가 나오기까지의 시간

        [Header("Burrow / Spawn Dust VFX")]
        [SerializeField] private GameObject dustVfxPrefab; // Burrow와 Spawn 때 사용할 먼지 VFX Prefab

        [Min(0.0f)]
        [SerializeField] private float dustVfxHeightOffset = 0.0f; // 먼지 VFX 높이 보정

        [Min(0.1f)]
        [SerializeField] private float burrowDustVfxScale = 1.0f; // Burrow 먼지 VFX 크기

        [Min(0.1f)]
        [SerializeField] private float spawnDustVfxScale = 1.2f; // Spawn 먼지 VFX 크기

        [Min(0.01f)]
        [SerializeField] private float dustVfxLifeTime = 1.5f; // 먼지 VFX 제거 시간

        [Header("Consume VFX")]
        [SerializeField] private GameObject consumeVfxPrefab; // 먹는 순간 대상과 해츨링 사이에 생성할 VFX

        [Min(0.0f)]
        [SerializeField] private float consumeVfxHeightOffset = 0.0f; // 먹기 VFX 높이 보정

        [Min(0.1f)]
        [SerializeField] private float consumeVfxScale = 1.2f; // 먹기 VFX 크기

        [Min(0.01f)]
        [SerializeField] private float consumeVfxLifeTime = 1.5f; // 먹기 VFX 제거 시간

        [Header("Growth VFX")]
        [SerializeField] private GameObject growthVfxPrefab; // 성장 성공 순간 생성할 VFX

        [SerializeField] private bool attachGrowthVfxToOwner = true; // 성장 VFX를 해츨링 자식으로 붙여 따라다니게 할지

        [Min(-5.0f)]
        [SerializeField] private float growthVfxHeightOffset = -0.5f; // 성장 VFX 높이 보정

        [Min(0.1f)]
        [SerializeField] private float growthVfxScale = 1.0f; // 성장 VFX 크기

        [Min(0.01f)]
        [SerializeField] private float growthVfxLifeTime = 2.0f; // 성장 VFX 제거 시간

        private Vector3 visualOffsetBaseLocalPosition; // VisualOffsetRoot의 원래 Local Position
        private int consumingBoolHash; // IsConsuming Animator Bool Hash
        private int hitTriggerHash; // Hit Animator Trigger Hash
        private bool hasConsumingBool; // Animator에 IsConsuming Bool이 있는지 여부
        private bool hasHitTrigger; // Animator에 Hit Trigger가 있는지 여부
        private bool isConsuming; // 현재 포식 연출 중인지 Bridge 내부에서 저장하는 값

        public float BiteVfxDelay // EnemyHatchlingGrowth가 Bite 후 VFX 타이밍을 읽기 위한 property
        {
            get
            {
                return biteVfxDelay; // Bite VFX 지연 시간을 반환한다.
            }
        }

        private void Awake()
        {
            if (animator == null) // Inspector에서 Animator가 연결되지 않았다면
            {
                animator = GetComponentInChildren<Animator>(true); // 자식 모델에서 Animator를 자동으로 찾는다.
            }

            if (visualOffsetRoot == null) // Inspector에서 VisualOffsetRoot가 연결되지 않았다면
            {
                Transform foundVisualOffsetRoot = transform.Find("VisualOffsetRoot"); // 자식에서 VisualOffsetRoot를 찾는다.

                if (foundVisualOffsetRoot != null) // VisualOffsetRoot를 찾았다면
                {
                    visualOffsetRoot = foundVisualOffsetRoot; // 시각 루트로 저장한다.
                }
            }

            if (visualOffsetRoot != null) // 시각 루트가 있다면
            {
                visualOffsetBaseLocalPosition = visualOffsetRoot.localPosition; // 원래 Local Position을 저장한다.
            }

            consumingBoolHash = Animator.StringToHash(consumingBoolName); // Animator Bool 이름을 Hash로 저장한다.
            hitTriggerHash = Animator.StringToHash(hitTriggerName); // Animator Trigger 이름을 Hash로 저장한다.
            hasConsumingBool = HasAnimatorParameter(consumingBoolName, AnimatorControllerParameterType.Bool); // Animator에 IsConsuming Bool Parameter가 있는지 확인한다.
            hasHitTrigger = HasAnimatorParameter(hitTriggerName, AnimatorControllerParameterType.Trigger); // Animator에 Hit Trigger Parameter가 있는지 확인한다.
        }

        private void OnEnable()
        {
            ResetVisualOffset(); // 활성화될 때 모델 높이를 원래대로 복구한다.
            SetConsuming(false); // 활성화될 때 Animator 포식 상태를 false로 초기화한다.
        }

        private void OnDisable()
        {
            ResetVisualOffset(); // 비활성화될 때 모델 높이를 원래대로 복구한다.
            SetConsuming(false); // 비활성화될 때 Animator 포식 상태를 false로 초기화한다.
        }

        private void Update()
        {
            if (!isConsuming) // 포식 중이 아니라면
            {
                return; // Hit Trigger를 지울 필요가 없다.
            }

            ResetHitTriggerWhileConsuming(); // 포식 중 쌓이는 Hit Trigger를 계속 제거한다.
        }

        public void SetConsuming(bool consuming) // 포식 중 상태를 Animator Bool로 전달한다.
        {
            isConsuming = consuming; // Bridge 내부 포식 상태를 저장한다.

            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // 처리하지 않는다.
            }

            if (hasConsumingBool) // Animator에 IsConsuming Bool이 있다면
            {
                animator.SetBool(consumingBoolHash, consuming); // Animator에 포식 상태를 전달한다.
            }

            if (consuming) // 포식 시작 상태라면
            {
                ResetHitTriggerWhileConsuming(); // 이미 쌓여 있던 Hit Trigger를 바로 제거한다.
            }
        }

        public IEnumerator PlayBurrow(Vector3 position) // Burrow 먼지 VFX와 Burrow 애니메이션을 재생한다.
        {
            ResetVisualOffset(); // Burrow 시작 전 모델 높이를 원래대로 맞춘다.
            SpawnOneShotVfx(dustVfxPrefab, position, dustVfxHeightOffset, burrowDustVfxScale, dustVfxLifeTime); // 현재 위치에 먼지 VFX를 생성한다.
            TryPlayAnimatorState(BurrowStateName); // Burrow 애니메이션을 재생한다.

            if (burrowAnimationDuration > 0.0f) // Burrow 시간이 있다면
            {
                yield return MoveVisualOffsetY(0.0f, -burrowVisualDownOffset, burrowAnimationDuration); // 모델만 아래로 내려 땅속으로 숨긴다.
            }
            else
            {
                SetVisualOffsetY(-burrowVisualDownOffset); // 시간이 없다면 바로 아래로 내린다.
            }
        }

        public IEnumerator PlaySpawn(Vector3 position) // Spawn 먼지 VFX와 Spawn 애니메이션을 재생한다.
        {
            SpawnOneShotVfx(dustVfxPrefab, position, dustVfxHeightOffset, spawnDustVfxScale, dustVfxLifeTime); // 나타난 위치에 먼지 VFX를 생성한다.
            TryPlayAnimatorState(SpawnStateName); // Spawn 애니메이션을 재생한다.

            if (spawnAnimationDuration > 0.0f) // Spawn 시간이 있다면
            {
                yield return MoveVisualOffsetY(-burrowVisualDownOffset, 0.0f, spawnAnimationDuration); // 모델을 원래 높이로 올린다.
            }
            else
            {
                SetVisualOffsetY(0.0f); // 시간이 없다면 바로 원래 높이로 복구한다.
            }
        }

        public void PlayBite() // Bite 애니메이션을 재생한다.
        {
            TryPlayAnimatorState(BiteStateName); // Bite 애니메이션을 재생한다.
        }

        public void SpawnConsumeVfx(Vector3 ownerPosition, Vector3 targetPosition) // 먹는 순간 VFX를 생성한다.
        {
            Vector3 spawnPosition = Vector3.Lerp(ownerPosition, targetPosition, 0.5f); // 해츨링과 대상 사이 중간 위치를 계산한다.
            SpawnOneShotVfx(consumeVfxPrefab, spawnPosition, consumeVfxHeightOffset, consumeVfxScale, consumeVfxLifeTime); // 먹기 VFX를 생성한다.
        }

        public void SpawnGrowthVfx(Transform owner) // 성장 성공 VFX를 생성한다.
        {
            if (owner == null) // 해츨링 Transform이 없다면
            {
                return; // 생성하지 않는다.
            }

            if (attachGrowthVfxToOwner) // 성장 VFX를 해츨링에게 붙여야 한다면
            {
                SpawnAttachedVfx(growthVfxPrefab, owner, growthVfxHeightOffset, growthVfxScale, growthVfxLifeTime); // 해츨링 자식으로 VFX를 생성한다.
                return; // 월드 고정 VFX는 생성하지 않는다.
            }

            SpawnOneShotVfx(growthVfxPrefab, owner.position, growthVfxHeightOffset, growthVfxScale, growthVfxLifeTime); // 해츨링 위치에 월드 VFX를 생성한다.
        }

        public void ResetVisualOffset() // VisualOffsetRoot를 원래 높이로 복구한다.
        {
            SetVisualOffsetY(0.0f); // 원래 높이로 맞춘다.
        }

        private IEnumerator MoveVisualOffsetY(float fromOffsetY, float toOffsetY, float duration) // VisualOffsetRoot를 부드럽게 위아래로 이동시킨다.
        {
            if (visualOffsetRoot == null) // 시각 루트가 없다면
            {
                yield return new WaitForSeconds(duration); // 기존 시간만큼만 기다린다.
                yield break; // 이동을 끝낸다.
            }

            if (duration <= 0.0f) // 이동 시간이 없다면
            {
                SetVisualOffsetY(toOffsetY); // 목표 높이로 바로 맞춘다.
                yield break; // 이동을 끝낸다.
            }

            float timer = 0.0f; // 시간 누적값

            while (timer < duration) // 지정 시간 동안 반복한다.
            {
                timer += Time.deltaTime; // 지난 시간을 더한다.
                float t = Mathf.Clamp01(timer / duration); // 진행률을 계산한다.
                float currentOffsetY = Mathf.Lerp(fromOffsetY, toOffsetY, t); // 현재 높이를 계산한다.
                SetVisualOffsetY(currentOffsetY); // 현재 높이를 적용한다.
                yield return null; // 다음 프레임까지 기다린다.
            }

            SetVisualOffsetY(toOffsetY); // 마지막에는 정확히 목표 높이로 맞춘다.
        }

        private void SetVisualOffsetY(float offsetY) // VisualOffsetRoot의 Local Y 위치를 설정한다.
        {
            if (visualOffsetRoot == null) // 시각 루트가 없다면
            {
                return; // 처리하지 않는다.
            }

            Vector3 localPosition = visualOffsetBaseLocalPosition; // 원래 Local Position을 기준으로 사용한다.
            localPosition.y += offsetY; // 추가 높이를 더한다.
            visualOffsetRoot.localPosition = localPosition; // 위치를 적용한다.
        }

        private bool TryPlayAnimatorState(string stateName) // Animator State를 직접 재생한다.
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return false; // 재생하지 않는다.
            }

            if (string.IsNullOrEmpty(stateName)) // State 이름이 비어 있다면
            {
                return false; // 재생하지 않는다.
            }

            int stateHash = Animator.StringToHash(stateName); // State 이름을 Hash로 변환한다.

            if (!animator.HasState(0, stateHash)) // Base Layer에 해당 State가 없다면
            {
                return false; // 재생하지 않는다.
            }

            animator.Play(stateHash, 0, 0.0f); // State를 처음부터 재생한다.
            animator.Update(0.0f); // 같은 프레임에 바로 반영한다.

            return true; // 재생 성공
        }

        private void SpawnOneShotVfx(GameObject prefab, Vector3 position, float heightOffset, float scaleMultiplier, float lifeTime) // 단발성 VFX를 생성한다.
        {
            if (prefab == null) // VFX Prefab이 없다면
            {
                return; // 생성하지 않는다.
            }

            Vector3 spawnPosition = position; // 생성 위치를 복사한다.
            spawnPosition.y += heightOffset; // 높이 보정값을 더한다.

            GameObject vfx = Instantiate(prefab, spawnPosition, Quaternion.identity, MonsterRuntimeRoot.GetRootOrFallback(transform.parent)); // Runtime Root 아래에 VFX를 생성한다.
            vfx.transform.localScale = vfx.transform.localScale * scaleMultiplier; // 크기 배율을 적용한다.
            Destroy(vfx, lifeTime); // 지정 시간 뒤 제거한다.
        }

        private void SpawnAttachedVfx(GameObject prefab, Transform parent, float heightOffset, float scaleMultiplier, float lifeTime) // 부모를 따라다니는 VFX를 생성한다.
        {
            if (prefab == null) // VFX Prefab이 없다면
            {
                return; // 생성하지 않는다.
            }

            if (parent == null) // 부모가 없다면
            {
                return; // 생성하지 않는다.
            }

            GameObject vfx = Instantiate(prefab, parent); // 부모 자식으로 VFX를 생성한다.
            vfx.transform.localPosition = Vector3.up * heightOffset; // 부모 기준 높이를 설정한다.
            vfx.transform.localRotation = Quaternion.identity; // 부모 기준 회전을 초기화한다.
            vfx.transform.localScale = vfx.transform.localScale * scaleMultiplier; // 크기 배율을 적용한다.

            ForceParticleSimulationSpace(vfx, ParticleSystemSimulationSpace.Local); // 부모를 따라다니도록 Simulation Space를 Local로 맞춘다.

            Destroy(vfx, lifeTime); // 지정 시간 뒤 제거한다.
        }

        private void ForceParticleSimulationSpace(GameObject vfx, ParticleSystemSimulationSpace simulationSpace) // ParticleSystem Simulation Space를 변경한다.
        {
            ParticleSystem[] particleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true); // 자식 ParticleSystem을 모두 찾는다.

            for (int i = 0; i < particleSystems.Length; i++) // 모든 ParticleSystem을 순회한다.
            {
                ParticleSystem.MainModule main = particleSystems[i].main; // Main Module을 가져온다.
                main.simulationSpace = simulationSpace; // Simulation Space를 설정한다.
            }
        }

        private void ResetHitTriggerWhileConsuming() // 포식 중 Hit Trigger가 남아 있지 않도록 제거한다.
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return; // 처리하지 않는다.
            }

            if (!hasHitTrigger) // Hit Trigger가 없다면
            {
                return; // 처리하지 않는다.
            }

            animator.ResetTrigger(hitTriggerHash); // Hit Trigger를 제거한다.
        }

        private bool CanUseAnimator() // Animator 사용 가능 여부를 확인한다.
        {
            if (animator == null) // Animator가 없다면
            {
                return false; // 사용할 수 없다.
            }

            if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) // Animator가 정상 상태가 아니라면
            {
                return false; // 사용할 수 없다.
            }

            return true; // 사용할 수 있다.
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType) // Animator Parameter가 있는지 확인한다.
        {
            if (!CanUseAnimator()) // Animator를 사용할 수 없다면
            {
                return false; // 확인할 수 없다.
            }

            if (string.IsNullOrEmpty(parameterName)) // Parameter 이름이 비어 있다면
            {
                return false; // 사용할 수 없다.
            }

            for (int i = 0; i < animator.parameterCount; i++) // Animator Parameter 목록을 순회한다.
            {
                AnimatorControllerParameter parameter = animator.parameters[i]; // 현재 Parameter를 가져온다.

                if (parameter.name == parameterName && parameter.type == parameterType) // 이름과 타입이 일치한다면
                {
                    return true; // Parameter가 있다.
                }
            }

            return false; // Parameter를 찾지 못했다.
        }
    }
}