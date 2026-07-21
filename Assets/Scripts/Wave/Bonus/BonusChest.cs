using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class BonusChest : MonoBehaviour
    {
        [Header("상자 감지 설정")]
        [Tooltip("컨보이 머리가 이 거리 안으로 들어오면 상자가 열립니다.")]
        [InspectorName("열림 거리")]
        [Range(0.5f, 80.0f)]
        [SerializeField] private float openDistance = 10.0f; // 상자가 열리기 시작하는 거리입니다.

        [Tooltip("컨보이 머리가 이 거리 안으로 들어오고 보상 딜레이가 끝나면 보상 선택 화면이 열립니다.")]
        [InspectorName("보상 선택 거리")]
        [Range(0.2f, 30.0f)]
        [SerializeField] private float collectDistance = 3.0f; // 보상 선택 화면이 열리는 거리입니다.

        [Header("보상 선택 설정")]
        [Tooltip("이 상자가 열 보상 선택 카드의 레어 등장 확률 추가값입니다.")]
        [InspectorName("보상 레어 확률 보너스(%)")]
        [Range(0.0f, 100.0f)]
        [SerializeField] private float rewardChoiceRareChanceBonusPercent; // 보상 카드 레어 확률 추가값입니다.

        [Tooltip("이 상자가 열 보상 선택 카드의 유니크 등장 확률 추가값입니다.")]
        [InspectorName("보상 유니크 확률 보너스(%)")]
        [Range(0.0f, 100.0f)]
        [SerializeField] private float rewardChoiceUniqueChanceBonusPercent; // 보상 카드 유니크 확률 추가값입니다.

        [Tooltip("상자가 열린 뒤 보상 선택 화면이 열리기 전까지 기다리는 시간입니다.")]
        [InspectorName("보상 선택 딜레이(초)")]
        [Range(0.0f, 5.0f)]
        [SerializeField] private float rewardDropDelay = 0.4f; // 상자 열림 연출을 보여주기 위한 딜레이입니다.

        [Header("애니메이션 설정")]
        [Tooltip("상자 열림 애니메이션을 재생할 Animator입니다. 비워두면 자식에서 자동으로 찾습니다.")]
        [InspectorName("상자 애니메이터")]
        [SerializeField] private Animator animator; // 상자 열림 애니메이션 담당입니다.

        [Tooltip("상자 열림 Trigger 이름입니다. 현재 상자처럼 Trigger가 없으면 비워둬도 됩니다.")]
        [InspectorName("열림 트리거 이름")]
        [SerializeField] private string openTriggerName = "Open"; // 선택적으로 사용할 Animator Trigger입니다.

        [Tooltip("상자 열림 애니메이션 재생 속도입니다. 2면 2배속입니다.")]
        [InspectorName("열림 애니메이션 속도")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float openAnimationSpeed = 2.0f; // 상자가 너무 느리게 열릴 때 조절합니다.

        [Tooltip("애니메이션을 몇 퍼센트 지점부터 재생할지 정합니다. 0이면 처음부터, 0.25면 25% 지점부터입니다.")]
        [InspectorName("열림 애니메이션 시작 지점")]
        [Range(0.0f, 0.95f)]
        [SerializeField] private float openAnimationStart = 0.0f; // 필요하면 초반 느린 구간을 건너뜁니다.

        [Tooltip("켜두면 생성 직후 Animator를 멈춰 상자가 자동으로 열리지 않게 합니다.")]
        [InspectorName("열리기 전 애니메이터 정지")]
        [SerializeField] private bool pauseAnimatorUntilOpen = true; // 스폰 직후 자동 재생을 막습니다.

        [Tooltip("켜두면 보상 선택 화면이 열린 뒤 상자 오브젝트를 제거합니다.")]
        [InspectorName("보상 후 상자 제거")]
        [SerializeField] private bool destroyAfterReward = true; // 보상 선택 화면이 열린 뒤 상자를 지웁니다.

        [Tooltip("보상 선택 화면이 열린 뒤 상자를 제거하기 전까지 기다리는 시간입니다.")]
        [InspectorName("제거 대기 시간(초)")]
        [Range(0.0f, 10.0f)]
        [SerializeField] private float destroyDelay = 2.0f; // 열린 상자를 잠깐 보여주기 위한 시간입니다.

        private bool opened; // 상자가 열렸는지 저장합니다.
        private bool rewarded; // 보상 선택 화면을 이미 열었는지 저장합니다.
        private float rewardReadyTime; // 이 시간 이후 보상 선택이 가능합니다.
        private ConvoyController cachedConvoy; // 컨보이 머리만 상자를 열 수 있게 하기 위한 캐시입니다.
        private BonusChestWaveSpawner ownerSpawner; // 같은 보너스 웨이브의 다른 상자를 정리하기 위한 스포너 참조입니다.
        private Transform choiceGroupRoot; // 같은 선택 그룹에 속한 상자들을 찾을 부모입니다.
        private bool allowOnlyOneChoice = true; // 한 상자만 열 수 있는지 저장합니다.
        private float unselectedChestDestroyDelay = 0.2f; // 선택되지 않은 상자 제거 대기 시간입니다.
        private global::CardUI cachedCardUi; // 보상 선택 화면 진입점 캐시입니다.
        private bool warnedMissingCardUi; // CardUI 누락 경고 반복 방지입니다.

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (animator != null && pauseAnimatorUntilOpen)
            {
                animator.enabled = false;
            }
        }

        private void Update()
        {
            Transform headTarget = ResolveHeadTarget();
            if (headTarget == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, headTarget.position);

            if (!opened && distance <= openDistance)
            {
                OpenChest();
            }

            if (opened && !rewarded && Time.time >= rewardReadyTime && distance <= collectDistance)
            {
                TryOpenRewardChoicePanel();
            }
        }

        public void ConfigureOwner(BonusChestWaveSpawner owner)
        {
            ownerSpawner = owner;
        }

        public void ConfigureChoiceGroup(Transform groupRoot, bool oneChoiceOnly, float removeDelay)
        {
            choiceGroupRoot = groupRoot;
            allowOnlyOneChoice = oneChoiceOnly;
            unselectedChestDestroyDelay = Mathf.Max(0.0f, removeDelay);
        }

        public void ConfigureRewardChoiceTierBonus(float rareChanceBonusPercent, float uniqueChanceBonusPercent)
        {
            rewardChoiceRareChanceBonusPercent = Mathf.Clamp(rareChanceBonusPercent, 0.0f, 100.0f);
            rewardChoiceUniqueChanceBonusPercent = Mathf.Clamp(uniqueChanceBonusPercent, 0.0f, 100.0f);
        }

        public void RemoveWithoutReward(float delay)
        {
            if (this == null)
            {
                return;
            }

            Destroy(gameObject, Mathf.Max(0.0f, delay));
        }

        private Transform ResolveHeadTarget()
        {
            if (!MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget))
            {
                return null;
            }

            if (cachedConvoy == null || cachedConvoy.transform != convoyTarget || !cachedConvoy.gameObject.activeInHierarchy)
            {
                cachedConvoy = convoyTarget.GetComponent<ConvoyController>();
            }

            if (cachedConvoy != null && cachedConvoy.HeadVisual != null && cachedConvoy.HeadVisual.gameObject.activeInHierarchy)
            {
                return cachedConvoy.HeadVisual;
            }

            return convoyTarget; // 테스트 환경에서 HeadVisual이 없을 때만 사용하는 안전장치입니다.
        }

        private void OpenChest()
        {
            if (ownerSpawner != null && !ownerSpawner.TrySelectChest(this))
            {
                return;
            }

            RemoveOtherChoiceChests();

            opened = true;
            rewardReadyTime = Time.time + Mathf.Max(0.0f, rewardDropDelay);
            PlayOpenSfx();

            if (animator == null)
            {
                return;
            }

            animator.enabled = true;
            animator.speed = Mathf.Max(0.1f, openAnimationSpeed);
            animator.Play(0, 0, Mathf.Clamp01(openAnimationStart));

            if (!string.IsNullOrWhiteSpace(openTriggerName) && HasAnimatorTrigger(openTriggerName))
            {
                animator.SetTrigger(openTriggerName);
            }
        }

        private bool HasAnimatorTrigger(string triggerName)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayOpenSfx()
        {
            if (GameplaySfxEmitter.TryPlay(transform, GameplaySfxCue.Open))
            {
                return;
            }

            GameplaySfxEmitter.TryPlayCatalogAt(GameplaySfxCue.Open, transform.position);
        }

        private void RemoveOtherChoiceChests()
        {
            if (!allowOnlyOneChoice)
            {
                return;
            }

            Transform root = choiceGroupRoot != null ? choiceGroupRoot : transform.parent;
            if (root == null)
            {
                return;
            }

            BonusChest[] chests = root.GetComponentsInChildren<BonusChest>(true);
            for (int i = 0; i < chests.Length; i++)
            {
                BonusChest chest = chests[i];
                if (chest == null || chest == this)
                {
                    continue;
                }

                chest.RemoveWithoutReward(unselectedChestDestroyDelay);
            }
        }

        private bool TryOpenRewardChoicePanel()
        {
            global::CardUI cardUi = ResolveCardUi();
            if (cardUi == null)
            {
                if (!warnedMissingCardUi)
                {
                    warnedMissingCardUi = true;
                    Debug.LogWarning("[BonusChest] CardUI가 없어 보상 선택 화면을 열 수 없습니다.", this);
                }

                return false;
            }

            if (!cardUi.OpenRewardChoice(rewardChoiceRareChanceBonusPercent, rewardChoiceUniqueChanceBonusPercent))
            {
                return false; // 다른 카드 화면이 열려 있으면 다음 프레임/재진입 때 재시도
            }

            rewarded = true;

            if (destroyAfterReward)
            {
                Destroy(gameObject, destroyDelay);
            }

            return true;
        }

        private global::CardUI ResolveCardUi()
        {
            if (cachedCardUi != null && cachedCardUi.gameObject.activeInHierarchy)
            {
                return cachedCardUi; // 캐시 사용
            }

            cachedCardUi = FindFirstObjectByType<global::CardUI>();
            return cachedCardUi;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, openDistance);

            Gizmos.color = new Color(1.0f, 0.8f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, collectDistance);
        }
    }
}
