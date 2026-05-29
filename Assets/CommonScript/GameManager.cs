using UnityEngine;
using System; // Action 사용을 위해 필수

public class GameManager : MonoBehaviour
{
    // 1. 싱글톤 패턴 (팀 프로젝트에서 접근하기 가장 쉬움)
    public static GameManager Instance { get; private set; }

    // 2. 게임 상태 정의
    public enum GameState { Field, Battle }
    public GameState CurrentState { get; private set; } = GameState.Field;

    // 3. 이벤트 선언 (다른 매니저들이 이 이벤트를 구독함)
    public event Action OnBattleStarted;
    public event Action OnBattleEnded;

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

    // 배틀 시작 메서드
    public void EnterBattle()
    {
        CurrentState = GameState.Battle;
        Debug.Log("[GameManager] 배틀 시작!");
        
        // 구독하고 있는 모든 매니저(UI, Sound 등)에게 알림
        OnBattleStarted?.Invoke();
    }

    // 배틀 종료(복귀) 메서드
    public void ReturnToField()
    {
        CurrentState = GameState.Field;
        Debug.Log("[GameManager] 필드로 복귀!");
        
        // 구독하고 있는 모든 매니저에게 알림
        OnBattleEnded?.Invoke();
    }

    public void ResetToField()
    {
        CurrentState = GameState.Field;
    }
}