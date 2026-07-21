using UnityEditor;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(EliteWaveSpawner))]
    public sealed class EliteWaveSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveInspectorUtility.DrawScriptField(target);

            WaveInspectorUtility.DrawSection("참조");
            WaveInspectorUtility.DrawProperty(serializedObject, "enemySpawner", "몬스터 스포너");
            WaveInspectorUtility.DrawProperty(serializedObject, "normalWaveSpawner", "일반 웨이브 스포너");

            WaveInspectorUtility.DrawSection("엘리트 지연 스폰", "엘리트 등장표로 만든 플랜을 웨이브 시작 후 지정 시간 뒤에 별도 생성합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnDelaySeconds", "스폰 지연 시간");
            WaveInspectorUtility.DrawProperty(serializedObject, "earlyGateCount", "초반 사용 게이트 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "midGateStartStage", "중반 게이트 시작 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "midGateCount", "중반 사용 게이트 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "lateGateStartStage", "후반 게이트 시작 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "lateGateCount", "후반 사용 게이트 방향 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "frontRowCount", "앞줄 배치 수");

            WaveInspectorUtility.DrawSection("엘리트 대형 설정", "현재는 엘리트 조합을 한 묶음으로 유지하며 기존 외곽 스폰 API를 사용합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnFormationMode", "스폰 대형");
            WaveInspectorUtility.DrawProperty(serializedObject, "keepEliteCombinationAsSingleGroup", "엘리트 조합 한 묶음 유지");
            WaveInspectorUtility.DrawProperty(serializedObject, "circleFormationCenterJitterRadius", "대형 중심 반원 랜덤 반경");
            WaveInspectorUtility.DrawProperty(serializedObject, "randomizeCircleFormationRotation", "원형 대형 랜덤 회전");

            WaveInspectorUtility.DrawSection("스폰 퍼짐 설정");
            WaveInspectorUtility.DrawProperty(serializedObject, "useSpawnSpread", "스폰 퍼짐 사용");
            WaveInspectorUtility.DrawProperty(serializedObject, "spawnSpreadAmount", "퍼짐 정도");

            WaveInspectorUtility.DrawSection("스폰 혼잡 보정");
            WaveInspectorUtility.DrawProperty(serializedObject, "useCongestionPush", "혼잡 보정 사용");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionCheckRadius", "혼잡 체크 반경");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionMonsterThreshold", "혼잡 판단 몬스터 수");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionPushDistance", "혼잡 시 뒤로 밀 거리");
            WaveInspectorUtility.DrawProperty(serializedObject, "congestionMaxPushDistance", "최대 뒤로 밀 거리");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
