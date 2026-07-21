using System.Collections.Generic;

public static class AudioSceneName
{
    private static readonly Dictionary<SceneType, string> sceneTable =
        new Dictionary<SceneType, string>()
        {
            { SceneType.TitleScene, "TitleScene" },
            { SceneType.StageScene, "StageScene" }, // 스테이지 씬 이름 //안건준 수정 - 0628
        };

    private static readonly Dictionary<string, BGMType> bgmTable =
        new Dictionary<string, BGMType>()
        {
            { "TitleScene", BGMType.Title_BGM },
            { "StageScene", BGMType.Stage_BGM }, // StageScene 진입 시 스테이지 BGM 재생 //안건준 추가 - 0628
            //안건준 추가 - 0630: Dev 테스트 씬 — 메인 StageScene과 동일 Stage_BGM
            { "CoreTest_StageScene", BGMType.Stage_BGM },
            { "LevelTest_StageScene", BGMType.Stage_BGM },
            { "MonsterTest_StageScene", BGMType.Stage_BGM },
            { "SegmentTest_StageScene", BGMType.Stage_BGM },
        };

    public static string GetSceneName(SceneType sceneType)
    {
        return sceneTable.TryGetValue(sceneType, out string sceneName)
            ? sceneName
            : string.Empty;
    }

    public static BGMType GetBGMType(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return BGMType.None;
        }

        if (bgmTable.TryGetValue(sceneName, out BGMType bgmType))
        {
            return bgmType;
        }

        //안건준 추가 - 0630: 이름에 Stage 포함 씬은 Stage_BGM (추가 테스트 씬 대응)
        if (sceneName.IndexOf("Stage", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BGMType.Stage_BGM;
        }

        return BGMType.None;
    }
}
