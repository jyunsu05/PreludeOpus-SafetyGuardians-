using UnityEngine;
using System;

public class PollutionManager : MonoBehaviour
{
    public const float DefaultInitialPollution = 100f;

    public static PollutionManager Instance { get; private set; }

    [SerializeField] private float currentPollution;
    [SerializeField] private float maxPollution = 100f;
    [SerializeField] private float defaultInitialPollution = DefaultInitialPollution;

    public event Action<float, float> OnPollutionChanged;

    public float CurrentPollution => currentPollution;
    public float MaxPollution => maxPollution;

    public static PollutionManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PollutionManager[] managers =
            FindObjectsByType<PollutionManager>(FindObjectsInactive.Include);
        return managers.Length > 0 ? managers[0] : null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (currentPollution <= 0f)
                SetPollution(defaultInitialPollution);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        if (Instance == this)
            NotifyPollutionChanged();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddPollution(float amount)
    {
        if (amount <= 0f) return;

        currentPollution = Mathf.Clamp(currentPollution + amount, 0f, maxPollution);
        NotifyPollutionChanged();
    }

    public void ResetPollution()
    {
        currentPollution = 0f;
        NotifyPollutionChanged();
    }

    /// <summary>새 게임·전체 리셋 시 목표 오염도를 적용합니다(0 이하이면 defaultInitialPollution 사용).</summary>
    public void ApplyInitialPollution(float value)
    {
        float target = value > 0f ? value : defaultInitialPollution;
        SetPollution(target);
    }

    public void SetPollution(float value)
    {
        currentPollution = Mathf.Clamp(value, 0f, maxPollution);
        NotifyPollutionChanged();
    }

    private void NotifyPollutionChanged()
    {
        OnPollutionChanged?.Invoke(currentPollution, maxPollution);
    }
}
