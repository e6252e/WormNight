using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(ManaOrbCollectSpecialWave))]
    public sealed class ManaOrbCollectSpecialWaveEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveInspectorUtility.DrawScriptField(target);

            WaveInspectorUtility.DrawSection("특수웨이브 등장", "최소 Stage 이후 확률로 마력 구슬 수집 특수웨이브가 시작됩니다. 실패할 때마다 확률이 조금씩 올라갑니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "minStartStage", "최소 등장 Stage");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("baseChancePercent"), "기본 등장 확률");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("chanceIncreaseOnFailPercent"), "실패 후 증가 확률");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("maxChancePercent"), "최대 등장 확률");
            WaveInspectorUtility.DrawProperty(serializedObject, "cooldownStageCount", "재등장 대기 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "blockBossStage", "보스 Stage 제외");

            WaveInspectorUtility.DrawSection("마력 구슬 수집", "Nexus 주변에 마력 구슬을 뿌리고, 제한 시간 동안 먹은 비율을 계산합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "manaOrbPickupPrefab", "마력 구슬 Prefab");
            WaveInspectorUtility.DrawProperty(serializedObject, "manaOrbRoot", "마력 구슬 생성 부모");
            WaveInspectorUtility.DrawProperty(serializedObject, "nexus", "Nexus 기준 위치");
            WaveInspectorUtility.DrawProperty(serializedObject, "manaOrbSpawnCount", "마력 구슬 생성 개수");
            WaveInspectorUtility.DrawProperty(serializedObject, "collectDurationSeconds", "수집 제한 시간 (초)");
            WaveInspectorUtility.DrawProperty(serializedObject, "minSpawnRadius", "최소 생성 반경");
            WaveInspectorUtility.DrawProperty(serializedObject, "maxSpawnRadius", "최대 생성 반경");
            WaveInspectorUtility.DrawProperty(serializedObject, "randomizeSpawnShape", "도형 랜덤 선택");
            WaveInspectorUtility.DrawProperty(serializedObject, "fixedSpawnShape", "고정 도형");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("safetyTextShapeChancePercent"), "안전조차 글자 확률");
            WaveInspectorUtility.DrawProperty(serializedObject, "shapeSpawnRotationDegrees", "도형 회전 각도");
            WaveInspectorUtility.DrawProperty(serializedObject, "manaOrbHeightOffset", "마력 구슬 높이 보정");
            WaveInspectorUtility.DrawProperty(serializedObject, "collectRadius", "획득 거리");

            WaveInspectorUtility.DrawSection("보상 기준", "먹은 비율에 따라 일반/레어/유니크 상자가 중앙 정렬로 생성됩니다.");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("normalChestPercent"), "일반 상자 기준");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("rareChestPercent"), "레어 상자 기준");
            WaveInspectorUtility.DrawPercentProperty(serializedObject.FindProperty("uniqueChestPercent"), "유니크 상자 기준");

            WaveInspectorUtility.DrawSection("보상 상자", "BonusChest 프리팹을 연결합니다. 상자 열기/보상 처리는 기존 BonusChest 로직을 사용합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "normalChestPrefab", "일반 상자");
            WaveInspectorUtility.DrawProperty(serializedObject, "rareChestPrefab", "레어 상자");
            WaveInspectorUtility.DrawProperty(serializedObject, "uniqueChestPrefab", "유니크 상자");
            WaveInspectorUtility.DrawProperty(serializedObject, "chestRoot", "상자 생성 부모");

            WaveInspectorUtility.DrawSection("보상 배치", "Reward Center를 기준으로 상자 1개는 중앙, 2개는 좌우, 3개는 중앙 정렬로 배치합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "rewardCenter", "상자 중앙 위치");
            WaveInspectorUtility.DrawProperty(serializedObject, "fallbackRewardDirection", "기본 배치 방향");
            WaveInspectorUtility.DrawProperty(serializedObject, "fallbackRewardDistance", "Nexus에서 떨어진 거리");
            WaveInspectorUtility.DrawProperty(serializedObject, "chestSpacing", "상자 간격");
            WaveInspectorUtility.DrawProperty(serializedObject, "chestHeightOffset", "상자 높이 보정");
            WaveInspectorUtility.DrawProperty(serializedObject, "rewardStageMaxWaitSeconds", "상자 대기 제한 시간 (초)");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8.0f);
            DrawRuntimeButtons();
        }

        private void DrawRuntimeButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("테스트 시작"))
                {
                    RunForTargets(wave => wave.TestStartSpecialWave());
                }

                if (GUILayout.Button("오브젝트 정리"))
                {
                    RunForTargets(wave => wave.ClearSpecialWaveObjects());
                }
            }
        }

        private void RunForTargets(System.Action<ManaOrbCollectSpecialWave> action)
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is ManaOrbCollectSpecialWave wave)
                {
                    action?.Invoke(wave);
                    EditorUtility.SetDirty(wave);
                }
            }
        }
    }
}
