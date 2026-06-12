using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]
public class LetterBox : MonoBehaviour
{
    private const float TargetAspect = 16f / 9f;
    private const int BackgroundCameraDepth = -100;

    private Camera _camera;
    private Camera _backgroundCamera;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Start()
    {
        ApplyLetterBox();
    }

    private void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            ApplyLetterBox();
    }

    private void ApplyLetterBox()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        _camera.rect = CalculateViewportRect();
        EnsureBackgroundCamera();
        FitOverlayCanvasScalers();
    }

    private static Rect CalculateViewportRect()
    {
        float windowAspect = (float)Screen.width / Screen.height;

        if (windowAspect >= TargetAspect)
        {
            float viewportWidth = TargetAspect / windowAspect;
            return new Rect((1f - viewportWidth) * 0.5f, 0f, viewportWidth, 1f);
        }

        float viewportHeight = windowAspect / TargetAspect;
        return new Rect(0f, (1f - viewportHeight) * 0.5f, 1f, viewportHeight);
    }

    private void EnsureBackgroundCamera()
    {
        if (_backgroundCamera == null)
        {
            var backgroundObject = new GameObject("LetterboxBackground");
            _backgroundCamera = backgroundObject.AddComponent<Camera>();
            _backgroundCamera.depth = BackgroundCameraDepth;
            _backgroundCamera.cullingMask = 0;
        }

        _backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        _backgroundCamera.backgroundColor = Color.black;
        _backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private static void FitOverlayCanvasScalers()
    {
        float windowAspect = (float)Screen.width / Screen.height;
        float match = windowAspect >= TargetAspect ? 1f : 0f;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;

            scaler.matchWidthOrHeight = match;
        }
    }
}
