using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game start screen — StartButton으로 OpeningScene 진입.
/// </summary>
public class UIGameStartScreen : MonoBehaviour
{
    private const string OpeningSceneName = "OpeningScene";

    [SerializeField] private Button startButton;
    [SerializeField] private Button endButton;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip menuBgmClip;
    [SerializeField] [Range(0f, 1f)] private float menuBgmVolume = 0.7f;

    private AudioSource menuBgmSource;

    private void Awake()
    {
        GameplayAudioGuard.Unblock();
        EnsureClipLoaded(clickClip);
        startButton ??= FindButton("StartButton");
        endButton ??= FindButton("End button");
        WireAllButtonClickSounds();
        WireStartButton();
        WireEndButton();
        StartMenuBgm();
    }

    private void OnDestroy()
    {
        StopMenuBgm();
    }

    public void OnStartClick()
    {
        StartCoroutine(PlayClickSoundThenLoadOpeningScene());
    }

    public void OnEndClick()
    {
        StartCoroutine(PlayClickSoundThenQuit());
    }

    private IEnumerator PlayClickSoundThenLoadOpeningScene()
    {
        PlayButtonClickSound();
        yield return new WaitForSecondsRealtime(GetClickSoundDelay());

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadOpeningScene();
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(OpeningSceneName))
        {
            Debug.LogError(
                $"[UIGameStartScreen] '{OpeningSceneName}' 씬을 로드할 수 없습니다. " +
                "Build Settings에 씬이 포함되어 있는지 확인하세요.");
            yield break;
        }

        SceneManager.LoadScene(OpeningSceneName);
    }

    private IEnumerator PlayClickSoundThenQuit()
    {
        PlayButtonClickSound();
        yield return new WaitForSecondsRealtime(GetClickSoundDelay());
        QuitApplication();
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void WireAllButtonClickSounds()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            WireButtonClickSound(buttons[i]);
    }

    private void WireButtonClickSound(Button button)
    {
        if (button == null || button == startButton || button == endButton)
            return;

        button.onClick.AddListener(PlayButtonClickSound);
    }

    private void PlayButtonClickSound()
    {
        AudioClip clip = clickClip ?? UIButtonClickSoundPlayer.Instance?.ClickClip;
        if (clip != null)
        {
            UIButtonClickSoundPlayer.PlaySurvivingOneShot(clip);
            return;
        }

        UIButtonClickSoundPlayer.Instance?.PlayClickSound(allowWhenBlocked: true);
    }

    private void StartMenuBgm()
    {
        if (menuBgmClip == null)
            return;

        EnsureClipLoaded(menuBgmClip);

        menuBgmSource = gameObject.AddComponent<AudioSource>();
        menuBgmSource.playOnAwake = false;
        menuBgmSource.loop = true;
        menuBgmSource.spatialBlend = 0f;
        menuBgmSource.volume = menuBgmVolume;
        menuBgmSource.clip = menuBgmClip;
        menuBgmSource.Play();
    }

    private void StopMenuBgm()
    {
        if (menuBgmSource == null)
            return;

        if (menuBgmSource.isPlaying)
            menuBgmSource.Stop();
    }

    private static void EnsureClipLoaded(AudioClip clip)
    {
        if (clip == null)
            return;

        if (!clip.preloadAudioData && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    private float GetClickSoundDelay()
    {
        if (clickClip == null)
            return 0.08f;

        return Mathf.Clamp(clickClip.length * 0.35f, 0.08f, 0.2f);
    }

    private Button FindButton(string buttonName)
    {
        Transform found = transform.Find($"BG/{buttonName}");
        if (found == null)
            found = transform.Find(buttonName);

        return found != null ? found.GetComponent<Button>() : null;
    }

    private void WireStartButton()
    {
        if (startButton == null)
        {
            Debug.LogWarning("[UIGameStartScreen] StartButton을 찾을 수 없습니다.");
            return;
        }

        startButton.onClick.RemoveListener(OnStartClick);
        startButton.onClick.AddListener(OnStartClick);
    }

    private void WireEndButton()
    {
        if (endButton == null)
        {
            Debug.LogWarning("[UIGameStartScreen] End button을 찾을 수 없습니다.");
            return;
        }

        endButton.onClick.RemoveListener(OnEndClick);
        endButton.onClick.AddListener(OnEndClick);
    }
}
