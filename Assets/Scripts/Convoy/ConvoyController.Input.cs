using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private WormInput ReadInput() // 입력 수집
        {
            WormInput input = default; // 입력 묶음

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current; // 키보드
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    input.Turn -= 1f; // 좌회전
                    input.Move.x -= 1f; // 좌 입력
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    input.Turn += 1f; // 우회전
                    input.Move.x += 1f; // 우 입력
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    input.Move.y += 1f; // 전방 입력
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    input.Move.y -= 1f; // 후방 입력
                }

                input.AddSegment = keyboard.spaceKey.wasPressedThisFrame; // 테스트 추가
                input.RemoveSegment = keyboard.backspaceKey.wasPressedThisFrame; // 테스트 제거
                input.Reset = keyboard.rKey.wasPressedThisFrame; // 리셋

                // 숫자키 1~5는 골드 액션 HUD 스킬 사용에 배정한다.
            }

            Gamepad gamepad = Gamepad.current; // 게임패드
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue(); // 왼쪽 스틱
                input.Turn += stick.x; // 턴 입력
                input.Move += stick; // 방향 입력
                input.AddSegment |= gamepad.buttonSouth.wasPressedThisFrame; // 추가
                input.RemoveSegment |= gamepad.buttonWest.wasPressedThisFrame; // 제거
                input.Reset |= gamepad.startButton.wasPressedThisFrame; // 리셋
            }

            Mouse mouse = Mouse.current; // 마우스
            Camera camera = Camera.main; // 기준 카메라
            if (mouse != null && camera != null)
            {
                Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue()); // 화면 ray
                if (GroundService.RaycastGround(ray, out Vector3 mouseWorld))
                {
                    input.MouseWorld = mouseWorld; // 마우스 월드
                    input.HasMouseWorld = true; // 포인터 유효
                }
            }
#endif

            input.Turn = Mathf.Clamp(input.Turn, -1f, 1f); // 턴 제한
            input.Move = Vector2.ClampMagnitude(input.Move, 1f); // 방향 제한
            return input; // 결과
        }

        private void ApplyControl(WormInput input, float deltaTime) // 조작 적용
        {
            if (TryApplyAutoOrbitControl(deltaTime))
            {
                return; // 자동궤도 조향
            }

            switch (ControlMode)
            {
                case ConvoyControlMode.WasdDirection:
                    currentForwardSpeed = GetAutoForwardSpeed(); // 자동 전진
                    ApplyDirectionSteer(GetCameraRelativeDirection(input.Move), deltaTime); // WASD 방향
                    break;
                case ConvoyControlMode.MousePointer:
                    currentForwardSpeed = GetAutoForwardSpeed(); // 자동 전진
                    ApplyDirectionSteer(GetMouseDirection(input), deltaTime); // 마우스 방향
                    break;
                case ConvoyControlMode.WasdManualForward:
                    currentForwardSpeed = HasMoveInput(input.Move) ? BaseSpeed : 0f; // 입력 시 전진
                    ApplyDirectionSteer(GetCameraRelativeDirection(input.Move), deltaTime); // WASD 방향
                    break;
                default:
                    currentForwardSpeed = GetAutoForwardSpeed(); // 자동 전진
                    ApplyTurnInput(input.Turn, deltaTime); // 좌우 턴
                    break;
            }
        }

        private float GetAutoForwardSpeed() // 자동 속도
        {
            return Mathf.Max(0f, BaseSpeed); // 음수 방지
        }

        private void ApplyTurnInput(float turnInput, float deltaTime) // 턴 입력
        {
            currentTurnInput = Mathf.Clamp(turnInput, -1f, 1f); // 입력 제한
            float targetTurnVelocity = currentTurnInput * GetEffectiveTurnSpeed(); // 목표 회전
            float turnSharpness = Mathf.Abs(currentTurnInput) > 0.01f ? TurnResponse : TurnReleaseResponse; // 반응 선택
            currentTurnVelocity = ExpLerp(currentTurnVelocity, targetTurnVelocity, turnSharpness, deltaTime); // 회전 보간

            transform.Rotate(0f, currentTurnVelocity * deltaTime, 0f, Space.World); // 실제 회전
        }

        private void ApplyDirectionSteer(Vector3 desiredDirection, float deltaTime) // 목표 방향 조향
        {
            desiredDirection.y = 0f; // 평면화

            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                ApplyTurnInput(0f, deltaTime); // 입력 없음
                return; // 조향 종료
            }

            float signedAngle = Vector3.SignedAngle(transform.forward, desiredDirection.normalized, Vector3.up); // 각도 차
            float fullTurnAngle = DirectionSteerFullTurnAngle > 0f ? DirectionSteerFullTurnAngle : 42f; // 최대 입력각
            float turnInput = Mathf.Clamp(signedAngle / fullTurnAngle, -1f, 1f); // 턴 변환
            ApplyTurnInput(turnInput, deltaTime); // 턴 재사용
        }

        private Vector3 GetCameraRelativeDirection(Vector2 move) // 카메라 기준 방향
        {
            if (!HasMoveInput(move))
            {
                return Vector3.zero; // 입력 없음
            }

            Camera camera = Camera.main; // 기준 카메라
            if (camera == null)
            {
                return new Vector3(move.x, 0f, move.y); // 월드 fallback
            }

            Vector3 forward = camera.transform.forward; // 카메라 앞
            Vector3 right = camera.transform.right; // 카메라 오른쪽
            forward.y = 0f; // 기울기 제거
            right.y = 0f; // 기울기 제거

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward; // 앞 fallback
            }

            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right; // 오른쪽 fallback
            }

            return forward.normalized * move.y + right.normalized * move.x; // 합성 방향
        }

        private Vector3 GetMouseDirection(WormInput input) // 마우스 방향
        {
            if (!input.HasMouseWorld)
            {
                return Vector3.zero; // 포인터 없음
            }

            Vector3 direction = input.MouseWorld - transform.position; // 목표 벡터
            direction.y = 0f; // 평면화
            return direction; // 방향 반환
        }

        private static string GetControlModeLabel(ConvoyControlMode mode) // 모드 표시명
        {
            switch (mode)
            {
                case ConvoyControlMode.WasdDirection:
                    return "WASD 조이스틱식"; // 2번
                case ConvoyControlMode.MousePointer:
                    return "마우스 포인터 추적"; // 3번
                case ConvoyControlMode.WasdManualForward:
                    return "전진입력 WASD식"; // 4번
                default:
                    return "진행방향 좌우 턴"; // 1번
            }
        }

        private static bool HasMoveInput(Vector2 move) // 방향 입력 여부
        {
            return move.sqrMagnitude > 0.0001f; // deadzone
        }
    }
}
