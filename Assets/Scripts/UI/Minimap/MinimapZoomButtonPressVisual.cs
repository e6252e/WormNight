using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MinimapZoomButtonPressVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler, ICancelHandler
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image visualImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private Vector2 pressedOffset = new Vector2(1.5f, -1.5f);

        private Button button;
        private Vector2 baseAnchoredPosition;
        private bool hasBasePosition;
        private bool isPressed;

        private void Awake()
        {
            CacheReferences();
            CaptureBasePosition();
            SetPressed(false);
        }

        private void OnEnable()
        {
            CacheReferences();
            CaptureBasePosition();
            SetPressed(false);
        }

        private void OnDisable()
        {
            SetPressed(false);
        }

        public void Configure(RectTransform targetRoot, Image targetImage, Sprite normal, Sprite pressed, Vector2 offset)
        {
            visualRoot = targetRoot;
            visualImage = targetImage;
            normalSprite = normal != null ? normal : targetImage != null ? targetImage.sprite : null;
            pressedSprite = pressed;
            pressedOffset = offset;
            CacheReferences();
            CaptureBasePosition();
            SetPressed(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetPressed(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetPressed(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SetPressed(false);
        }

        public void OnCancel(BaseEventData eventData)
        {
            SetPressed(false);
        }

        private void LateUpdate()
        {
            if (isPressed && !IsAnyPointerPressed())
            {
                SetPressed(false);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SetPressed(false);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SetPressed(false);
            }
        }

        private void CacheReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual") as RectTransform;
            }

            if (visualImage == null && visualRoot != null)
            {
                visualImage = visualRoot.GetComponent<Image>();
            }
        }

        private void CaptureBasePosition()
        {
            if (visualRoot == null)
            {
                return;
            }

            baseAnchoredPosition = isPressed ? visualRoot.anchoredPosition - pressedOffset : visualRoot.anchoredPosition;
            hasBasePosition = true;
        }

        private void SetPressed(bool pressed)
        {
            isPressed = pressed;

            if (visualRoot != null && hasBasePosition)
            {
                visualRoot.anchoredPosition = pressed ? baseAnchoredPosition + pressedOffset : baseAnchoredPosition;
            }

            if (visualImage != null)
            {
                visualImage.sprite = pressed && pressedSprite != null ? pressedSprite : normalSprite;
                visualImage.color = Color.white;
            }
        }

        private bool IsInteractable()
        {
            return button == null || (button.interactable && button.enabled);
        }

        private static bool IsAnyPointerPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.press.isPressed)
                    {
                        return true;
                    }
                }
            }

            Pen pen = Pen.current;
            return pen != null && pen.tip.isPressed;
        }
    }
}
