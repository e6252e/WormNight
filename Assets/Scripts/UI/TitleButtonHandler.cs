using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class TitleButtonHandler : MonoBehaviour // 타이틀 버튼 호버·클릭 연출 //안건준 추가 - 0628
    {   [Header("버튼 루트")]
        [SerializeField] private Transform buttonRoot; // 비어 있으면 이 오브젝트 하위 버튼 전부 대상
        [Header("호버 배율")]
        [SerializeField] private float hoverScale = 1.09f; // CardUI와 동일한 호버 배율
        [Header("클릭 배율")]
        [SerializeField] private float clickScale = 1.2f; // CardUI 리롤 버튼 클릭 배율
        [Header("호버 스케일 전환 시간")]
        [SerializeField] private float hoverDuration = 0.15f; // 호버 스케일 전환 시간
        [Header("클릭 시 커지는 시간")]
        [SerializeField] private float clickUpSeconds = 0.15f; // 클릭 시 커지는 시간
        [Header("클릭 후 원래 크기로 돌아오는 시간")]
        [SerializeField] private float clickDownSeconds = 0.1f; // 클릭 후 원래 크기로 돌아오는 시간
        [Header("AudioManager SFX 실패 시 직접 재생용 (선택)")]
        [SerializeField] private AudioClip fallbackClickClip; // AudioManager SFX 실패 시 직접 재생용 (선택) //안건준 추가 - 0629

        private readonly HashSet<Button> registeredButtons = new HashSet<Button>();
        private readonly Dictionary<Button, bool> hoverStates = new Dictionary<Button, bool>();

        private void Awake()
        {
            if (buttonRoot == null)
            {
                buttonRoot = transform; // 기본은 자신 하위 버튼 탐색
            }
        }

        private void OnEnable()
        {
            RefreshButtons(); // 패널 전환 후 버튼 재등록 //안건준 수정 - 0629
        }

        private void Start()
        {
            RefreshButtons(); // 타이틀 UI의 모든 버튼 등록
        }

        public void RefreshButtons() // 런타임 생성 버튼까지 다시 등록할 때 호출
        {
            if (buttonRoot == null)
            {
                return;
            }

            Button[] buttons = buttonRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                RegisterButton(buttons[i]);
            }
        }

        private void RegisterButton(Button button)
        {
            if (button == null || !registeredButtons.Add(button))
            {
                return;
            }

            TitleButtonHoverBridge bridge = button.GetComponent<TitleButtonHoverBridge>();
            if (bridge == null)
            {
                bridge = button.gameObject.AddComponent<TitleButtonHoverBridge>();
            }

            bridge.Initialize(this, button);
            button.onClick.AddListener(() => HandleButtonClicked(button)); // 클릭 시 효과음 + 클릭 모션
        }

        internal void SetHover(Button button, bool active)
        {
            if (hoverStates.TryGetValue(button, out bool current) && current == active)
            {
                return;
            }

            hoverStates[button] = active;
            RectTransform rt = button.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            rt.DOKill();
            rt.DOScale(active ? Vector3.one * hoverScale : Vector3.one, hoverDuration)
                .SetEase(active ? Ease.OutQuad : Ease.InQuad)
                .SetUpdate(true);
        }

        private void HandleButtonClicked(Button button)
        {
            PlayTitleButtonClickSfx(); // 클릭 효과음 //안건준 수정 - 0629

            PlayClickTween(button);
        }

        private void PlayTitleButtonClickSfx()
        {
            AudioManager manager = AudioManager.EnsureExists();
            if (manager != null
                && manager.TryGetSfxClip(SFXType.ClickButton, out AudioClip clip, out float localVolume)
                && manager.GetEffectiveSfxVolume(localVolume) > 0.0001f)
            {
                manager.PlaySfxOneShotDirect(clip, localVolume);
                return;
            }

            if (fallbackClickClip != null)
            {
                AudioManager.PlayUiSfxClip(fallbackClickClip, 1f);
                return;
            }

            AudioManager.PlayClickButtonSfx(); // 실패 원인 Console 경고 출력
        }

        private void PlayClickTween(Button button)
        {
            RectTransform rt = button.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            hoverStates[button] = false;
            rt.DOKill();
            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(rt.DOScale(Vector3.one * clickScale, clickUpSeconds).SetEase(Ease.OutBack));
            seq.Append(rt.DOScale(Vector3.one, clickDownSeconds));
        }

        private sealed class TitleButtonHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private TitleButtonHandler handler;
            private Button button;

            public void Initialize(TitleButtonHandler owner, Button target)
            {
                handler = owner;
                button = target;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                handler?.SetHover(button, true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                handler?.SetHover(button, false);
            }
        }
    }
}
