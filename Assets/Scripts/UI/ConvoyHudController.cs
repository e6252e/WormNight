using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class ConvoyHudController : MonoBehaviour // 컨보이 HUD
    {
        public ConvoyController Controller; // 표시 대상
        public Text SpeedText; // 속도 텍스트
        public Text TurnText; // 회전 텍스트
        public Text SegmentText; // 길이 텍스트
        public Text LevelText; // 레벨 텍스트
        public Text ExperienceText; // 경험치 텍스트
        public Text GoldText; // 골드 텍스트
        public Text ModeText; // 모드 텍스트
        public Text HelpText; // 도움말 텍스트
        public Button RelativeTurnButton; // 1번 버튼
        public Button WasdDirectionButton; // 2번 버튼
        public Button MousePointerButton; // 3번 버튼
        public Button WasdManualForwardButton; // 4번 버튼
        public Button AutoOrbitButton; // 자동궤도 버튼
        public Button AutoCardSelectButton; // 자동궤도 중 카드자동 버튼
        public CardUI CardUi; // 카드 자동선택 제어 대상
        public Color ButtonNormalColor = new Color(0.1f, 0.12f, 0.13f, 0.88f); // 기본 배경
        public Color ButtonSelectedColor = new Color(0.72f, 0.94f, 1f, 0.95f); // 선택 배경
        public Color ButtonNormalTextColor = new Color(0.93f, 0.96f, 0.96f, 1f); // 기본 글자
        public Color ButtonSelectedTextColor = new Color(0.08f, 0.1f, 0.11f, 1f); // 선택 글자

        private bool buttonsWired; // 이벤트 연결 여부
        private bool hasAutoOrbitSnapshot; // 자동궤도 상태 변경 감지 시작 여부
        private bool lastAutoOrbitActive; // 직전 자동궤도 상태

        private void Awake() // 초기 연결
        {
            WireButtons(); // 버튼 연결
        }

        private void OnEnable() // 활성화 갱신
        {
            WireButtons(); // 버튼 보장
            RefreshAll(); // 즉시 표시
        }

        private void Update() // 표시 갱신
        {
            RefreshAll(); // HUD 값 갱신
        }

        private void WireButtons() // 버튼 이벤트
        {
            if (buttonsWired)
            {
                return; // 이미 연결
            }

            SetButtonLabel(RelativeTurnButton, "좌우턴"); // 숫자키는 액션 HUD 전용
            SetButtonLabel(WasdDirectionButton, "WASD"); // 숫자 제거
            SetButtonLabel(MousePointerButton, "마우스"); // 숫자 제거
            SetButtonLabel(WasdManualForwardButton, "수동WASD"); // 숫자 제거
            BindModeButton(RelativeTurnButton, ConvoyControlMode.RelativeTurn); // 버튼 전환
            BindModeButton(WasdDirectionButton, ConvoyControlMode.WasdDirection); // 버튼 전환
            BindModeButton(MousePointerButton, ConvoyControlMode.MousePointer); // 버튼 전환
            BindModeButton(WasdManualForwardButton, ConvoyControlMode.WasdManualForward); // 버튼 전환
            ResolveAutoOrbitButton(); // 씬 배치 버튼 연결
            BindAutoOrbitButton(AutoOrbitButton); // 자동궤도
            ResolveAutoCardSelectButton(); // 자동궤도 카드자동 버튼 연결
            BindAutoCardSelectButton(AutoCardSelectButton); // 카드자동
            buttonsWired = true; // 연결 완료
        }

        private void BindModeButton(Button button, ConvoyControlMode mode) // 버튼 바인딩
        {
            if (button == null)
            {
                return; // 버튼 없음
            }

            button.onClick.RemoveAllListeners(); // 중복 제거
            button.onClick.AddListener(() =>
            {
                if (Controller != null)
                {
                    Controller.SetControlMode(mode); // 모드 변경
                    RefreshAll(); // 선택 표시
                }
            });
        }

        private void BindAutoOrbitButton(Button button) // 자동궤도 바인딩
        {
            if (button == null)
            {
                return; // 버튼 없음
            }

            button.onClick.RemoveAllListeners(); // 중복 제거
            button.onClick.AddListener(() =>
            {
                if (Controller != null)
                {
                    Controller.ToggleAutoOrbit(); // 자동궤도 토글
                    RefreshAll(); // 선택 표시
                }
            });
        }

        private void BindAutoCardSelectButton(Button button) // 카드자동 바인딩
        {
            if (button == null)
            {
                return; // 버튼 없음
            }

            button.onClick.RemoveAllListeners(); // 중복 제거
            button.onClick.AddListener(() =>
            {
                CardUI cardUi = ResolveCardUi(); // 카드 UI 찾기
                if (cardUi != null)
                {
                    cardUi.ToggleAutoSelectInAutoOrbit(); // 자동선택 토글
                    RefreshAll(); // 라벨/색 갱신
                }
            });
        }

        private void RefreshAll() // 전체 갱신
        {
            if (Controller == null)
            {
                SetAutoCardSelectButtonVisible(false); // 대상 없으면 숨김
                return; // 대상 없음
            }

            SetText(SpeedText, $"속도 {Controller.CurrentSpeed:0.00}"); // 속도
            SetText(TurnText, $"회전 {Controller.CurrentTurnVelocity:0} 도/초"); // 회전
            SetText(SegmentText, $"세그먼트 {Controller.SegmentCount}"); // 길이
            CoreStatData stats = CoreStatProvider.GetCurrentOrDefault(); // 코어 표시값
            SetText(LevelText, $"레벨 {stats.Level}"); // 레벨
            SetText(ExperienceText, $"경험치 {stats.CurrentExperience}/{stats.ExperienceToNextLevel}"); // 경험치
            SetText(GoldText, $"골드 {stats.Gold}"); // 골드
            SetText(ModeText, Controller.CurrentControlModeLabel); // 모드명
            SetText(HelpText, "하단 버튼 조작모드 전환\n1~5 액션 HUD\nSpace 세그먼트 추가\nBackspace 세그먼트 제거\nR 리셋\nQ/E 카메라각도조절"); // 도움말

            bool autoOrbit = Controller.IsAutoOrbitActive; // 자동궤도 상태
            NotifyCardUiAutoOrbitStateIfChanged(autoOrbit); // 자동궤도 종료 시 예약 자동선택 취소
            RefreshButton(RelativeTurnButton, !autoOrbit && Controller.CurrentControlMode == ConvoyControlMode.RelativeTurn); // 1번 상태
            RefreshButton(WasdDirectionButton, !autoOrbit && Controller.CurrentControlMode == ConvoyControlMode.WasdDirection); // 2번 상태
            RefreshButton(MousePointerButton, !autoOrbit && Controller.CurrentControlMode == ConvoyControlMode.MousePointer); // 3번 상태
            RefreshButton(WasdManualForwardButton, !autoOrbit && Controller.CurrentControlMode == ConvoyControlMode.WasdManualForward); // 4번 상태
            RefreshButton(AutoOrbitButton, autoOrbit); // 자동궤도 상태
            RefreshAutoCardSelectButton(autoOrbit); // 자동궤도 중에만 표시
        }

        private void ResolveAutoOrbitButton() // 자동궤도 버튼 연결
        {
            if (AutoOrbitButton != null)
            {
                SetButtonLabel(AutoOrbitButton, "자동궤도"); // 라벨 보장
                return; // 이미 있음
            }

            Transform searchRoot = GetButtonSearchRoot(); // 버튼 검색 루트
            AutoOrbitButton = FindButtonByName(searchRoot, "AutoOrbitButton"); // 씬 버튼 찾기

            if (AutoOrbitButton != null)
            {
                SetButtonLabel(AutoOrbitButton, "자동궤도"); // 라벨 보장
            }
        }

        private void ResolveAutoCardSelectButton() // 카드자동 버튼 연결
        {
            if (AutoCardSelectButton != null)
            {
                SetButtonLabel(AutoCardSelectButton, "카드자동 ON"); // 라벨 보장
                AutoCardSelectButton.gameObject.SetActive(false); // 자동궤도 전까지 숨김
                return; // 이미 있음
            }

            Transform searchRoot = GetButtonSearchRoot(); // 버튼 검색 루트
            AutoCardSelectButton = FindButtonByName(searchRoot, "AutoCardSelectButton"); // 씬 버튼 찾기

            if (AutoCardSelectButton == null)
            {
                AutoCardSelectButton = CreateRuntimeAutoCardSelectButton(searchRoot); // 다른 테스트 씬 fallback
            }

            if (AutoCardSelectButton != null)
            {
                SetButtonLabel(AutoCardSelectButton, "카드자동 ON"); // 라벨 보장
                AutoCardSelectButton.gameObject.SetActive(false); // 자동궤도 전까지 숨김
            }
        }

        private Transform GetButtonSearchRoot() // 버튼 검색 루트 반환
        {
            if (WasdManualForwardButton != null && WasdManualForwardButton.transform.parent != null)
            {
                return WasdManualForwardButton.transform.parent; // 버튼 패널
            }

            if (MousePointerButton != null && MousePointerButton.transform.parent != null)
            {
                return MousePointerButton.transform.parent; // 대체 패널
            }

            if (RelativeTurnButton != null && RelativeTurnButton.transform.parent != null)
            {
                return RelativeTurnButton.transform.parent; // 대체 패널
            }

            return transform; // HUD 루트 fallback
        }

        private static Button FindButtonByName(Transform root, string objectName) // 이름으로 버튼 찾기
        {
            if (root == null)
            {
                return null; // 검색 불가
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true); // 하위 버튼
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == objectName)
                {
                    return buttons[i]; // 대상 버튼
                }
            }

            return null; // 없음
        }

        private Button CreateRuntimeAutoCardSelectButton(Transform searchRoot) // 테스트 씬 fallback 버튼
        {
            if (AutoOrbitButton == null || searchRoot == null)
            {
                return null; // 기준 버튼 없음
            }

            Button button = Instantiate(AutoOrbitButton, searchRoot); // 기존 버튼 양식 복제
            button.name = "AutoCardSelectButton"; // 이름 기반 자동 연결
            PositionAutoCardSelectButton(AutoOrbitButton, button); // 자동궤도 버튼 위쪽
            return button;
        }

        private static void PositionAutoCardSelectButton(Button template, Button button) // 버튼 위치 보정
        {
            if (template == null || button == null)
            {
                return; // 대상 없음
            }

            RectTransform templateRect = template.GetComponent<RectTransform>(); // 기준
            RectTransform rect = button.GetComponent<RectTransform>(); // 대상
            if (templateRect == null || rect == null)
            {
                return; // Rect 없음
            }

            float height = templateRect.rect.height > 1f ? templateRect.rect.height : 36f; // 높이
            rect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, height + 8f); // 위쪽 배치
        }

        private static void SetButtonLabel(Button button, string label) // 버튼 글자
        {
            if (button == null)
            {
                return; // 버튼 없음
            }

            Text text = button.GetComponentInChildren<Text>(true); // 라벨
            if (text != null)
            {
                text.text = label; // 표시
            }
        }

        private CardUI ResolveCardUi() // 카드 UI 찾기
        {
            if (CardUi != null)
            {
                return CardUi; // Inspector 연결 우선
            }

            CardUi = FindFirstObjectByType<CardUI>(); // CoreTest 단일 CardUI fallback
            return CardUi;
        }

        private void NotifyCardUiAutoOrbitStateIfChanged(bool autoOrbit)
        {
            if (hasAutoOrbitSnapshot && lastAutoOrbitActive == autoOrbit)
            {
                return; // 변경 없음
            }

            hasAutoOrbitSnapshot = true;
            lastAutoOrbitActive = autoOrbit;
            ResolveCardUi()?.NotifyAutoOrbitActiveChanged(autoOrbit); // 예약 자동선택 정리/재시작
        }

        private void RefreshAutoCardSelectButton(bool autoOrbit)
        {
            if (AutoCardSelectButton == null)
            {
                return; // 버튼 없음
            }

            SetAutoCardSelectButtonVisible(autoOrbit); // 자동궤도 상태에서만 표시
            if (!autoOrbit)
            {
                return; // 숨김 상태
            }

            CardUI cardUi = ResolveCardUi(); // 현재 설정 확인
            bool autoSelect = cardUi != null && cardUi.AutoSelectInAutoOrbit;
            AutoCardSelectButton.interactable = cardUi != null; // 대상 없으면 클릭 비활성
            SetButtonLabel(AutoCardSelectButton, autoSelect ? "카드자동 ON" : "카드자동 OFF");
            RefreshButton(AutoCardSelectButton, autoSelect); // ON이면 선택색
        }

        private void SetAutoCardSelectButtonVisible(bool visible)
        {
            if (AutoCardSelectButton != null && AutoCardSelectButton.gameObject.activeSelf != visible)
            {
                AutoCardSelectButton.gameObject.SetActive(visible); // 자동궤도 외 숨김
            }
        }

        private void RefreshButton(Button button, bool selected) // 버튼 표시
        {
            if (button == null)
            {
                return; // 버튼 없음
            }

            Image image = button.targetGraphic as Image; // 배경 이미지
            if (image != null)
            {
                image.color = selected ? ButtonSelectedColor : ButtonNormalColor; // 배경색
            }

            Text text = button.GetComponentInChildren<Text>(true); // 라벨
            if (text != null)
            {
                text.color = selected ? ButtonSelectedTextColor : ButtonNormalTextColor; // 글자색
            }
        }

        private static void SetText(Text target, string value) // 텍스트 설정
        {
            if (target != null)
            {
                target.text = value; // 값 반영
            }
        }
    }
}

