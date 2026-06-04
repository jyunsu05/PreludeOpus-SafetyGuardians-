using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Field, Battle }
    public GameState CurrentState { get; private set; } = GameState.Field;

    public event Action OnBattleStarted;
    public event Action OnBattleEnded;
    public event Action OnStageCleared;
    public event Action OnStageMonstersSpawned;

    private bool stageClearPending;
    private bool isPublishingBattleEnded;

    private void Awake()
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

    public void EnterBattle()
    {
        if (CurrentState == GameState.Battle)
            return;

        CurrentState = GameState.Battle;
        Debug.Log("[GameManager] 배틀 시작!");
        OnBattleStarted?.Invoke();
    }

    public void ReturnToField()
    {
        if (isPublishingBattleEnded)
        {
            Debug.LogWarning("[GameManager] 배틀 종료 처리가 이미 진행 중입니다. 중복 호출을 차단합니다.");
            return;
        }

        isPublishingBattleEnded = true;
        CurrentState = GameState.Field;

        Debug.Log("[GameManager] 필드로 복귀합니다.");

        try
        {
            OnBattleEnded?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] OnBattleEnded 처리 중 오류: {e.Message}");
        }
        finally
        {
            isPublishingBattleEnded = false;
        }
    }

    public void ResetToField()
    {
        CurrentState = GameState.Field;
        isPublishingBattleEnded = false;
    }

    public bool IsInBattle => CurrentState == GameState.Battle;

    public void NotifyStageCleared()
    {
        stageClearPending = true;
        Debug.Log("[GameManager] 스테이지 클리어!");
    }

    public bool ConsumeStageClearPending()
    {
        if (!stageClearPending)
            return false;

        stageClearPending = false;
        OnStageCleared?.Invoke();
        return true;
    }

    public void NotifyStageMonstersSpawned()
    {
        stageClearPending = false;
        OnStageMonstersSpawned?.Invoke();
    }
}
