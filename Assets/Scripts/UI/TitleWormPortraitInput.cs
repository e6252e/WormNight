using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamProject01.Gameplay
{
    public sealed class TitleWormPortraitInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler // 3D 초상화 마우스 입력
    {
        public TitleWormPortraitPreview Preview; // 조작 대상
        [Range(0.02f, 2f)] public float DragYawSensitivity = 0.35f; // 드래그 민감도
        [Range(0.02f, 0.5f)] public float ScrollZoomStep = 0.12f; // 휠 줌 단위
        public bool InvertDrag; // 드래그 반전
        public bool AllowScrollZoom; // 휠 줌 허용

        private void Awake() // 참조 보정
        {
            ResolvePreview(); // 대상 찾기
        }

        private void OnEnable() // 활성화
        {
            ResolvePreview(); // 대상 찾기
        }

        public void OnPointerDown(PointerEventData eventData) // 클릭 시작
        {
            ResolvePreview(); // 대상 찾기
            if (Preview != null)
            {
                Preview.PauseIdleMotion(); // 자동 회전 정지
            }
        }

        public void OnDrag(PointerEventData eventData) // 드래그 회전
        {
            ResolvePreview(); // 대상 찾기
            if (Preview == null || eventData == null)
            {
                return; // 대상 없음
            }

            float direction = InvertDrag ? 1f : -1f; // 방향
            Preview.AddManualYaw(eventData.delta.x * DragYawSensitivity * direction); // 좌우 회전
        }

        public void OnScroll(PointerEventData eventData) // 휠 확대/축소
        {
            if (!AllowScrollZoom)
            {
                return; // 지렁이 선택 화면에서는 회전만 허용
            }

            ResolvePreview(); // 대상 찾기
            if (Preview == null || eventData == null)
            {
                return; // 대상 없음
            }

            Preview.ZoomBy(-eventData.scrollDelta.y * ScrollZoomStep); // 휠 위=확대
        }

        private void ResolvePreview() // 프리뷰 찾기
        {
            if (Preview == null)
            {
                Preview = FindFirstObjectByType<TitleWormPortraitPreview>(); // 씬 검색
            }
        }
    }
}
