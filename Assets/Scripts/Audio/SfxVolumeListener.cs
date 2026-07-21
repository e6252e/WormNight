using UnityEngine;

[DisallowMultipleComponent]
public sealed class SfxVolumeListener : MonoBehaviour // 씬 SFX AudioSource — 설정 SFX 볼륨 연동 //안건준 추가 - 0628
{
    [SerializeField] private AudioSource target;
    [SerializeField] private float baseVolume = 1f;

    public float BaseVolume => baseVolume;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        AudioManager.RegisterSfxListener(this);
    }

    private void Start()
    {
        if (target != null)
        {
            AudioManager.NotifySfxSourceReady(target, baseVolume);
        }
    }

    private void OnDisable()
    {
        AudioManager.UnregisterSfxListener(this);
    }

    public void SetBaseVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);

        if (target == null)
        {
            target = GetComponent<AudioSource>();
        }

        if (target != null)
        {
            AudioManager.RegisterSfxBaseVolume(target, baseVolume);
        }
    }

    public void ApplyVolume(float sfxVolume, float masterVolume)
    {
        if (target == null)
        {
            target = GetComponent<AudioSource>();
        }

        if (target == null)
        {
            return;
        }

        target.volume = Mathf.Clamp01(baseVolume * sfxVolume * masterVolume);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (target == null)
        {
            target = GetComponent<AudioSource>();
        }
    }
#endif
}
