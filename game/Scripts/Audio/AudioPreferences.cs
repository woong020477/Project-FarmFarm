// 역할: BGM/SFX의 범위 검증과 장치 로컬 저장. 클라우드 경제 데이터와 분리한다.
using UnityEngine;

/// <summary>Device-local volume preferences. They never enter the economy/cloud save.</summary>
public readonly struct AudioPreferences
{
    public const string KeyPrefix = "FarmFarm.Audio.v1.";
    public float Music { get; }
    public float Effects { get; }

    public AudioPreferences(float music, float effects)
    {
        Music = Clamp(music);
        Effects = Clamp(effects);
    }

    public static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? .5f : Mathf.Clamp01(value);
    public static AudioPreferences Load(string prefix = KeyPrefix) => new(PlayerPrefs.GetFloat(prefix + "Music", .5f), PlayerPrefs.GetFloat(prefix + "Effects", .5f));
    public void Save(string prefix = KeyPrefix)
    {
        PlayerPrefs.SetFloat(prefix + "Music", Music);
        PlayerPrefs.SetFloat(prefix + "Effects", Effects);
        PlayerPrefs.Save();
    }
}
