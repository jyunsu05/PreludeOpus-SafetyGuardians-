using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIButtonClickSoundPlayer : MonoBehaviour
{
    public static UIButtonClickSoundPlayer Instance { get; private set; }

    [SerializeField] private AudioClip clickClip;

    private AudioSource audioSource;
    private readonly HashSet<int> registeredButtonIds = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureAudioSource();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RegisterAllButtonsInLoadedScenes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RegisterAllButtonsInLoadedScenes();
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public void RegisterAllButtonsInLoadedScenes()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
            RegisterButton(buttons[i]);

        if (UIManager.Instance != null)
            RegisterButtonsInHierarchy(UIManager.Instance.transform);
    }

    public void RegisterButtonsInHierarchy(Transform root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            RegisterButton(buttons[i]);
    }

    public void RegisterButton(Button button)
    {
        if (button == null || clickClip == null)
            return;

        int id = button.GetInstanceID();
        if (registeredButtonIds.Contains(id))
            return;

        registeredButtonIds.Add(id);
        button.onClick.AddListener(PlayClickSound);
    }

    public void PlayClickSound()
    {
        if (clickClip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clickClip);
    }
}
