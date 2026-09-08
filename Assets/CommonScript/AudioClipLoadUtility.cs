using System.Collections;
using UnityEngine;

public static class AudioClipLoadUtility
{
    public static void RequestLoad(AudioClip clip)
    {
        if (clip == null || clip.loadState != AudioDataLoadState.Unloaded)
            return;

        clip.LoadAudioData();
    }

    public static IEnumerator WaitUntilLoaded(AudioClip clip, float timeoutSeconds = 10f)
    {
        if (clip == null)
            yield break;

        RequestLoad(clip);

        float timeoutAt = Time.unscaledTime + timeoutSeconds;
        while (clip.loadState != AudioDataLoadState.Loaded &&
               clip.loadState != AudioDataLoadState.Failed &&
               Time.unscaledTime < timeoutAt)
        {
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            yield return null;
        }
    }

    public static bool IsReadyToPlay(AudioClip clip)
    {
        return clip != null && clip.loadState == AudioDataLoadState.Loaded;
    }
}
