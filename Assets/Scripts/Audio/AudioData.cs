using System;
using UnityEngine;

[Serializable]
public class BGMClipData
{
    [Header("BGM 타입")]
    public BGMType type; //어떤 BGM인지 구분하는 열거형
    public AudioClip clip; //실제 재생할 오디오 파일

    [Range(0f, 1f)]
    public float volume = 1f; //볼륨


}

[Serializable]
public class SFXClipData //효과음쪽
{
    [Header("효과음 타입")]
    public SFXType type; //어떤 효과음인지 구분하는 열거형
    public AudioClip clip; //실제 재생할 효과음 파일

    [Range(0f, 1f)]
    public float volume = 1f; //볼륨

}
