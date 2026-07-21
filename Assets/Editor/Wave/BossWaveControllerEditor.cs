using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(BossWaveController))]
    public sealed class BossWaveControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveInspectorUtility.DrawScriptField(target);

            WaveInspectorUtility.DrawSection("참조");
            WaveInspectorUtility.DrawProperty(serializedObject, "enemySpawner", "몬스터 스포너");
            WaveInspectorUtility.DrawProperty(serializedObject, "bonusChestWaveSpawner", "보너스 상자 스포너");

            WaveInspectorUtility.DrawSection("보스 진행 설정", "보스는 Stage 프로필에 등록된 웨이브에서만 등장합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "blockAdditionalBossWhileAlive", "보스 생존 중 추가 등장 금지");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnChestAfterBossClear", "보스 처치 후 상자 생성");

            WaveInspectorUtility.DrawSection("보스 Stage 규칙", "보스 Stage에서는 기존 몬스터는 남기고, 새 일반/엘리트 스폰만 멈출 수 있습니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "pauseNormalSpawnWhileBossAlive", "보스 Stage 중 새 일반 스폰 중지");
            WaveInspectorUtility.DrawProperty(serializedObject, "endBossStageOnBossClear", "보스 Stage는 처치 시 종료");

            DrawBossStageProfiles();
            DrawBossSequence();
            DrawBossCombinations();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBossStageProfiles()
        {
            WaveInspectorUtility.DrawSection("보스 Stage 프로필", "Stage / HP / 다이아 피해 / 엘리트 소환 구성을 관리합니다.");
            SerializedProperty profiles = serializedObject.FindProperty("bossStageProfiles");

            if (profiles != null)
            {
                EditorGUILayout.PropertyField(profiles, new GUIContent("보스 Stage 목록"), true);
            }
        }

        private void DrawBossSequence()
        {
            WaveInspectorUtility.DrawSection("보스 등장 순서");
            SerializedProperty bosses = serializedObject.FindProperty("bossSequence");
            WaveInspectorUtility.DrawArray(
                bosses,
                "보스 목록",
                GetBossLabel,
                DrawBossBody,
                "+ 보스 추가",
                "- 마지막 보스 삭제");
        }

        private static string GetBossLabel(SerializedProperty boss, int index)
        {
            SerializedProperty id = boss.FindPropertyRelative("bossId");
            SerializedProperty name = boss.FindPropertyRelative("displayName");

            string idValue = id != null && !string.IsNullOrWhiteSpace(id.stringValue) ? id.stringValue : $"B{index + 1:00}";
            string nameValue = name != null && !string.IsNullOrWhiteSpace(name.stringValue) ? name.stringValue : "보스 이름 없음";

            return $"{index + 1}. {idValue} - {nameValue}";
        }

        private static void DrawBossBody(SerializedProperty boss)
        {
            EditorGUILayout.PropertyField(boss.FindPropertyRelative("bossId"), new GUIContent("보스 ID"));
            EditorGUILayout.PropertyField(boss.FindPropertyRelative("displayName"), new GUIContent("보스 이름"));
            EditorGUILayout.PropertyField(boss.FindPropertyRelative("prefab"), new GUIContent("보스 Prefab"));
        }

        private void DrawBossCombinations()
        {
            WaveInspectorUtility.DrawSection("보스 조합 설정", "체크하면 조합 시작 Stage 이후 보스 조합 목록을 순서대로 사용합니다.");

            SerializedProperty enableCombination = serializedObject.FindProperty("enableBossCombination");
            EditorGUILayout.PropertyField(enableCombination, new GUIContent("보스 조합 사용"));

            using (new EditorGUI.DisabledScope(enableCombination == null || !enableCombination.boolValue))
            {
                WaveInspectorUtility.DrawProperty(serializedObject, "bossCombinationStartStage", "보스 조합 시작 Stage");

                SerializedProperty combinations = serializedObject.FindProperty("bossCombinations");
                WaveInspectorUtility.DrawArray(
                    combinations,
                    "보스 조합 목록",
                    GetCombinationLabel,
                    DrawCombinationBody,
                    "+ 조합 추가",
                    "- 마지막 조합 삭제");
            }
        }

        private static string GetCombinationLabel(SerializedProperty combination, int index)
        {
            SerializedProperty id = combination.FindPropertyRelative("combinationId");
            SerializedProperty name = combination.FindPropertyRelative("displayName");

            string idValue = id != null && !string.IsNullOrWhiteSpace(id.stringValue) ? id.stringValue : $"BC{index + 1:00}";
            string nameValue = name != null && !string.IsNullOrWhiteSpace(name.stringValue) ? name.stringValue : "조합 이름 없음";

            return $"{idValue} - {nameValue}";
        }

        private static void DrawCombinationBody(SerializedProperty combination)
        {
            EditorGUILayout.PropertyField(combination.FindPropertyRelative("combinationId"), new GUIContent("조합 ID"));
            EditorGUILayout.PropertyField(combination.FindPropertyRelative("displayName"), new GUIContent("조합 이름"));

            SerializedProperty bosses = combination.FindPropertyRelative("bosses");
            WaveInspectorUtility.DrawArray(
                bosses,
                "같이 나올 보스",
                GetCombinationBossLabel,
                DrawCombinationBossBody,
                "+ 보스 추가",
                "- 마지막 보스 삭제");
        }

        private static string GetCombinationBossLabel(SerializedProperty boss, int index)
        {
            SerializedProperty prefab = boss.FindPropertyRelative("prefab");
            return prefab != null && prefab.objectReferenceValue != null ? prefab.objectReferenceValue.name : $"보스 {index + 1}";
        }

        private static void DrawCombinationBossBody(SerializedProperty boss)
        {
            EditorGUILayout.PropertyField(boss.FindPropertyRelative("prefab"), new GUIContent("보스 Prefab"));
        }
    }
}
