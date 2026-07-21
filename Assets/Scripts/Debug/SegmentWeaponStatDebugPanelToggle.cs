using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    //전찬우 수정-0622
    public sealed class SegmentWeaponStatDebugPanelToggle : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect; // 열기/닫기 대상 디버그 패널
        [SerializeField] private Button toggleButton; // '<'/'>' 디버그 버튼
        [SerializeField] private TextMeshProUGUI toggleText; // TMP 버튼 라벨
        [SerializeField] private Text legacyToggleText; // 기본 Text 버튼 라벨 fallback
        [SerializeField] private DebugPanelStage panelStage = DebugPanelStage.Open; // 시작 표시 상태
        [SerializeField] private string openedLabel = ">"; // 열려 있을 때 버튼 표시
        [SerializeField] private string closedLabel = "<"; // 닫혀 있을 때 버튼 표시
        [Min(0f)][SerializeField] private float closedMargin = 32f; // 닫힘 상태에서 화면 밖 여유
        [Min(0f)][SerializeField] private float slideSeconds = 0.18f; // 열기/닫기 이동 시간

        private readonly List<MoveTargetState> moveTargets = new List<MoveTargetState>(); // 함께 이동할 오른쪽 디버그 묶음
        private bool openPositionsCached; // 열린 위치 캐시 여부
        private bool wired; // 버튼 연결 여부
        private float nextResolveTime; // 참조 재검색 간격

        private enum DebugPanelStage
        {
            Open = 0,
            StatsClosed = 1,
            AllClosed = 2
        }

        private enum MoveTargetKind
        {
            StatsPanel = 0,
            ButtonPanel = 1,
            ToggleHandle = 2
        }

        private sealed class MoveTargetState
        {
            public RectTransform Rect;
            public Vector2 OpenAnchoredPosition;
            public float OpenLocalMinX;
            public float OpenLocalMaxX;
            public bool KeepVisibleWhenClosed;
            public MoveTargetKind Kind;
        }

        private void OnEnable()
        {
            TryWire();
        }

        private void Start()
        {
            TryWire();
            ApplyPanelState(false);
        }

        private void Update()
        {
            if (wired && panelRect != null && toggleButton != null && moveTargets.Count > 0)
            {
                return; // 연결 유지 중
            }

            if (Time.unscaledTime < nextResolveTime)
            {
                return; // 너무 잦은 검색 방지
            }

            nextResolveTime = Time.unscaledTime + 0.5f;
            TryWire();
        }

        private void OnDisable()
        {
            Unwire();
        }

        private void OnDestroy()
        {
            Unwire();
        }

        private void TryWire()
        {
            if (panelRect == null)
            {
                panelRect = FindDebugPanel();
            }

            if (toggleButton == null)
            {
                toggleButton = FindToggleButtonNearPanel(panelRect);
            }

            if (toggleButton == null)
            {
                return; // 버튼 없음
            }

            RefreshMoveTargets();
            ResolveToggleLabelReferences();
            string currentLabel = ResolveButtonLabel(toggleButton);
            if (currentLabel == "<")
            {
                panelStage = DebugPanelStage.AllClosed;
            }
            else if (currentLabel == ">")
            {
                panelStage = DebugPanelStage.Open;
            }

            toggleButton.onClick.RemoveListener(TogglePanel);
            toggleButton.onClick.AddListener(TogglePanel);
            wired = true;
            ApplyPanelState(false);
        }

        private void Unwire()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(TogglePanel);
            }

            wired = false;
        }

        private void TogglePanel()
        {
            panelStage = panelStage == DebugPanelStage.Open
                ? DebugPanelStage.StatsClosed
                : panelStage == DebugPanelStage.StatsClosed
                    ? DebugPanelStage.AllClosed
                    : DebugPanelStage.Open;
            ApplyPanelState(true);
        }

        private void ApplyPanelState(bool animated)
        {
            if (panelRect == null)
            {
                return; // 패널 없음
            }

            RefreshMoveTargets();
            if (moveTargets.Count == 0)
            {
                return; // 이동 대상 없음
            }

            CacheOpenPositions();
            float closedOffsetX = panelStage == DebugPanelStage.Open ? 0f : GetClosedOffsetX();
            for (int i = 0; i < moveTargets.Count; i++)
            {
                MoveTargetState target = moveTargets[i];
                if (target == null || target.Rect == null)
                {
                    continue;
                }

                Vector2 targetPosition = GetTargetPosition(target, closedOffsetX);

                target.Rect.DOKill();
                if (Application.isPlaying && animated && slideSeconds > 0f)
                {
                    target.Rect.DOAnchorPos(targetPosition, slideSeconds)
                        .SetEase(Ease.OutCubic)
                        .SetUpdate(true);
                }
                else
                {
                    target.Rect.anchoredPosition = targetPosition;
                }
            }

            SetToggleLabel(panelStage == DebugPanelStage.AllClosed ? closedLabel : openedLabel);
        }

        private Vector2 GetTargetPosition(MoveTargetState target, float closedOffsetX)
        {
            if (target == null)
            {
                return Vector2.zero;
            }

            if (panelStage == DebugPanelStage.Open)
            {
                return target.OpenAnchoredPosition;
            }

            if (target.Kind == MoveTargetKind.ToggleHandle)
            {
                return panelStage == DebugPanelStage.StatsClosed
                    ? GetHandleBesideButtonPanelsPosition(target)
                    : GetVisibleHandleClosedPosition(target, closedOffsetX);
            }

            if (target.Kind == MoveTargetKind.StatsPanel)
            {
                return target.OpenAnchoredPosition + Vector2.right * closedOffsetX;
            }

            return panelStage == DebugPanelStage.AllClosed
                ? target.OpenAnchoredPosition + Vector2.right * closedOffsetX
                : target.OpenAnchoredPosition;
        }

        private void CacheOpenPositions()
        {
            if (openPositionsCached)
            {
                return;
            }

            for (int i = 0; i < moveTargets.Count; i++)
            {
                MoveTargetState target = moveTargets[i];
                if (target != null && target.Rect != null)
                {
                    target.OpenAnchoredPosition = target.Rect.anchoredPosition;
                    if (TryGetLocalBounds(target.Rect, out float minX, out float maxX))
                    {
                        target.OpenLocalMinX = minX;
                        target.OpenLocalMaxX = maxX;
                    }
                }
            }

            openPositionsCached = true;
        }

        private static bool TryGetLocalBounds(RectTransform rectTransform, out float minX, out float maxX)
        {
            minX = 0f;
            maxX = 0f;
            if (rectTransform == null || rectTransform.parent == null)
            {
                return false;
            }

            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            minX = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float localX = parentRect.InverseTransformPoint(corners[i]).x;
                minX = Mathf.Min(minX, localX);
                maxX = Mathf.Max(maxX, localX);
            }

            return !float.IsInfinity(minX) && !float.IsInfinity(maxX);
        }

        private float GetClosedOffsetX()
        {
            float width = panelRect != null ? panelRect.rect.width : 0f;
            float panelOpenX = GetOpenPositionX(panelRect);
            float rightHiddenX = Mathf.Max(width * Mathf.Max(panelRect != null ? panelRect.pivot.x : 1f, 0f), 430f)
                + Mathf.Max(closedMargin, 0f);
            float closedOffsetX = rightHiddenX - panelOpenX;
            if (closedOffsetX <= 0f)
            {
                closedOffsetX = Mathf.Max(width, 430f) + Mathf.Max(closedMargin, 0f);
            }

            return closedOffsetX;
        }

        private Vector2 GetVisibleHandleClosedPosition(MoveTargetState target, float closedOffsetX)
        {
            Vector2 closedPosition = target.OpenAnchoredPosition + Vector2.right * closedOffsetX;
            if (target == null || target.Rect == null)
            {
                return closedPosition;
            }

            if (TryGetMaxVisibleAnchoredX(target.Rect, out float maxVisibleX) && maxVisibleX > target.OpenAnchoredPosition.x)
            {
                closedPosition.x = Mathf.Min(closedPosition.x, maxVisibleX);
                return closedPosition;
            }

            float panelWidth = panelRect != null ? Mathf.Max(panelRect.rect.width, 0f) : 0f;
            closedPosition.x = target.OpenAnchoredPosition.x + Mathf.Max(closedOffsetX - panelWidth, 0f);
            return closedPosition;
        }

        private Vector2 GetHandleBesideButtonPanelsPosition(MoveTargetState target)
        {
            Vector2 targetPosition = target.OpenAnchoredPosition;
            if (target == null || target.Rect == null)
            {
                return targetPosition;
            }

            if (!TryGetButtonPanelGroupMinX(out float groupMinX))
            {
                return targetPosition;
            }

            float desiredRightEdgeX = groupMinX - Mathf.Max(closedMargin * 0.25f, 6f);
            float pivotLocalX = desiredRightEdgeX - target.Rect.rect.width * (1f - target.Rect.pivot.x);
            if (TryConvertParentLocalXToAnchoredX(target.Rect, pivotLocalX, out float anchoredX))
            {
                targetPosition.x = Mathf.Max(target.OpenAnchoredPosition.x, anchoredX);
            }

            return targetPosition;
        }

        private bool TryGetButtonPanelGroupMinX(out float groupMinX)
        {
            groupMinX = float.PositiveInfinity;
            for (int i = 0; i < moveTargets.Count; i++)
            {
                MoveTargetState target = moveTargets[i];
                if (target == null || target.Kind != MoveTargetKind.ButtonPanel)
                {
                    continue;
                }

                groupMinX = Mathf.Min(groupMinX, target.OpenLocalMinX);
            }

            return !float.IsInfinity(groupMinX);
        }

        private static bool TryConvertParentLocalXToAnchoredX(RectTransform rectTransform, float parentLocalX, out float anchoredX)
        {
            anchoredX = 0f;
            RectTransform parentRect = rectTransform != null ? rectTransform.parent as RectTransform : null;
            if (parentRect == null)
            {
                return false;
            }

            float anchorX = Mathf.Lerp(rectTransform.anchorMin.x, rectTransform.anchorMax.x, rectTransform.pivot.x);
            float anchorReferenceX = parentRect.rect.xMin + parentRect.rect.width * anchorX;
            anchoredX = parentLocalX - anchorReferenceX;
            return true;
        }

        private bool TryGetMaxVisibleAnchoredX(RectTransform rectTransform, out float maxVisibleX)
        {
            maxVisibleX = 0f;
            RectTransform parentRect = rectTransform != null ? rectTransform.parent as RectTransform : null;
            if (parentRect == null || parentRect.rect.width <= 0f)
            {
                return false;
            }

            float anchorX = Mathf.Lerp(rectTransform.anchorMin.x, rectTransform.anchorMax.x, rectTransform.pivot.x);
            maxVisibleX = parentRect.rect.width * (1f - anchorX)
                - Mathf.Max(closedMargin, 0f)
                - rectTransform.rect.width * (1f - rectTransform.pivot.x);
            return true;
        }

        private float GetOpenPositionX(RectTransform rectTransform)
        {
            for (int i = 0; i < moveTargets.Count; i++)
            {
                MoveTargetState target = moveTargets[i];
                if (target != null && target.Rect == rectTransform)
                {
                    return target.OpenAnchoredPosition.x;
                }
            }

            return rectTransform != null ? rectTransform.anchoredPosition.x : 0f;
        }

        private void RefreshMoveTargets()
        {
            if (panelRect == null)
            {
                return;
            }

            if (moveTargets.Count > 0)
            {
                return; // 이미 구성됨
            }

            Transform parent = panelRect.parent;
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    RectTransform childRect = parent.GetChild(i) as RectTransform;
                    if (TryResolveMoveTargetKind(childRect, out MoveTargetKind kind))
                    {
                        AddMoveTarget(childRect, false, kind);
                    }
                }
            }

            RectTransform toggleRect = toggleButton != null ? toggleButton.transform as RectTransform : null;
            AddMoveTarget(toggleRect, true, MoveTargetKind.ToggleHandle);
            if (moveTargets.Count == 0)
            {
                AddMoveTarget(panelRect, false, MoveTargetKind.StatsPanel);
            }
        }

        private void AddMoveTarget(RectTransform rectTransform, bool keepVisibleWhenClosed = false, MoveTargetKind kind = MoveTargetKind.ButtonPanel)
        {
            if (rectTransform == null)
            {
                return;
            }

            for (int i = 0; i < moveTargets.Count; i++)
            {
                if (moveTargets[i].Rect == rectTransform)
                {
                    moveTargets[i].KeepVisibleWhenClosed |= keepVisibleWhenClosed;
                    moveTargets[i].Kind = kind;
                    return; // 중복 방지
                }
            }

            moveTargets.Add(new MoveTargetState { Rect = rectTransform, KeepVisibleWhenClosed = keepVisibleWhenClosed, Kind = kind });
        }

        private static bool TryResolveMoveTargetKind(RectTransform rectTransform, out MoveTargetKind kind)
        {
            kind = MoveTargetKind.ButtonPanel;
            if (rectTransform == null)
            {
                return false;
            }

            string objectName = rectTransform.name;
            if (objectName.IndexOf("ControlModeButtonPanel", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (objectName.IndexOf("DebugSection_Upgred", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("SegmentDebugPanel", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = MoveTargetKind.StatsPanel;
                return true;
            }

            if (objectName.IndexOf("DebugSection_", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = MoveTargetKind.ButtonPanel;
                return true;
            }

            return false;
        }

        private static RectTransform FindDebugPanel()
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

        private static Button FindToggleButtonNearPanel(RectTransform targetPanel)
        {
            Transform parent = targetPanel != null ? targetPanel.parent : null;
            if (parent == null)
            {
                return null; // 패널 부모 없음
            }

            Button[] siblingButtons = parent.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < siblingButtons.Length; i++)
            {
                Button button = siblingButtons[i];
                if (button != null
                    && button.transform.parent == parent
                    && IsToggleButtonCandidate(button))
                {
                    return button;
                }
            }

            return null;
        }

        private static bool IsToggleButtonCandidate(Button button)
        {
            if (button == null)
            {
                return false;
            }

            string label = ResolveButtonLabel(button);
            return label == "<" || label == ">";
        }

        private static string ResolveButtonLabel(Button button)
        {
            TextMeshProUGUI tmp = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (tmp != null)
            {
                return tmp.text.Trim();
            }

            Text legacy = button != null ? button.GetComponentInChildren<Text>(true) : null;
            return legacy != null ? legacy.text.Trim() : string.Empty;
        }

        private void ResolveToggleLabelReferences()
        {
            if (toggleButton == null)
            {
                return; // 버튼 없음
            }

            if (toggleText == null)
            {
                toggleText = toggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (toggleText == null && legacyToggleText == null)
            {
                legacyToggleText = toggleButton.GetComponentInChildren<Text>(true);
            }
        }

        private void SetToggleLabel(string label)
        {
            string resolvedLabel = string.IsNullOrWhiteSpace(label) ? (panelStage == DebugPanelStage.AllClosed ? "<" : ">") : label.Trim();
            if (toggleText != null)
            {
                toggleText.text = resolvedLabel;
            }
            else if (legacyToggleText != null)
            {
                legacyToggleText.text = resolvedLabel;
            }
        }
    }
}
