using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 배틀 UI 산소 게이지 표시 전용. Slider만 연결하면 플레이어 산소는 자동 연동됩니다.
/// </summary>
public class UIBattleOxygenGauge : MonoBehaviour
{
    [Header("--- 배틀 산소 게이지 UI ---")]
    [Tooltip("배틀 씬에 추가한 산소 Slider")]
    [SerializeField] private Slider oxygenSlider;

    [Tooltip("선택: 현재 산소 수치 텍스트 (예: 85/100)")]
    [SerializeField] private TextMeshProUGUI oxygenValueText;

    [Header("--- 자동 탐색 ---")]
    [SerializeField] private bool autoFindSliderInChildren = true;

    private PlayerOxygen boundPlayerOxygen;
    private Coroutine bindRoutine;

    private void Awake()
    {
        TryResolveSliderReference();
    }

    private void OnEnable()
    {
        TryResolveSliderReference();
        RestartBindRoutine();
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        UnbindPlayerOxygen();
    }

    public void BindToRuntimePlayerOxygen()
    {
        UnbindPlayerOxygen();

        boundPlayerOxygen = PlayerOxygen.ResolveRuntime();
        if (boundPlayerOxygen == null)
            return;

        boundPlayerOxygen.RegisterBattleOxygenGauge(this);
        UpdateGauge(boundPlayerOxygen.currentOxygen, boundPlayerOxygen.maxOxygen);
    }

    public void UpdateGauge(float currentOxygen, float maxOxygen)
    {
        if (oxygenSlider != null)
        {
            oxygenSlider.maxValue = maxOxygen;
            oxygenSlider.value = currentOxygen;
        }

        if (oxygenValueText != null)
            oxygenValueText.text = $"{Mathf.CeilToInt(currentOxygen)}/{Mathf.CeilToInt(maxOxygen)}";
    }

    private void RestartBindRoutine()
    {
        if (bindRoutine != null)
            StopCoroutine(bindRoutine);

        bindRoutine = StartCoroutine(BindWhenReady());
    }

    private IEnumerator BindWhenReady()
    {
        const int maxAttempts = 20;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            BindToRuntimePlayerOxygen();
            if (boundPlayerOxygen != null)
            {
                bindRoutine = null;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            $"[UIBattleOxygenGauge] 씬의 Player에서 PlayerOxygen을 찾지 못했습니다. ({gameObject.name})");
        bindRoutine = null;
    }

    private void UnbindPlayerOxygen()
    {
        if (boundPlayerOxygen == null)
            return;

        boundPlayerOxygen.UnregisterBattleOxygenGauge(this);
        boundPlayerOxygen = null;
    }

    private void TryResolveSliderReference()
    {
        if (oxygenSlider == null)
        {
            if (!autoFindSliderInChildren)
                return;

            oxygenSlider = GetComponentInChildren<Slider>(true);
            if (oxygenSlider == null)
            {
                Debug.LogWarning($"[UIBattleOxygenGauge] Slider가 연결되지 않았습니다. ({gameObject.name})");
                return;
            }
        }

        oxygenSlider.interactable = false;
        TryResolveValueTextReference();
    }

    private void TryResolveValueTextReference()
    {
        if (oxygenValueText == null)
            oxygenValueText = GetComponentInChildren<TextMeshProUGUI>(true);
    }
}
