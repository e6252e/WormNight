using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(BonusChestWaveSpawner))]
    public sealed class BonusChestWaveSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            EditorGUILayout.Space(8.0f);

            DrawSectionTitle("참조 설정");
            DrawHelp("등급별 프리팹이 비어 있으면 기본 상자 프리팹을 사용합니다.");
            DrawProperty("chestPrefab", "기본 상자 프리팹");
            DrawProperty("chestRoot", "상자 생성 부모");

            DrawSectionTitle("생성 위치 설정");
            DrawHelp("보너스 상자는 컨보이 주변에 생성됩니다. 너무 가까우면 선택할 시간이 없으니 최소 반경을 크게 잡는 편이 좋습니다.");
            DrawProperty("spawnAroundConvoy", "컨보이 주변에 생성");
            DrawProperty("fallbackCenter", "대체 기준 위치");
            DrawProperty("minSpawnRadius", "최소 생성 반경");
            DrawProperty("maxSpawnRadius", "최대 생성 반경");
            DrawProperty("groundHeightOffset", "바닥 높이 보정");
            DrawProperty("minChestSpacing", "상자 간 최소 거리");

            DrawSectionTitle("상자 선택 설정");
            DrawHelp("보너스 웨이브마다 여러 상자를 보여주되, 하나만 열 수 있게 만드는 설정입니다.");
            DrawProperty("chestSpawnCount", "상자 생성 개수");
            DrawProperty("allowOnlyOneChoice", "하나만 선택 가능");
            DrawProperty("unselectedChestDestroyDelay", "미선택 상자 제거 대기(초)");

            DrawSectionTitle("등급 확률 설정");
            DrawHelp("확률은 % 기준입니다. 상자 등급은 보상 선택 카드의 레어/유니크 등장 확률 보너스로 연결됩니다.");
            DrawProperty("maxHighGradeCount", "Lv2 이상 최대 등장 수");
            DrawProperty("blockLevel2WhenLevel3Appears", "Lv3 등장 시 Lv2 제외");
            DrawChestGrades();

            EditorGUILayout.Space(8.0f);
            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("보너스 상자 웨이브 생성"))
            {
                foreach (Object targetObject in targets)
                {
                    if (targetObject is BonusChestWaveSpawner spawner)
                    {
                        spawner.SpawnBonusChestWave();
                    }
                }
            }
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = MonoScript.FromMonoBehaviour((BonusChestWaveSpawner)target);
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

        private void DrawChestGrades()
        {
            SerializedProperty grades = serializedObject.FindProperty("chestGrades");
            if (grades == null)
            {
                EditorGUILayout.HelpBox("chestGrades 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            grades.isExpanded = EditorGUILayout.Foldout(grades.isExpanded, "상자 등급 목록", true);
            if (!grades.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;

            int newSize = Mathf.Max(0, EditorGUILayout.IntField("목록 개수", grades.arraySize));
            if (newSize != grades.arraySize)
            {
                grades.arraySize = newSize;
            }

            for (int i = 0; i < grades.arraySize; i++)
            {
                SerializedProperty grade = grades.GetArrayElementAtIndex(i);
                SerializedProperty displayName = grade.FindPropertyRelative("displayName");
                string foldoutLabel = displayName != null && !string.IsNullOrWhiteSpace(displayName.stringValue)
                    ? displayName.stringValue
                    : $"Element {i}";

                grade.isExpanded = EditorGUILayout.Foldout(grade.isExpanded, foldoutLabel, true);
                if (!grade.isExpanded)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                DrawRelativeProperty(grade, "displayName", "등급 이름");
                DrawRelativeProperty(grade, "prefab", "상자 프리팹");
                DrawRelativeProperty(grade, "chancePercent", "등장 확률 (%)");
                DrawRelativeProperty(grade, "overrideRewardChoiceTierChanceBonus", "보상 확률 직접 지정");
                DrawRelativeProperty(grade, "rewardChoiceRareChanceBonusPercent", "보상 레어 확률 보너스 (%)");
                DrawRelativeProperty(grade, "rewardChoiceUniqueChanceBonusPercent", "보상 유니크 확률 보너스 (%)");
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawRelativeProperty(SerializedProperty parent, string propertyName, string label)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"{propertyName} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }
    }
}
