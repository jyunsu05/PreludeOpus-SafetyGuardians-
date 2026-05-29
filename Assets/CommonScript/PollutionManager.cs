using UnityEngine;
using System;

public class PollutionManager : MonoBehaviour
{
    public static PollutionManager Instance { get; private set; }

    [SerializeField] private float currentPollution = 0f;
    [SerializeField] private float maxPollution = 100f;

    public event Action<float> OnPollutionChanged;

    public float CurrentPollution => currentPollution;
    public float MaxPollution => maxPollution;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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

    private void NotifyPollutionChanged()
    {
        float ratio = maxPollution > 0f ? currentPollution / maxPollution : 0f;
        OnPollutionChanged?.Invoke(ratio);
    }
}
