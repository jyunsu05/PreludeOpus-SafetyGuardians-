using UnityEngine;
using System; // Action 사용을 위해 필수

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
    private bool isPublishingBattleEnded; // 이벤트 호출 중임을 나타내는 플래그

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
        CurrentState = GameState.Battle;
        Debug.Log("[GameManager] 배틀 시작!");
        OnBattleStarted?.Invoke();
    }

    // [수정됨] 배틀 종료 및 필드 복귀 메서드
    public void ReturnToField()
    {
        // 1. 이미 종료 처리 중이라면 중복 호출 방지
        if (isPublishingBattleEnded)
        {
            Debug.LogWarning("[GameManager] 이미 복귀 처리 중입니다. 중복 호출을 차단합니다.");
            return;
        }

        isPublishingBattleEnded = true;
        CurrentState = GameState.Field; // 상태를 먼저 필드로 변경

        Debug.Log("[GameManager] 필드로 복귀합니다.");

        try
        {
            // 2. 이벤트 발생 (구독 중인 PlayerController 및 몬스터들에게 알림)
            OnBattleEnded?.Invoke();
        }
        catch (Exception e)
        {
            // 이벤트 호출 중 에러가 나도 게임이 멈추지 않도록 예외 처리
            Debug.LogError($"[GameManager] 배틀 종료 이벤트 처리 중 오류 발생: {e.Message}");
        }
        finally
        {
            // 3. 어떤 상황에서도 반드시 플래그를 해제하여 다음 배틀 종료가 정상 작동하게 함
            isPublishingBattleEnded = false;
        }
    }

    public void ResetToField()
    {
        CurrentState = GameState.Field;
        isPublishingBattleEnded = false; // 강제 초기화
    }

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