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

    private void Awake()
    {
        GameplayAudioGuard.Unblock();
        EnsureClickClipLoaded();
        startButton ??= FindButton("StartButton");
        endButton ??= FindButton("End button");
        WireAllButtonClickSounds();
        WireStartButton();
        WireEndButton();
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
        if (clickClip != null)
        {
            UIButtonClickSoundPlayer.PlaySurvivingOneShot(clickClip);
            return;
        }

        UIButtonClickSoundPlayer.Instance?.PlayClickSound(allowWhenBlocked: true);
    }

    private void EnsureClickClipLoaded()
    {
        if (clickClip == null)
            return;

        if (!clickClip.preloadAudioData && clickClip.loadState == AudioDataLoadState.Unloaded)
            clickClip.LoadAudioData();
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
