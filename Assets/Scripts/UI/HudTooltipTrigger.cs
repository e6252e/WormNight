using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamProject01.Gameplay
{
    public sealed class HudTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private HudTooltipContent content = new HudTooltipContent();

        private bool pointerInside;

        public void SetContent(string title, string body, string footer)
        {
            if (content == null)
            {
                content = new HudTooltipContent();
            }

            content.Set(title, body, footer);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            Show(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!pointerInside)
            {
                return;
            }

            HudTooltipManager manager = HudTooltipManager.Instance ?? HudTooltipManager.ResolveFor(transform);
            manager?.MoveTo(eventData != null ? eventData.position : Vector2.zero);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            HudTooltipManager.Instance?.HideTooltip();
        }

        private void OnDisable()
        {
            if (pointerInside)
            {
                pointerInside = false;
                HudTooltipManager.Instance?.HideTooltip();
            }
        }

        private void Show(PointerEventData eventData)
        {
            if (content == null || !content.HasAnyText)
            {
                return;
            }

            HudTooltipManager manager = HudTooltipManager.Instance ?? HudTooltipManager.ResolveFor(transform);
            manager?.ShowTooltip(content, eventData != null ? eventData.position : Vector2.zero);
        }
    }
}
