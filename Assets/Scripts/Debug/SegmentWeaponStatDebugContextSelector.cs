using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    //전찬우 수정-0622
    public sealed class SegmentWeaponStatDebugContextSelector : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot; // 컨텍스트 선택 UI가 들어있는 디버그 패널
        [SerializeField] private global::CardUI cardUi; // 스탯 표시 갱신 담당
        [SerializeField] private RectTransform selectorRoot; // 선택기 루트 오브젝트
        [SerializeField] private LayoutElement selectorRootLayout; // 펼침 높이 조정
        [SerializeField] private Button selectorButton; // 리스트 열기/닫기 버튼
        [SerializeField] private Text selectorLabel; // 현재 선택 표시
        [SerializeField] private RectTransform listRoot; // 세그먼트 리스트 루트
        [SerializeField] private int maxVisibleOptions = 9; // 리스트 최대 표시 개수
        [SerializeField] private float selectorHeight = 24f; // 선택 버튼 높이
        [SerializeField] private float optionHeight = 22f; // 리스트 항목 높이

        private readonly List<OptionButtonView> optionViews = new List<OptionButtonView>(); // 씬에 배치된 선택 항목
        private string selectedSegmentId;
        private bool listOpen;
        private float nextRefreshTime;

        private readonly struct OptionButtonView
        {
            public readonly string SegmentId;
            public readonly string DisplayName;
            public readonly Image Background;

            public OptionButtonView(string segmentId, string displayName, Image background)
            {
                SegmentId = string.IsNullOrWhiteSpace(segmentId) ? string.Empty : segmentId.Trim();
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? SegmentId : displayName.Trim();
                Background = background;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            WireButtons();
            ApplyListOpen(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            WireButtons();
            SyncSelectedFromCardUi();
            RefreshLabelAndHighlights();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.5f;
            SyncSelectedFromCardUi();
            RefreshLabelAndHighlights();
        }

        private void OnDestroy()
        {
            if (selectorButton != null)
            {
                selectorButton.onClick.RemoveListener(ToggleList);
            }

            if (listRoot == null)
            {
                return;
            }

            SegmentWeaponStatDebugContextOption[] options = listRoot.GetComponentsInChildren<SegmentWeaponStatDebugContextOption>(true);
            for (int i = 0; i < options.Length; i++)
            {
                SegmentWeaponStatDebugContextOption option = options[i];
                if (option != null && option.Button != null)
                {
                    option.Button.onClick.RemoveAllListeners();
                }
            }
        }

        private void ResolveReferences()
        {
            if (cardUi == null)
            {
                cardUi = FindFirstObjectByType<global::CardUI>();
            }

            if (panelRoot == null)
            {
                panelRoot = FindDebugPanelRoot();
            }

            if (selectorRoot == null)
            {
                selectorRoot = transform as RectTransform;
            }

            if (selectorRootLayout == null && selectorRoot != null)
            {
                selectorRootLayout = selectorRoot.GetComponent<LayoutElement>();
            }

            if (selectorButton == null && selectorRoot != null)
            {
                Transform buttonTransform = selectorRoot.Find("ContextButton");
                selectorButton = buttonTransform != null ? buttonTransform.GetComponent<Button>() : selectorRoot.GetComponentInChildren<Button>(true);
            }

            if (selectorLabel == null && selectorButton != null)
            {
                selectorLabel = selectorButton.GetComponentInChildren<Text>(true);
            }

            if (listRoot == null && selectorRoot != null)
            {
                listRoot = selectorRoot.Find("List") as RectTransform;
            }
        }

        private void WireButtons()
        {
            if (selectorButton != null)
            {
                selectorButton.onClick.RemoveListener(ToggleList);
                selectorButton.onClick.AddListener(ToggleList);
            }

            optionViews.Clear();
            if (listRoot == null)
            {
                return;
            }

            SegmentWeaponStatDebugContextOption[] options = listRoot.GetComponentsInChildren<SegmentWeaponStatDebugContextOption>(true);
            for (int i = 0; i < options.Length; i++)
            {
                SegmentWeaponStatDebugContextOption option = options[i];
                if (option == null || string.IsNullOrWhiteSpace(option.SegmentId) || option.Button == null)
                {
                    continue;
                }

                string capturedId = option.SegmentId.Trim();
                option.Button.onClick.RemoveAllListeners();
                option.Button.onClick.AddListener(() => SelectContext(capturedId));
                option.RefreshLabel();
                optionViews.Add(new OptionButtonView(option.SegmentId, option.DisplayName, option.Background));
            }
        }

        private void ToggleList()
        {
            ApplyListOpen(!listOpen);
        }

        private void ApplyListOpen(bool open)
        {
            listOpen = open && optionViews.Count > 0;
            if (listRoot != null)
            {
                listRoot.gameObject.SetActive(listOpen);
                LayoutElement listLayout = listRoot.GetComponent<LayoutElement>();
                if (listLayout != null)
                {
                    int visibleCount = Mathf.Min(optionViews.Count, Mathf.Max(1, maxVisibleOptions));
                    listLayout.minHeight = listOpen ? visibleCount * optionHeight + 4f : 0f;
                    listLayout.preferredHeight = listLayout.minHeight;
                }
            }

            if (selectorRootLayout != null)
            {
                float listHeight = listOpen ? Mathf.Min(optionViews.Count, Mathf.Max(1, maxVisibleOptions)) * optionHeight + 6f : 0f;
                selectorRootLayout.minHeight = selectorHeight + listHeight;
                selectorRootLayout.preferredHeight = selectorRootLayout.minHeight;
            }
        }

        private void SelectContext(string segmentId)
        {
            if (string.IsNullOrWhiteSpace(segmentId) || cardUi == null)
            {
                return;
            }

            selectedSegmentId = segmentId.Trim();
            cardUi.SelectSegmentWeaponStatDebugContext(selectedSegmentId);
            ApplyListOpen(false);
            RefreshLabelAndHighlights();
        }

        private void SyncSelectedFromCardUi()
        {
            if (cardUi == null)
            {
                return;
            }

            string currentId = cardUi.GetSelectedSegmentWeaponStatDebugContextId();
            if (!string.IsNullOrWhiteSpace(currentId))
            {
                selectedSegmentId = currentId.Trim();
            }
        }

        private void RefreshLabelAndHighlights()
        {
            if (selectorLabel != null)
            {
                selectorLabel.text = $"세그먼트: {ResolveDisplayName(selectedSegmentId)} ▼";
            }

            for (int i = 0; i < optionViews.Count; i++)
            {
                OptionButtonView view = optionViews[i];
                if (view.Background == null)
                {
                    continue;
                }

                bool selected = string.Equals(view.SegmentId, selectedSegmentId, System.StringComparison.OrdinalIgnoreCase);
                view.Background.color = selected
                    ? new Color(0.25f, 0.42f, 0.52f, 0.98f)
                    : new Color(0.07f, 0.09f, 0.10f, 0.96f);
            }
        }

        private string ResolveDisplayName(string segmentId)
        {
            if (string.IsNullOrWhiteSpace(segmentId))
            {
                return "-";
            }

            for (int i = 0; i < optionViews.Count; i++)
            {
                if (string.Equals(optionViews[i].SegmentId, segmentId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return optionViews[i].DisplayName;
                }
            }

            return segmentId.Trim();
        }

        private static RectTransform FindDebugPanelRoot()
        {
            RectTransform fallback = null;
            RectTransform[] rectTransforms = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform rectTransform = rectTransforms[i];
                if (rectTransform == null)
                {
                    continue;
                }

                string objectName = rectTransform.name;
                if (objectName.IndexOf("DebugSection_Upgred", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rectTransform;
                }

                if (fallback == null
                    && objectName.IndexOf("SegmentDebugPanel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fallback = rectTransform;
                }
            }

            return fallback;
        }
    }
}
