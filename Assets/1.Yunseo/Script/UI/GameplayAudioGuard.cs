using UnityEngine;

/// <summary>
/// 게임오버 등 특정 상황에서 배경·캐릭터·UI 사운드 재생을 막고 기존 재생을 정지합니다.
/// </summary>
public static class GameplayAudioGuard
{
    public static bool IsBlocked { get; private set; }
    public static bool IsInventoryFieldSoundsSuppressed { get; private set; }

    public static bool CanPlay => !IsBlocked;

    /// <summary>몬스터·플레이어 등 필드 캐릭터 사운드 재생 가능 여부. 공장 배경음은 별도로 재생됩니다.</summary>
    public static bool CanPlayFieldCharacterSounds =>
        !IsBlocked && !IsInventoryFieldSoundsSuppressed;

    public static void SuppressFieldSoundsForInventory()
    {
        if (IsInventoryFieldSoundsSuppressed)
            return;

        IsInventoryFieldSoundsSuppressed = true;
        StopInventorySuppressedAudio();
    }

    public static void ResumeFieldSoundsFromInventory()
    {
        IsInventoryFieldSoundsSuppressed = false;
    }

    public static void BlockAndStopAll()
    {
        IsBlocked = true;
        IsInventoryFieldSoundsSuppressed = false;
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
        IsInventoryFieldSoundsSuppressed = false;
    }

    private static void StopInventorySuppressedAudio()
    {
        MonsterFieldSoundController[] monsterSounds =
            Object.FindObjectsByType<MonsterFieldSoundController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < monsterSounds.Length; i++)
            monsterSounds[i]?.StopFieldSoundsForInventoryPause();

        PlayerOxygen oxygen = PlayerOxygen.ResolveRuntime();
        oxygen?.PauseFieldAudioForInventory();

        FactoryPipeSmokeSoundZone[] pipeZones =
            Object.FindObjectsByType<FactoryPipeSmokeSoundZone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < pipeZones.Length; i++)
            pipeZones[i]?.StopForInventoryPause();
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
