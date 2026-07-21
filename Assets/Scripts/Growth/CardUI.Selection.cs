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
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public partial class CardUI
{
    private void HandleCardChoiceKeyboardShortcuts()
    {
        if (!spawnedForCurrentOpen || isProcessingSelection || !IsLevelUpPanelOpen() || IsCardChoiceTextInputFocused())
        {
            return;
        }

        int choiceIndex = ResolvePressedCardChoiceShortcutIndex();
        if (!TryResolveCardChoiceByVisualOrder(choiceIndex, out SpawnedCardEntry entry))
        {
            return;
        }

        if (entry == null || !entry.CanSelect || !entry.IsClickable)
        {
            return;
        }

        StopAutoSelect(); // 수동 선택 우선
        NotifySpawnedCardClicked(entry);
    }

    private static int ResolvePressedCardChoiceShortcutIndex()
    {
        if (WasCardChoiceShortcutPressed(1))
        {
            return 0;
        }

        if (WasCardChoiceShortcutPressed(2))
        {
            return 1;
        }

        if (WasCardChoiceShortcutPressed(3))
        {
            return 2;
        }

        return -1;
    }

    private bool TryResolveCardChoiceByVisualOrder(int choiceIndex, out SpawnedCardEntry entry)
    {
        entry = null;
        if (choiceIndex < 0 || spawnedCards == null || spawnedCards.Count == 0)
        {
            return false;
        }

        List<int> orderedIndices = new List<int>(spawnedCards.Count);
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i];
            if (card?.RootTransform == null)
            {
                continue;
            }

            orderedIndices.Add(i);
        }

        orderedIndices.Sort((left, right) =>
        {
            float leftX = GetSpawnedCardVisualCenterX(spawnedCards[left]);
            float rightX = GetSpawnedCardVisualCenterX(spawnedCards[right]);
            int compare = leftX.CompareTo(rightX);
            return compare != 0 ? compare : left.CompareTo(right);
        });

        if (choiceIndex >= orderedIndices.Count)
        {
            return false;
        }

        entry = spawnedCards[orderedIndices[choiceIndex]];
        return entry != null;
    }

    private static float GetSpawnedCardVisualCenterX(SpawnedCardEntry entry)
    {
        if (entry?.RootTransform != null)
        {
            return entry.RootTransform.position.x;
        }

        return entry?.Root != null ? entry.Root.transform.position.x : float.PositiveInfinity;
    }

    private static bool WasCardChoiceShortcutPressed(int number)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (number)
            {
                case 1:
                    return WasKeyboardKeyPressed(keyboard.digit1Key) || WasKeyboardKeyPressed(keyboard.numpad1Key);
                case 2:
                    return WasKeyboardKeyPressed(keyboard.digit2Key) || WasKeyboardKeyPressed(keyboard.numpad2Key);
                case 3:
                    return WasKeyboardKeyPressed(keyboard.digit3Key) || WasKeyboardKeyPressed(keyboard.numpad3Key);
            }
        }
#endif

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool WasKeyboardKeyPressed(KeyControl key)
    {
        return key != null && key.wasPressedThisFrame;
    }
