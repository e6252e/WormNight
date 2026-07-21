using UnityEditor;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(NormalWaveSpawner))]
    public sealed class NormalWaveSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveInspectorUtility.DrawScriptField(target);

            WaveInspectorUtility.DrawSection("참조");
            WaveInspectorUtility.DrawProperty(serializedObject, "enemySpawner", "몬스터 스포너");

            WaveInspectorUtility.DrawSection("수량 설정", "기본 수량에 Stage별 배율을 곱해서 일반 몬스터 총량을 계산합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "baseSpawnCount", "기본 스폰 수");
            DrawScaleSteps();

            WaveInspectorUtility.DrawSection("초중반 일반 수량 보정", "정확히 일치하는 Stage의 일반 몬스터 수만 덮어씁니다. 엘리트 수량은 그대로 둡니다.");
            DrawNormalSpawnCountOverrides();

            WaveInspectorUtility.DrawSection("난이도 배율", "Stage별로 생성 직후 몬스터 체력/이동속도/Nexus 피해 배율을 적용합니다.");
            DrawDifficultySteps();

            WaveInspectorUtility.DrawSection("일반 몬스터 조합", "현재 Stage에서 사용 가능한 조합 중 하나를 가중치로 고릅니다.");
            DrawCompositions();

            WaveInspectorUtility.DrawSection("고급 설정", "초반에 집중해서 나오게 하는 시간/게이트 설정입니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnWindowPercent", "스폰 집중 비율 (%)");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnBatchCount", "기존 묶음 횟수 Fallback");

            WaveInspectorUtility.DrawSection("일반 스폰 분할", "일반 몬스터 총량을 기준으로 1회 스폰 수를 제한하고, 지정 시간 안에 반복 스폰합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "normalSpawnWindowSeconds", "일반 스폰 완료 시간");
            WaveInspectorUtility.DrawProperty(serializedObject, "normalSpawnSplitDivisor", "총량 분할 기준");
            WaveInspectorUtility.DrawProperty(serializedObject, "minMonstersPerSpawnTick", "1회 최소 스폰 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "maxMonstersPerSpawnTick", "1회 최대 스폰 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "avoidPreviousSpawnDirections", "직전 방향 제외");

            WaveInspectorUtility.DrawSection("게이트 설정", "Stage별 활성 게이트 방향 수와 한 번에 사용할 방향 수입니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "batchGateCount", "묶음당 사용 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "earlyGateCount", "초반 사용 게이트 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "midGateStartStage", "중반 게이트 시작 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "midGateCount", "중반 사용 게이트 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "lateGateStartStage", "후반 게이트 시작 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "lateGateCount", "후반 사용 게이트 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "frontRowCount", "앞줄 배치 수");

            WaveInspectorUtility.DrawSection("스폰 대형 설정", "Rows는 기존 사각 오와열, FilledCircleRows는 몬스터를 원형으로 채웁니다. 중심 랜덤 범위는 넥서스 반대쪽 반원만 사용합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnFormationMode", "스폰 대형");
            WaveInspectorUtility.DrawProperty(serializedObject, "disableWaveSpawnRedistribution", "임시 재분배 비활성화");
            WaveInspectorUtility.DrawProperty(serializedObject, "circleFormationCenterJitterRadius", "대형 중심 반원 랜덤 반경");
            WaveInspectorUtility.DrawProperty(serializedObject, "randomizeCircleFormationRotation", "원형 대형 랜덤 회전");

            WaveInspectorUtility.DrawSection("스폰 퍼짐 설정", "선택된 방향 안에서 몬스터가 한 줄로만 나오지 않게 살짝 퍼뜨립니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "useSpawnSpread", "스폰 퍼짐 사용");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnSpreadAmount", "퍼짐 정도");

            WaveInspectorUtility.DrawSection("스폰 혼잡 보정", "켜면 스폰 예정 위치 주변에 몬스터가 많을 때 넥서스 반대 방향으로 더 멀리 생성합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "useCongestionPush", "혼잡 보정 사용");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionCheckRadius", "혼잡 체크 반경");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionMonsterThreshold", "혼잡 판단 몬스터 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionPushDistance", "혼잡 시 뒤로 밀 거리");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionMaxPushDistance", "최대 뒤로 밀 거리");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScaleSteps()
        {
            SerializedProperty steps = serializedObject.FindProperty("spawnScaleSteps");
            WaveInspectorUtility.DrawArray(
                steps,
                "스폰 수 증가 단계",
                GetScaleStepLabel,
                DrawScaleStepBody,
                "+ 단계 추가",
                "- 마지막 단계 삭제");
        }

        private static string GetScaleStepLabel(SerializedProperty step, int index)
        {
            SerializedProperty stage = step.FindPropertyRelative("startStage");
            SerializedProperty scale = step.FindPropertyRelative("spawnScalePercent");
            int stageValue = stage != null ? stage.intValue : 1;
            int scaleValue = scale != null ? scale.intValue : 100;
            return $"Stage {stageValue} - {scaleValue}%";
        }

        private static void DrawScaleStepBody(SerializedProperty step)
        {
            EditorGUILayout.PropertyField(step.FindPropertyRelative("startStage"), new UnityEngine.GUIContent("시작 Stage"));
            DrawSpawnScaleProperty(step.FindPropertyRelative("spawnScalePercent"), "스폰 배율");
        }

        private static void DrawSpawnScaleProperty(SerializedProperty property, string label)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox($"{label} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            int value = EditorGUILayout.IntField($"{label} (%)", property.intValue);
            property.intValue = UnityEngine.Mathf.Max(0, value);
        }

        private void DrawNormalSpawnCountOverrides()
        {
            SerializedProperty overrides = serializedObject.FindProperty("normalSpawnCountOverrides");
            WaveInspectorUtility.DrawArray(
                overrides,
                "일반 몬스터 수량 보정",
                GetNormalSpawnCountOverrideLabel,
                DrawNormalSpawnCountOverrideBody,
                "+ 수량 보정 추가",
                "- 마지막 수량 보정 삭제");
        }

        private static string GetNormalSpawnCountOverrideLabel(SerializedProperty element, int index)
        {
            int stage = element.FindPropertyRelative("stage")?.intValue ?? 1;
            int count = element.FindPropertyRelative("normalSpawnCount")?.intValue ?? 0;
            return $"Stage {stage} - 일반 {count}";
        }

        private static void DrawNormalSpawnCountOverrideBody(SerializedProperty element)
        {
            EditorGUILayout.PropertyField(element.FindPropertyRelative("stage"), new UnityEngine.GUIContent("Stage"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("normalSpawnCount"), new UnityEngine.GUIContent("일반 몬스터 수"));
        }

        private void DrawDifficultySteps()
        {
            SerializedProperty steps = serializedObject.FindProperty("difficultyScaleSteps");
            WaveInspectorUtility.DrawArray(
                steps,
                "난이도 배율 단계",
                GetDifficultyStepLabel,
                DrawDifficultyStepBody,
                "+ 난이도 단계 추가",
                "- 마지막 단계 삭제");
        }

        private static string GetDifficultyStepLabel(SerializedProperty step, int index)
        {
            int stageValue = step.FindPropertyRelative("startStage")?.intValue ?? 1;
            int healthScale = step.FindPropertyRelative("healthScalePercent")?.intValue ?? 100;
            int speedScale = step.FindPropertyRelative("moveSpeedScalePercent")?.intValue ?? 100;
            int damageScale = step.FindPropertyRelative("nexusDamageScalePercent")?.intValue ?? 100;
            return $"Stage {stageValue} - HP {healthScale}% / Speed {speedScale}% / Damage {damageScale}%";
        }

        private static void DrawDifficultyStepBody(SerializedProperty step)
        {
            EditorGUILayout.PropertyField(step.FindPropertyRelative("startStage"), new UnityEngine.GUIContent("시작 Stage"));
            DrawPositiveScaleProperty(step.FindPropertyRelative("healthScalePercent"), "체력 배율");
            DrawPositiveScaleProperty(step.FindPropertyRelative("moveSpeedScalePercent"), "이동속도 배율");
            DrawPositiveScaleProperty(step.FindPropertyRelative("nexusDamageScalePercent"), "Nexus 피해 배율");
        }

        private static void DrawPositiveScaleProperty(SerializedProperty property, string label)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox($"{label} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            int value = EditorGUILayout.IntField($"{label} (%)", property.intValue);
            property.intValue = UnityEngine.Mathf.Max(1, value);
        }

        private void DrawCompositions()
        {
            SerializedProperty compositions = serializedObject.FindProperty("normalCompositions");
            WaveInspectorUtility.DrawArray(
                compositions,
                "일반 몬스터 조합",
                (element, index) => WaveInspectorUtility.GetIdNameLabel(element, "compositionId", "displayName", index),
                DrawCompositionBody,
                "+ 조합 추가",
                "- 마지막 조합 삭제");
        }

        private static void DrawCompositionBody(SerializedProperty composition)
        {
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("compositionId"), new UnityEngine.GUIContent("조합 ID"));
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("displayName"), new UnityEngine.GUIContent("조합 이름"));
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("minStage"), new UnityEngine.GUIContent("최소 등장 Stage"));
            EditorGUILayout.PropertyField(composition.FindPropertyRelative("weight"), new UnityEngine.GUIContent("선택 가중치"));
            DrawMonsterRatios(composition.FindPropertyRelative("monsters"));
        }

        private static void DrawMonsterRatios(SerializedProperty monsters)
        {
            WaveInspectorUtility.DrawArray(
                monsters,
                "몬스터 비율 목록",
                (element, index) => $"몬스터 {index + 1}",
                DrawMonsterRatioBody,
                "+ 몬스터 추가",
                "- 마지막 몬스터 삭제");
        }

        private static void DrawMonsterRatioBody(SerializedProperty monster)
        {
            EditorGUILayout.PropertyField(monster.FindPropertyRelative("prefab"), new UnityEngine.GUIContent("몬스터 Prefab"));
            WaveInspectorUtility.DrawPercentProperty(monster.FindPropertyRelative("ratioPercent"), "비율");
        }
    }
}
