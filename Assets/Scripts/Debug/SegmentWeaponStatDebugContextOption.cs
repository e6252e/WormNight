using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    //전찬우 수정-0622
    public sealed class SegmentWeaponStatDebugContextOption : MonoBehaviour
    {
        [SerializeField] private string segmentId; // 선택할 세그먼트 ID
        [SerializeField] private string displayName; // 버튼 표시 이름
        [SerializeField] private Button button; // 항목 버튼
        [SerializeField] private Text label; // 항목 텍스트
        [SerializeField] private Image background; // 선택 강조 배경

        public string SegmentId => segmentId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? segmentId : displayName;
        public Button Button => button;
        public Image Background => background;

        public void Configure(string id, string display)
        {
            segmentId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            displayName = string.IsNullOrWhiteSpace(display) ? segmentId : display.Trim();
            ResolveReferences();
            RefreshLabel();
        }

        public void RefreshLabel()
        {
            ResolveReferences();
            if (label != null)
            {
                label.text = DisplayName;
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            RefreshLabel();
        }

        private void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (label == null && button != null)
            {
                label = button.GetComponentInChildren<Text>(true);
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }
        }
    }
}
