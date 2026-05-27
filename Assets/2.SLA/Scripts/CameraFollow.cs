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

    private void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 1. 현재 메인 카메라 화면의 월드 기준 가로/세로 반절 크기를 실시간 계산합니다.
        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        // 2. 카메라 시야가 맵 끝 경계선을 절대로 넘지 않도록 제한(Clamp)할 범위를 산출합니다.
        float minX = -mapWidthHalf + camWidth;
        float maxX = mapWidthHalf - camWidth;
        float minY = -mapHeightHalf + camHeight;
        float maxY = mapHeightHalf - camHeight;

        // 맵 크기가 현재 카메라 가로세로 시야보다 좁아져 화면이 망가지는 것을 방지하는 가드
        float clampedX = (minX < maxX) ? Mathf.Clamp(target.position.x, minX, maxX) : 0f;
        float clampedY = (minY < maxY) ? Mathf.Clamp(target.position.y, minY, maxY) : 0f;

        // 3. 카메라의 새로운 목적지 좌표를 설정합니다. (Z축은 유니티 2D 기본 카메라인 transform.position.z 값 유지)
        Vector3 desiredPosition = new Vector3(clampedX, clampedY, transform.position.z);

        // 4. Lerp를 활용해 부드러운 감속 효과를 주며 카메라의 위치를 점진적으로 이동시킵니다.
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}