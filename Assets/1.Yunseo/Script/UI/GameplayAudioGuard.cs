using UnityEngine;

/// <summary>
/// 게임오버 등 특정 상황에서 배경·캐릭터·UI 사운드 재생을 막고 기존 재생을 정지합니다.
/// </summary>
public static class GameplayAudioGuard
{
    public static bool IsBlocked { get; private set; }

    public static bool CanPlay => !IsBlocked;

    public static void BlockAndStopAll()
    {
        IsBlocked = true;
        StopAllAudioSources();

        UIButtonClickSoundPlayer uiSoundPlayer = UIButtonClickSoundPlayer.Instance;
        if (uiSoundPlayer != null)
            uiSoundPlayer.ForceStopAll();

        FactoryAmbientSoundController[] ambientControllers =
            Object.FindObjectsByType<FactoryAmbientSoundController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < ambientControllers.Length; i++)
        {
            if (ambientControllers[i] != null)
                ambientControllers[i].StopForGameplayAudioBlock();
        }
    }

    public static void Unblock()
    {
        IsBlocked = false;
    }

    private static void StopAllAudioSources()
    {
        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            source.Stop();
            source.loop = false;
        }
    }
}
