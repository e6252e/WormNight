using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(EliteMixController))]
    public sealed class EliteMixControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveInspectorUtility.DrawScriptField(target);
            EditorGUILayout.HelpBox("엘리트는 일반 웨이브 수량 일부를 대체하며, 일반 웨이브와 같은 게이트에서 함께 등장합니다.", MessageType.Info);

            WaveInspectorUtility.DrawSection("엘리트 등장 스케줄", "Stage별 엘리트 종류/수량만 정합니다. 실제 스폰 위치와 분배 방식은 기존 스폰 흐름을 사용합니다.");
            DrawScriptedSchedule();

            WaveInspectorUtility.DrawSection("엘리트 비율 설정", "전체 웨이브 수량 중 몇 %를 엘리트로 바꿀지 정합니다.");
            DrawRatioSteps();

            WaveInspectorUtility.DrawSection("엘리트 조합 설정", "엘리트가 등장하는 Stage에서는 아래 조합 후보 중 하나를 가중치로 고릅니다.");
            DrawCompositions();

            WaveInspectorUtility.DrawSection("같이 나오면 안 되는 엘리트 조합", "맵에 살아있는 엘리트의 조합 성격과 이번 Stage 조합 성격이 겹치면 후보에서 제외합니다.");
            DrawBlockedCombinations();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptedSchedule()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useScriptedEliteSchedule"), new GUIContent("고정 등장 스케줄 사용"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("challengeStartStage"), new GUIContent("도전모드 시작 Stage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("challengeBaseEliteCount"), new GUIContent("도전모드 시작 엘리트 수"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("challengeEliteIncreaseIntervalStages"), new GUIContent("엘리트 +1 증가 Stage 간격"));
        }

        private void DrawRatioSteps()
        {
            SerializedProperty steps = serializedObject.FindProperty("eliteRatioSteps");
            WaveInspectorUtility.DrawArray(
                steps,
                "엘리트 비율 단계",
                GetRatioStepLabel,
                DrawRatioStepBody,
                "+ 단계 추가",
                "- 마지막 단계 삭제");
        }

        private static string GetRatioStepLabel(SerializedProperty step, int index)
        {
            SerializedProperty stage = step.FindPropertyRelative("startStage");
            SerializedProperty ratio = step.FindPropertyRelative("eliteRatioPercent");
            int stageValue = stage != null ? stage.intValue : 1;
            int ratioValue = ratio != null ? ratio.intValue : 0;
            return $"Stage {stageValue} - {ratioValue}%";
        }

        private static void DrawRatioStepBody(SerializedProperty step)
        {
            EditorGUILayout.PropertyField(step.FindPropertyRelative("startStage"), new GUIContent("시작 Stage"));
            WaveInspectorUtility.DrawPercentProperty(step.FindPropertyRelative("eliteRatioPercent"), "엘리트 비율");
        }

        private void DrawCompositions()
        {
            SerializedProperty compositions = serializedObject.FindProperty("eliteCompositions");
            WaveInspectorUtility.DrawArray(
                compositions,
                "엘리트 조합",
                GetCompositionLabel,
                DrawCompositionBody,
                "+ 조합 추가",
                "- 마지막 조합 삭제");
        }

        private static string GetCompositionLabel(SerializedProperty composition, int index)
        {
            SerializedProperty id = composition.FindPropertyRelative("compositionId");
            SerializedProperty type = composition.FindPropertyRelative("combinationType");
            SerializedProperty minStage = composition.FindPropertyRelative("minStage");
            string idValue = id != null && !string.IsNullOrWhiteSpace(id.stringValue) ? id.stringValue : $"Element {index}";
            string typeValue = type != null && !string.IsNullOrWhiteSpace(type.stringValue) ? type.stringValue : "성격 미지정";
            int minStageValue = minStage != null ? Mathf.Max(1, minStage.intValue) : 1;
            return $"{idValue} - {typeValue} / Stage {minStageValue}+";
        }

        private static void DrawCompositionBody(SerializedProperty composition)
        {
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("compositionId"), new GUIContent("조합 ID"));
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("minStage"), new GUIContent("최소 등장 Stage"));
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("weight"), new GUIContent("선택 가중치"));
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("combinationType"), new GUIContent("조합 성격"));
            DrawEliteRatios(composition.FindPropertyRelative("elites"));
        }

        private static void DrawEliteRatios(SerializedProperty elites)
        {
            WaveInspectorUtility.DrawArray(
                elites,
                "엘리트 목록",
                GetEliteLabel,
                DrawEliteRatioBody,
                "+ 엘리트 추가",
                "- 마지막 엘리트 삭제");
        }

        private static string GetEliteLabel(SerializedProperty elite, int index)
        {
            SerializedProperty prefab = elite.FindPropertyRelative("prefab");
            SerializedProperty ratio = elite.FindPropertyRelative("ratioPercent");
            string prefabName = prefab != null && prefab.objectReferenceValue != null ? prefab.objectReferenceValue.name : $"엘리트 {index + 1}";
            int ratioValue = ratio != null ? ratio.intValue : 0;
            return $"{prefabName} - {ratioValue}%";
        }

        private static void DrawEliteRatioBody(SerializedProperty elite)
        {
            EditorGUILayout.PropertyField(elite.FindPropertyRelative("prefab"), new GUIContent("Prefab"));
            WaveInspectorUtility.DrawPercentProperty(elite.FindPropertyRelative("ratioPercent"), "비율");
        }

        private void DrawBlockedCombinations()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("checkAliveEliteCombinations"), new GUIContent("살아있는 엘리트 검사 사용"));

            SerializedProperty blockedCombinations = serializedObject.FindProperty("blockedEliteCombinations");
            WaveInspectorUtility.DrawArray(
                blockedCombinations,
                "같이 나오면 안 되는 조합",
                GetBlockedCombinationLabel,
                DrawBlockedCombinationBody,
                "+ 금지 조합 추가",
                "- 마지막 금지 조합 삭제");
        }

        private static string GetBlockedCombinationLabel(SerializedProperty rule, int index)
        {
            SerializedProperty first = rule.FindPropertyRelative("combinationA");
            SerializedProperty second = rule.FindPropertyRelative("combinationB");
            string firstValue = first != null && !string.IsNullOrWhiteSpace(first.stringValue) ? first.stringValue : "성격 A";
            string secondValue = second != null && !string.IsNullOrWhiteSpace(second.stringValue) ? second.stringValue : "성격 B";
            return $"{firstValue} + {secondValue} 금지";
        }

        private static void DrawBlockedCombinationBody(SerializedProperty rule)
        {
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("combinationA"), new GUIContent("조합 A"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("combinationB"), new GUIContent("조합 B"));
        }
    }
}
