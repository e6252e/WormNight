using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    [CustomEditor(typeof(WaveController))]
    public sealed class WaveControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveInspectorUtility.DrawScriptField(target);

            WaveInspectorUtility.DrawSection("스테이지 진행", "WaveController는 Stage 시간과 각 웨이브 컴포넌트 호출만 담당합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "autoStart", "Play 시작 시 자동 실행");
            WaveInspectorUtility.DrawProperty(serializedObject, "startStage", "시작 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "stageDurationSeconds", "Stage 시간 (초)");

            WaveInspectorUtility.DrawSection("스테이지 클리어 조건", "몬스터를 전부 잡으면 시간과 상관없이 다음 Stage로 넘깁니다. 못 잡으면 시간이 끝날 때 누적됩니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "advanceWhenAllMonstersCleared", "전부 처치 시 다음 Stage");
            WaveInspectorUtility.DrawProperty(serializedObject, "clearCheckDelaySeconds", "처치 판정 대기 시간 (초)");

            WaveInspectorUtility.DrawSection("참조 연결", "실제 스폰 방식은 아래 컴포넌트들이 담당합니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "enemySpawner", "몬스터 스포너");
            WaveInspectorUtility.DrawProperty(serializedObject, "disableSpawnerStageRulesUpdate", "기존 스폰 규칙 중지");
            WaveInspectorUtility.DrawProperty(serializedObject, "normalWaveSpawner", "일반 웨이브 스포너");
            WaveInspectorUtility.DrawProperty(serializedObject, "eliteMixController", "엘리트 섞기 컨트롤러");
            WaveInspectorUtility.DrawProperty(serializedObject, "eliteWaveSpawner", "엘리트 웨이브 스포너");
            WaveInspectorUtility.DrawProperty(serializedObject, "enableBossWave", "보스 웨이브 사용");
            WaveInspectorUtility.DrawProperty(serializedObject, "bossWaveController", "보스 웨이브 컨트롤러");
            WaveInspectorUtility.DrawProperty(serializedObject, "bonusChestWaveSpawner", "보너스 상자 스포너");

            WaveInspectorUtility.DrawSection("특수 웨이브 확장 자리", "보상/마력 구슬 같은 별도 Stage가 필요해지면 여기서 일반 스폰을 잠글 수 있습니다.");
            WaveInspectorUtility.DrawProperty(serializedObject, "enableSpecialWaveExtension", "마력 구슬 수집 특수웨이브 사용");

            SerializedProperty enableSpecial = serializedObject.FindProperty("enableSpecialWaveExtension");
            using (new EditorGUI.DisabledScope(enableSpecial == null || !enableSpecial.boolValue))
            {
                WaveInspectorUtility.DrawProperty(serializedObject, "specialWaveController", "특수 웨이브 컨트롤러 자리");
                WaveInspectorUtility.DrawProperty(serializedObject, "manaOrbCollectSpecialWave", "마력 구슬 수집 특수웨이브");
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
