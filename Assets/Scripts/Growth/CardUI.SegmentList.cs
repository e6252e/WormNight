using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DG.Tweening;
using TeamProject01.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class CardUI
{
    private void SetupSegmentListHoverUi() // 호버 브릿지 부착 및 초기 비활성
    {
        HideSegmentListUi();

        if (segmentListPopup == null)
        {
            return;
        }

        EnsureUiRaycastTarget(segmentListPopup); // Image Raycast Target 보장

        SegmentListHoverBridge popupBridge = segmentListPopup.GetComponent<SegmentListHoverBridge>();
        if (popupBridge == null)
        {
            popupBridge = segmentListPopup.AddComponent<SegmentListHoverBridge>();
        }

        popupBridge.Initialize(this);

        // 안건준 수정 - 0622 — Segment List(+스크롤바 영역)에도 브릿지 추가: 리스트 위에서 스크롤 가능
        if (segmentList != null)
        {
            EnsureUiRaycastTarget(segmentList);
            SegmentListHoverBridge listBridge = segmentList.GetComponent<SegmentListHoverBridge>();
            if (listBridge == null)
            {
                listBridge = segmentList.AddComponent<SegmentListHoverBridge>();
            }

            listBridge.Initialize(this);
        }
    }

    private static void EnsureUiRaycastTarget(GameObject uiRoot) // 호버 감지용 Raycast
    {
        if (uiRoot == null || !uiRoot.TryGetComponent(out Graphic graphic))
        {
            return;
        }

        graphic.raycastTarget = true;
    }

    private void ShowSegmentListPopupOnPanelOpen() // 카드 패널 열릴 때 트리거 바 표시
    {
        if (segmentListPopup != null)
        {
            segmentListPopup.SetActive(true);
        }

        SetSegmentListVisible(false); // 리스트는 호버 전까지 숨김
    }

    private void HideSegmentListUi() // 패널 닫힐 때 전부 숨김
    {
        if (hideSegmentListCoroutine != null)
        {
            StopCoroutine(hideSegmentListCoroutine);
            hideSegmentListCoroutine = null;
        }

        SetSegmentListVisible(false);

        if (segmentListPopup != null)
        {
            segmentListPopup.SetActive(false);
        }
    }

    private void ShowSegmentListOnHover() // Popup/List 호버 시 목록 표시
    {
        if (hideSegmentListCoroutine != null)
        {
            StopCoroutine(hideSegmentListCoroutine);
            hideSegmentListCoroutine = null;
        }

        RefreshSegmentListText(); // 호버 시 최신 세그먼트 목록 갱신
        SetSegmentListVisible(true);
    }

    private void RequestHideSegmentListOnHoverExit() // Popup 또는 List에서 마우스가 나갔을 때
    {
        if (hideSegmentListCoroutine != null)
        {
            StopCoroutine(hideSegmentListCoroutine);
        }

        // 안건준 수정 - 0622 — 1프레임 대기 후 둘 다 벗어났는지 확인 (Popup→List 이동 시 깜빡임 방지)
        hideSegmentListCoroutine = StartCoroutine(HideSegmentListDelayed());
    }

    private IEnumerator HideSegmentListDelayed() // 1프레임 대기 후 호버 상태 재확인
    {
        yield return null; // 이동 중 false positive 방지

        // Popup 또는 List 위에 있으면 유지, 둘 다 아니면 숨김
        bool stillHovered = IsPointerOverSegmentUiArea();
        if (!stillHovered)
        {
            SetSegmentListVisible(false);
        }

        hideSegmentListCoroutine = null;
    }

    private bool IsPointerOverSegmentUiArea() // Popup 또는 Segment List 영역 위에 포인터 있는지
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        Vector2 screenPos;
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        else
        {
            screenPos = Input.mousePosition;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            Transform hit = results[i].gameObject.transform;

            if (segmentListPopup != null && segmentListPopup.activeInHierarchy
                && (hit == segmentListPopup.transform || hit.IsChildOf(segmentListPopup.transform)))
            {
                return true; // Popup 위
            }

            if (segmentList != null && segmentList.activeInHierarchy
                && (hit == segmentList.transform || hit.IsChildOf(segmentList.transform)))
            {
                return true; // List 또는 스크롤바 위
            }
        }

        return false;
    }

    private void SetSegmentListVisible(bool visible) // Segment List 활성/비활성
    {
        if (segmentList != null)
        {
            segmentList.SetActive(visible);
        }
    }

    private void RefreshSegmentListText() // 장착 세그먼트 이름:개수 텍스트 갱신
    {
        if (segmentListText == null)
        {
            return; // 텍스트 미연결
        }

        CoreStatProvider core = CoreStatProvider.Active;
        ConvoyController convoy = core != null ? core.Convoy : null;
        if (convoy == null)
        {
            segmentListText.text = string.Empty; // 컨보이 없으면 빈 텍스트
            return;
        }

        Dictionary<string, int> countsBySegmentId = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        convoy.CollectAttachedSegmentCounts(countsBySegmentId); // 현재 장착 세그먼트 ID별 개수

        Dictionary<string, string> idToDisplayName = BuildSegmentDisplayNameMap(); // ID → 표시명

        List<string> sortedIds = new List<string>(countsBySegmentId.Keys);
        sortedIds.Sort(System.StringComparer.OrdinalIgnoreCase); // 알파벳 순 정렬

        StringBuilder builder = new StringBuilder(256);
        for (int i = 0; i < sortedIds.Count; i++)
        {
            string segId = sortedIds[i];
            string displayName = idToDisplayName.TryGetValue(segId, out string found) ? found : segId; // 표시명 없으면 ID 그대로
            builder.Append(displayName);
            builder.Append(" : ");
            builder.Append(countsBySegmentId[segId]); // 개수
            if (i < sortedIds.Count - 1)
            {
                builder.AppendLine(); // 줄바꿈
            }
        }

        segmentListText.text = builder.ToString();
        ResizeSegmentListContent(); // 안건준 추가 - 0622 — 텍스트 높이에 맞게 Content 크기 조정
    }

    // 안건준 추가 - 0622
    private void ResizeSegmentListContent() // Content RT 높이를 TMP 필요 높이로 설정 (스크롤 활성화)
    {
        if (segmentListContent == null || segmentListText == null)
        {
            return; // 참조 미연결
        }

        segmentListText.ForceMeshUpdate(); // TMP 레이아웃 즉시 계산
        float needed = segmentListText.preferredHeight + 20f; // 텍스트 필요 높이 + 여유
        Vector2 sd = segmentListContent.sizeDelta;
        segmentListContent.sizeDelta = new Vector2(sd.x, Mathf.Max(needed, 50f)); // 최소 50px 보장
    }

    private Dictionary<string, string> BuildSegmentDisplayNameMap() // 카탈로그에서 ID→DisplayName 맵 빌드
    {
        Dictionary<string, string> map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        CoreStatProvider core = CoreStatProvider.Active;
        if (core == null)
        {
            return map;
        }

        List<SegmentCatalogEntry> entries = new List<SegmentCatalogEntry>();
        core.TryGetWeaponEnhanceChoiceCandidates(entries); // 카탈로그 전체 항목 (ID 있는 것)

        for (int i = 0; i < entries.Count; i++)
        {
            string id = entries[i].NormalizedId;
            if (string.IsNullOrWhiteSpace(id) || map.ContainsKey(id))
            {
                continue;
            }

            string name = !string.IsNullOrWhiteSpace(entries[i].DisplayName)
                ? entries[i].DisplayName
                : id; // DisplayName 없으면 ID 표시
            map[id] = name;
        }

        return map;
    }

    internal void NotifySegmentListHoverEnter() // 브릿지 → 호버 진입
    {
        if (!IsLevelUpPanelOpen())
        {
            return;
        }

        ShowSegmentListOnHover();
    }

    internal void NotifySegmentListHoverExit() // 브릿지 → 호버 이탈
    {
        if (!IsLevelUpPanelOpen())
        {
            return;
        }

        RequestHideSegmentListOnHoverExit();
    }
    // 안건준 추가 - 0622 ======
}
