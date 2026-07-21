using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(BonusChest))]
    public sealed class BonusChestEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            EditorGUILayout.Space(8.0f);

            DrawSectionTitle("상자 감지 설정");
            DrawHelp("컨보이 머리가 열림 거리 안에 들어오면 상자가 열리고, 보상 선택 거리 안에서 딜레이가 끝나면 보상 화면이 열립니다.");
            DrawProperty("openDistance", "열림 거리");
            DrawProperty("collectDistance", "보상 선택 거리");

            DrawSectionTitle("보상 선택 설정");
            DrawHelp("상자는 직접 경험치/골드를 드랍하지 않고 보상 선택 화면을 엽니다. 레어/유니크 보너스는 보상 카드 등급 확률에 더해집니다.");
            DrawProperty("rewardChoiceRareChanceBonusPercent", "보상 레어 확률 보너스(%)");
            DrawProperty("rewardChoiceUniqueChanceBonusPercent", "보상 유니크 확률 보너스(%)");
            DrawProperty("rewardDropDelay", "보상 선택 딜레이(초)");

            DrawSectionTitle("애니메이션 설정");
            DrawHelp("상자가 자동으로 열리면 애니메이터 정지를 켜고, 너무 느리면 열림 애니메이션 속도를 올립니다.");
            DrawProperty("animator", "상자 애니메이터");
            DrawProperty("openTriggerName", "열림 트리거 이름");
            DrawProperty("openAnimationSpeed", "열림 애니메이션 속도");
            DrawProperty("openAnimationStart", "열림 애니메이션 시작 지점");
            DrawProperty("pauseAnimatorUntilOpen", "열리기 전 애니메이터 정지");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = MonoScript.FromMonoBehaviour((BonusChest)target);
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }
        }

        private static void DrawSectionTitle(string title)
        {
            EditorGUILayout.Space(8.0f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void DrawHelp(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                EditorGUILayout.HelpBox($"{propertyName} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }
    }
}
