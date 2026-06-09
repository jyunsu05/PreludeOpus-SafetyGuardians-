using UnityEngine;

public class SimpleFogSize : MonoBehaviour
{
    [Header("시야 크기 설정")]
    [Tooltip("원하는 기본 스케일 값 (예: 15 또는 20)")]
    public float defaultSightRadius = 7f; 

    private float targetRadius;

    void Start()
    {
        // 시작할 때 원하는 스케일 값으로 초기화
        targetRadius = defaultSightRadius;
        transform.localScale = new Vector3(defaultSightRadius, defaultSightRadius, 1f);
    }

    void Update()
    {
        // 현재 크기에서 목표 크기로 부드럽게 전환 (Lerp)
        float currentScale = transform.localScale.x;
        float nextScale = Mathf.Lerp(currentScale, targetRadius, 5f * Time.deltaTime);
        transform.localScale = new Vector3(nextScale, nextScale, 1f);
    }

    /// <summary>
    /// 외부 코드에서 플레이어의 시야 크기를 바꾸고 싶을 때 호출하는 함수
    /// 예: 손전등 아이템 획득 시 SetVisionSize(25f); 호출하여 부드럽게 시야 확장
    /// </summary>
    public void SetVisionSize(float newRadius)
    {
        targetRadius = newRadius;
    }
}