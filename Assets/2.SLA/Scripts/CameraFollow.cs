using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target; // 카메라가 추적할 대상 (Player)
    [SerializeField] private float smoothSpeed = 0.125f; // 카메라가 따라오는 반응 속도 (낮을수록 더 묵직하고 부드러움)

    [Header("Map Boundaries")]
    // 3배 확대된 공장 맵 이미지 크기(PPU 100 기준)의 가로/세로 절반 값입니다.
    // 14.02 유닛 / 2 * 3배 = 21.03
    // 11.22 유닛 / 2 * 3배 = 16.83
    [SerializeField] private float mapWidthHalf = 21.03f;
    [SerializeField] private float mapHeightHalf = 16.83f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
    }

    /// <summary>추적 대상을 플레이어로 다시 잡습니다.</summary>
    public void RebindToPlayer(bool snapImmediately = true)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;

        if (snapImmediately)
            SnapToTarget();
    }

    /// <summary>현재 추적 대상 위치로 카메라를 즉시 맞춥니다(챕터 재시작·입구 스냅).</summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;

        if (cam == null)
            cam = GetComponent<Camera>();

        transform.position = ComputeClampedCameraPosition(target.position);
    }

    /// <summary>월드 좌표(스폰 포인트 등) 기준으로 카메라를 즉시 맞춥니다.</summary>
    public void SnapToWorldPoint(Vector3 worldPosition)
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        transform.position = ComputeClampedCameraPosition(worldPosition);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = ComputeClampedCameraPosition(target.position);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    private Vector3 ComputeClampedCameraPosition(Vector3 focusWorldPosition)
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        float minX = -mapWidthHalf + camWidth;
        float maxX = mapWidthHalf - camWidth;
        float minY = -mapHeightHalf + camHeight;
        float maxY = mapHeightHalf - camHeight;

        float clampedX = minX < maxX ? Mathf.Clamp(focusWorldPosition.x, minX, maxX) : 0f;
        float clampedY = minY < maxY ? Mathf.Clamp(focusWorldPosition.y, minY, maxY) : 0f;

        return new Vector3(clampedX, clampedY, transform.position.z);
    }
}