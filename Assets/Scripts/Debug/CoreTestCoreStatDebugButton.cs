using UnityEngine;
using UnityEngine.UI;

namespace TeamProject01.Gameplay
{
    public sealed class CoreTestCoreStatDebugButton : MonoBehaviour
    {
        public enum DebugAction
        {
            LevelUp,
            AddGold,
            AddExperience
        }

        public DebugAction Action;
        public CoreStatProvider CoreStats;
        public Button Button;
        public Text Label;
        [Min(1)] public int Amount = 1;
        public string LabelOverride;

        private void Awake()
        {
            ResolveReferences();
            if (Button != null)
            {
                Button.onClick.RemoveListener(Apply);
                Button.onClick.AddListener(Apply);
            }

            RefreshLabel();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshLabel();
        }

        private void OnDestroy()
        {
            if (Button != null)
            {
                Button.onClick.RemoveListener(Apply);
            }
        }

        public void Apply()
        {
            ResolveReferences();
            if (CoreStats == null)
            {
                Debug.LogWarning("[CoreTest] CoreStatProvider not found.", this);
                return;
            }

            int amount = Mathf.Max(1, Amount);
            if (Action == DebugAction.LevelUp)
            {
                CoreStats.DebugAddLevel(amount);
                Debug.Log($"[CoreTest] Core level +{amount} => Lv.{CoreStats.CurrentLevel}", this);
                return;
            }

            if (Action == DebugAction.AddGold)
            {
                CoreStats.DebugAddGold(amount);
                Debug.Log($"[CoreTest] Gold +{amount} => {CoreStats.CurrentGold}", this);
                return;
            }

            CoreStats.DebugAddExperience(amount);
            Debug.Log($"[CoreTest] Experience +{amount} => {CoreStats.CurrentExperience}/{CoreStats.ExperienceToNextLevel}", this);
        }

        private void ResolveReferences()
        {
            if (CoreStats == null)
            {
                CoreStats = CoreStatProvider.Active != null ? CoreStatProvider.Active : FindFirstObjectByType<CoreStatProvider>();
            }

            if (Button == null)
            {
                Button = GetComponent<Button>();
            }

            if (Label == null && Button != null)
            {
                Label = Button.GetComponentInChildren<Text>(true);
            }
        }

        private void RefreshLabel()
        {
            if (Label == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(LabelOverride))
            {
                Label.text = LabelOverride.Trim();
                return;
            }

            int amount = Mathf.Max(1, Amount);
            if (Action == DebugAction.LevelUp)
            {
                Label.text = $"레벨 +{amount}";
                return;
            }

            Label.text = Action == DebugAction.AddGold ? $"골드 +{amount}" : $"경험치 +{amount}";
        }
    }
}