#endif

    private static bool IsCardChoiceTextInputFocused()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
        {
            return false;
        }

        return selected.GetComponent<InputField>() != null || selected.GetComponent<TMP_InputField>() != null;
    }

    private void NotifySpawnedCardClicked(SpawnedCardEntry entry)
    {
        ForceHideAllCardTooltips(); // 클릭 후 화면 전환 중 툴팁 잔상 제거
        HandleCardClicked(entry); // 생성 카드 클릭
    }

    private void HandleCardClicked(SpawnedCardEntry selectedEntry)
    {
        if (isProcessingSelection || selectedEntry == null || !selectedEntry.CanSelect)
        {
            return;
        }

        // 카드 선택 사운드
        cardSound?.PlayCardSelect();

        if (selectedEntry.RewardChoice != RewardChoiceKind.None)
        {
            HandleRewardChoiceClicked(selectedEntry); // 보상 선택 카드
            return;
        }

        if (selectedEntry.SegmentRole == SegmentCardRole.Candidate)
        {
            HandleSegmentCandidateClicked(selectedEntry); // 세그먼트 후보 → 2단계 분기
            return;
        }

        if (selectedEntry.SegmentRole == SegmentCardRole.EnhanceChoice)
        {
            if (!TryApplySelectedCard(selectedEntry))
            {
                return;
            }

            isProcessingSelection = true; // 선택 처리 중
            PlaySelectionCloseSequence(selectedEntry); // 강화 적용 후 패널 닫기
            return;
        }

        if (!TryApplySelectedCard(selectedEntry))
        {
            return;
        }

        if (selectedEntry.StatUpgrade != null) // 스탯 카드 선택 성공 시
        {
            if (selectedEntry.StatUpgradeDefinition != null)
            {
                RememberSelectedStatCardDefinition(selectedEntry.StatUpgradeDefinition); // 데이터 에셋 기준 가중치 증가
            }
            else
            {
                RememberSelectedStatCardPrefab(selectedEntry.SourcePrefab); // 기존 프리팹 fallback 가중치 증가
            }
        }

        isProcessingSelection = true;
        PlaySelectionCloseSequence(selectedEntry);
    }

    // 세그먼트 후보 클릭 - 무기강화/추가·레벨업 흐름 분기
    private void HandleSegmentCandidateClicked(SpawnedCardEntry selectedEntry)
    {
        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        if (core == null || string.IsNullOrWhiteSpace(selectedEntry.SegmentId))
        {
            Debug.LogWarning("[CardUI] 세그먼트 후보 적용 실패: CoreStatProvider 또는 SegmentId 없음", selectedEntry.Root);
            return;
        }

        SetSegmentWeaponStatDebugTarget(selectedEntry.SegmentId); // 후보 선택 즉시 해당 세그먼트 스탯 표시

        if (currentSpawnPhase == LevelUpCardPhase.WeaponEnhance && useSegmentSelectWeaponEnhanceFlow)
        {
            isProcessingSelection = true; // 2단계 전환 중
            PlaySegmentEnhancementChoiceSequence(selectedEntry); // A: 선택 세그먼트 강화 카드
            return;
        }

        bool canAdd = core.CanAddSegment(selectedEntry.SegmentId); // 추가 가능 여부
        bool canLevelUp = core.CanLevelUpSegmentModel(selectedEntry.SegmentId); // 레벨업 가능 여부
        isProcessingSelection = true; // 2단계 전환 중

        if (TryResolveSingleSegmentAction(canAdd, canLevelUp, out SegmentCardRole singleActionRole))
        {
            PlaySingleSegmentActionAutoApplySequence(selectedEntry, singleActionRole, canAdd, canLevelUp); // 선택지 1개면 2차 창 스킵
            return;
        }

        PlaySegmentActionChoiceSequence(selectedEntry, canAdd, canLevelUp); // 추가/레벨업 2장
    }

    // 무기 강화 1단계 → 2단계: 세그먼트 선택 후 강화 카드 3장 표시
    private void PlaySegmentEnhancementChoiceSequence(SpawnedCardEntry selectedEntry)
    {
        ForceHideAllCardTooltips(); // 다음 카드 묶음으로 넘어가기 전 툴팁 즉시 제거
        Sequence sequence = DOTween.Sequence().SetUpdate(true); // 선택 연출

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i]; // 현재 카드
            if (card == null)
            {
                continue; // null 방지
            }

            if (card == selectedEntry)
            {
                sequence.Join(PlaySelectTween(card)); // 선택 카드 강조
            }
            else
            {
                sequence.Join(PlayHideTween(card)); // 나머지 카드 숨김
            }
        }

        sequence.AppendInterval(0.25f); // 전환 여유
        sequence.OnComplete(() =>
        {
            SpawnSegmentEnhancementCards(selectedEntry.SegmentId, selectedEntry.LevelDelta); // 2단계: 무기 강화 카드
            isProcessingSelection = false; // 다시 클릭 허용
        });
    }

    // 후보 카드가 사라진 뒤 추가/레벨업 2차 카드 표시
    private void PlaySegmentActionChoiceSequence(SpawnedCardEntry selectedEntry, bool canAdd, bool canLevelUp)
    {
        ForceHideAllCardTooltips(); // 추가/레벨업 2차 분기 전 툴팁 즉시 제거
        SegmentCatalogEntry catalogEntry = selectedEntry.SegmentCatalogEntry; // 후보 데이터 보관
        int levelDelta = Mathf.Max(0, selectedEntry.LevelDelta); // 선택권 모드는 0 소비
        Sequence sequence = DOTween.Sequence().SetUpdate(true); // 기존 선택 연출 재사용

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i]; // 현재 카드
            if (card == null)
            {
                continue; // null 방지
            }

            if (card == selectedEntry)
            {
                sequence.Join(PlaySelectTween(card)); // 선택 카드 강조
            }
            else
            {
                sequence.Join(PlayHideTween(card)); // 나머지 카드 숨김
            }
        }

        sequence.AppendInterval(0.25f); // 전환 여유
        sequence.OnComplete(() =>
        {
            ScheduleAutoSelectSegmentAction(canAdd, canLevelUp);
            SpawnSegmentActionCards(catalogEntry, levelDelta, canAdd, canLevelUp);
            isProcessingSelection = false;
        });
    }

    private static bool TryResolveSingleSegmentAction(bool canAdd, bool canLevelUp, out SegmentCardRole role) // 2차 선택지가 1개인지 판정
    {
        role = SegmentCardRole.None; // 기본값
        if (canAdd == canLevelUp)
        {
            return false; // 둘 다 가능하거나 둘 다 불가면 기존 2차 UI 유지
        }

        role = canAdd ? SegmentCardRole.AddAction : SegmentCardRole.LevelUpAction; // 유일한 액션
        return true;
    }

    private void PlaySingleSegmentActionAutoApplySequence(SpawnedCardEntry selectedEntry, SegmentCardRole actionRole, bool canAdd, bool canLevelUp)
    {
        ForceHideAllCardTooltips(); // 단일 자동 적용 전 툴팁 즉시 제거
        SegmentCatalogEntry catalogEntry = selectedEntry.SegmentCatalogEntry; // 실패 fallback용 후보 데이터
        int levelDelta = Mathf.Max(0, selectedEntry.LevelDelta); // 선택권 모드는 0 소비
        Sequence sequence = DOTween.Sequence().SetUpdate(true); // 후보 선택 연출 재사용

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i]; // 현재 후보 카드
            if (card == null)
            {
                continue; // null 방지
            }

            if (card == selectedEntry)
            {
                sequence.Join(PlaySelectTween(card)); // 선택 카드 강조
            }
            else
            {
                sequence.Join(PlayHideTween(card)); // 나머지 카드 숨김
            }
        }

        sequence.AppendInterval(0.25f); // 기존 2차 전환과 동일한 여유
        sequence.OnComplete(() =>
        {
            if (TryApplySingleSegmentAction(selectedEntry, actionRole))
            {
                cardEffect?.FadeAllEffects(0.2f); // 기존 닫기 경로와 동일하게 이펙트 정리
                CloseLevelUpPanelAfterSuccessfulSelection(); // 성공 시 바로 선택 완료
                return;
            }

            Debug.LogWarning("[CardUI] 단일 세그먼트 액션 자동 적용 실패: 2차 선택 UI로 fallback합니다.", selectedEntry.Root);
            SpawnSegmentActionCards(catalogEntry, levelDelta, canAdd, canLevelUp); // 실패 시 조작 가능한 화면 복구
            isProcessingSelection = false; // fallback 카드 클릭 허용
        });
    }

    private bool TryApplySingleSegmentAction(SpawnedCardEntry selectedEntry, SegmentCardRole actionRole) // 2차 카드 없이 코어에 직접 적용
    {
        CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
        if (core == null || selectedEntry == null || string.IsNullOrWhiteSpace(selectedEntry.SegmentId))
        {
            Debug.LogWarning("[CardUI] 단일 세그먼트 액션 적용 실패: CoreStatProvider 또는 SegmentId 없음", selectedEntry?.Root);
            return false;
        }

        int levelDelta = Mathf.Max(0, selectedEntry.LevelDelta); // 선택권 모드는 0 소비
        if (actionRole == SegmentCardRole.AddAction)
        {
            int addCount = selectedEntry.SegmentAddCard != null ? selectedEntry.SegmentAddCard.SegmentAddCount : 1; // 카드 설정 우선
            bool applied = core.TryApplySegmentAddChoice(selectedEntry.SegmentId, levelDelta, addCount); // 추가 적용
            if (applied)
            {
                SetSegmentWeaponStatDebugTarget(selectedEntry.SegmentId); // 추가 대상 표시
            }
            else
            {
                Debug.LogWarning("[CardUI] 세그먼트 추가 자동 적용 실패: 경험치/카탈로그/컨보이 조건 확인 필요", selectedEntry.Root);
            }

            return applied;
        }

        if (actionRole == SegmentCardRole.LevelUpAction)
        {
            bool applied = core.TryApplySegmentLevelUpChoice(selectedEntry.SegmentId, levelDelta); // 레벨업 적용
            if (applied)
            {
                SetSegmentWeaponStatDebugTarget(selectedEntry.SegmentId); // 레벨업 대상 표시
            }
            else
            {
                Debug.LogWarning("[CardUI] 세그먼트 레벨업 자동 적용 실패: 만렙/경험치/장착 상태 확인 필요", selectedEntry.Root);
            }

            return applied;
        }

        return false; // 지원하지 않는 역할
    }

    private bool TryApplySelectedCard(SpawnedCardEntry selectedEntry)
    {
        if (selectedEntry.SegmentRole == SegmentCardRole.EnhanceChoice)
        {
            CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
            WeaponDefinition definition = selectedEntry.WeaponDefinition; // 선택 강화
            bool applied = core != null
                && definition != null
                && core.TryApplyWeaponEnhancementChoice(
                    selectedEntry.SegmentId,
                    selectedEntry.LevelDelta,
                    definition,
                    selectedEntry.WeaponEnhancementTier); // 강화 적용 (등급별 수치)
            if (applied)
            {
                SetSegmentWeaponStatDebugTarget(selectedEntry.SegmentId); // 적용 대상 표시 유지
                LogWeaponEnhancementIncrease(selectedEntry.SegmentId, definition, core); // 누적 보너스 출력
            }
            else if (core == null)
            {
                Debug.LogWarning("[CardUI] 무기 강화 적용 실패: CoreStatProvider.Active 가 없습니다.", selectedEntry.Root);
            }

            return applied; // 적용 결과
        }

        if (selectedEntry.SegmentAddCard != null)
        {
            // 2차 선택 카드: 세그먼트 추가
            if (selectedEntry.SegmentRole == SegmentCardRole.AddAction)
            {
                CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
                bool applied = core != null && core.TryApplySegmentAddChoice(selectedEntry.SegmentId, selectedEntry.LevelDelta, selectedEntry.SegmentAddCard.SegmentAddCount); // 추가 적용
                if (!applied)
                {
                    Debug.LogWarning("[CardUI] 세그먼트 추가 적용 실패: 경험치/카탈로그/컨보이 조건 확인 필요", selectedEntry.Root); // 실패 로그
                }
                else
                {
                    SetSegmentWeaponStatDebugTarget(selectedEntry.SegmentId); // 추가된 세그먼트 표시
                }

                return applied; // 적용 결과 — 성공 시 SegmentCountChanged 구독에서 디버그 1회 출력
            }

            // 2차 선택 카드: 해당 세그먼트 전체 모델 레벨업
            if (selectedEntry.SegmentRole == SegmentCardRole.LevelUpAction)
            {
                CoreStatProvider core = CoreStatProvider.Active; // 현재 코어
                bool applied = core != null && core.TryApplySegmentLevelUpChoice(selectedEntry.SegmentId, selectedEntry.LevelDelta); // 레벨업 적용
                if (!applied)
                {
                    Debug.LogWarning("[CardUI] 세그먼트 레벨업 적용 실패: 만렙/경험치/장착 상태 확인 필요", selectedEntry.Root); // 실패 로그
                }
                else
                {
                    SetSegmentWeaponStatDebugTarget(selectedEntry.SegmentId); // 레벨업 대상 표시
                }

                return applied; // 적용 결과
            }

            return false; // 후보/없음 카드는 여기서 적용하지 않음
        }

        if (selectedEntry.StatUpgrade != null)
        {
            if (selectedEntry.StatUpgrade.TryApplyToCore())
            {
                return true;
            }

            Debug.LogWarning("[CardUI] 스탯 강화 적용 실패: CanLevelUp 미충족 또는 CoreStatProvider 없음", selectedEntry.Root);
            return false;
        }

        Debug.LogWarning("[CardUI] StatUpgrade/SegmentAddCard가 없어 코어에 반영하지 않습니다.", selectedEntry.Root);
        return false;
    }

    private void PlaySelectionCloseSequence(SpawnedCardEntry selectedEntry)
    {
        ForceHideAllCardTooltips(); // 선택 완료 닫힘 중 툴팁 잔상 제거
        // 카드 페이드(0.2s)와 동시에 이팩트도 축소 후 제거
        cardEffect?.FadeAllEffects(0.2f);

        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            SpawnedCardEntry card = spawnedCards[i];
            if (card == null)
            {
                continue;
            }

            if (card == selectedEntry)
            {
                sequence.Join(PlaySelectTween(card));
            }
            else
            {
                sequence.Join(PlayHideTween(card));
            }
        }

        sequence.AppendInterval(Mathf.Max(0f, selectionCloseHoldSeconds));
        sequence.OnComplete(CloseLevelUpPanelAfterSuccessfulSelection);
    }

    private void CloseLevelUpPanelAfterSuccessfulSelection()
    {
        if (activePanelMode == CardPanelMode.RewardChoice)
        {
            bool keepBackgroundBlur = pendingRewardSegmentTicketCount > 0; // 선택권 화면으로 이어질 때 블러 유지
            CloseLevelUpPanelAfterSelection(() =>
            {
                StopAutoSelect();
                ClearSpawnedCards();
                spawnedForCurrentOpen = false;
                isProcessingSelection = false;
                ApplyPendingRewardChoiceAfterClosed(); // 지급 또는 선택권 화면 연속 오픈
            }, keepBackgroundBlur);
            return;
        }

        if (activePanelMode == CardPanelMode.SegmentTicketChoice)
        {
            bool keepBackgroundBlur = segmentTicketChoicesRemaining > 1; // x2/x3 선택권 다음 화면 유지
            CloseLevelUpPanelAfterSelection(() =>
            {
                StopAutoSelect();
                ClearSpawnedCards();
                spawnedForCurrentOpen = false;
                isProcessingSelection = false;
                HandleSegmentTicketChoiceCompletedAfterClose(); // x2/x3 선택권 연속 처리
            }, keepBackgroundBlur);
            return;
        }

        // 안건준 수정 - 0622 : 패널이 완전히 닫힌 후 CompleteLevelUpChoice 호출 — 연속 레벨업 대응
        CloseLevelUpPanelAfterSelection(() =>
        {
            // 패널이 같은 프레임에 재오픈될 경우 Update()가 닫힘을 감지 못하므로 여기서 직접 정리
            StopAutoSelect();           // 이전 자동선택 코루틴 정리
            ClearSpawnedCards();        // 이전 카드 오브젝트 파괴
            spawnedForCurrentOpen = false; // 다음 오픈 시 새 카드 생성 허용
            isProcessingSelection = false; // 입력 잠금 해제

            CoreStatProvider.Active?.CompleteLevelUpChoice(); // 선택 완료 + StatsChanged → 다음 레벨업 트리거
        });
    }

    private void CloseLevelUpPanelAfterSelection(System.Action onClosed = null, bool keepBackgroundBlur = false) // 선택 완료 후 오버레이·일시정지 해제
    {
        LevelUpUi ui = ResolveLevelUpUi();
        if (ui != null)
        {
            ui.Close(selectionPanelCloseFadeSeconds, onClosed, keepBackgroundBlur); // 페이드 완료 후 onClosed 실행
            return;
        }

        if (levelUpPanelCanvasGroup != null)
        {
            levelUpPanelCanvasGroup.DOKill();
            levelUpPanelCanvasGroup
                .DOFade(0f, Mathf.Max(0.01f, selectionPanelCloseFadeSeconds))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    levelUpPanelCanvasGroup.blocksRaycasts = false;
                    levelUpPanelCanvasGroup.interactable = false;
                    if (Time.timeScale <= 0f)
                    {
                        Time.timeScale = TeamProject01.Gameplay.GameSpeedController.GetDesiredTimeScale();
                    }

                    onClosed?.Invoke(); // 닫힌 후 콜백
                });
            return;
        }

        if (Time.timeScale <= 0f)
        {
            Time.timeScale = TeamProject01.Gameplay.GameSpeedController.GetDesiredTimeScale();
        }

        onClosed?.Invoke(); // 즉시 호출
    }

    private void ClearSpawnedCards()
    {
        ForceHideAllCardTooltips(); // 카드 파괴 전 모든 툴팁 즉시 숨김
        // 안건준 추가 - 0623 : 카드 제거 전 이팩트 먼저 정리
        cardEffect?.ClearAll();

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i]?.Root != null)
            {
                Destroy(spawnedCards[i].Root);
            }
        }

        spawnedCards.Clear();
    }

    private LevelUpUi ResolveLevelUpUi()
    {
        if (levelUpUi != null)
        {
            return levelUpUi;
        }

        return FindFirstObjectByType<LevelUpUi>();
    }

    // 안건준 추가 - 0622 ======
}
