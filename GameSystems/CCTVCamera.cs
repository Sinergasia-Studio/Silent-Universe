using UnityEngine;

public class CCTVCamera : MonoBehaviour
{
    [Header("Identity")]
    public string cameraName = "CAM 01";

    [Header("Render Texture")]
    public RenderTexture renderTexture;

    [Header("Pan Limits")]
    [Range(0f, 179f)] public float maxPanLeft  = 60f;
    [Range(0f, 179f)] public float maxPanRight = 60f;
    [Range(0f, 45f)]  public float maxPanUp    = 15f;
    [Range(0f, 45f)]  public float maxPanDown  = 5f;

    [Header("Zoom")]
    public float zoomNormal = 60f;   // FOV default (tidak zoom)
    public float zoomInFov  = 15f;   // FOV saat klik kanan (max zoom in)
    public float zoomSmooth = 6f;    // kecepatan smooth zoom

    [Header("Pan Settings")]
    public float panSpeed       = 40f;
    public float panSmoothSpeed = 6f;

    // ── state ──
    private float  _initialYaw;
    private float  _initialPitch;
    private float  _currentYaw;
    private float  _currentPitch;
    private float  _targetYaw;
    private float  _targetPitch;
    private float  _currentFov;
    private float  _targetFov;
    private bool   _isZoomed;
    private Camera _cam;

    private void Awake()
    {
        _initialYaw   = transform.eulerAngles.y;
        _initialPitch = transform.eulerAngles.x;
        _cam          = GetComponent<Camera>();

        if (_cam != null)
        {
            if (renderTexture != null) _cam.targetTexture = renderTexture;
            _currentFov      = zoomNormal;
            _targetFov       = zoomNormal;
            _cam.fieldOfView = _currentFov;
        }
    }

    /// <summary>
    /// panH: deg/s horizontal | panV: deg/s vertical
    /// rightClick: tahan klik kanan = zoom in, lepas = zoom out
    /// </summary>
    public void ApplyPan(float panH, float panV, bool rightClick, float deltaTime)
    {
        // ── Pan ──
        _targetYaw += panH * deltaTime;
        _targetYaw  = Mathf.Clamp(_targetYaw, -maxPanLeft, maxPanRight);
        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, deltaTime * panSmoothSpeed);

        _targetPitch -= panV * deltaTime;
        _targetPitch  = Mathf.Clamp(_targetPitch, -maxPanUp, maxPanDown);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, deltaTime * panSmoothSpeed);

        transform.rotation = Quaternion.Euler(
            _initialPitch + _currentPitch,
            _initialYaw   + _currentYaw,
            0f
        );

        // ── Zoom (klik kanan toggle) ──
        if (_cam != null)
        {
            _isZoomed   = rightClick;
            _targetFov  = _isZoomed ? zoomInFov : zoomNormal;
            _currentFov = Mathf.Lerp(_currentFov, _targetFov, deltaTime * zoomSmooth);
            _cam.fieldOfView = _currentFov;
        }
    }

    public void ResetPan()
    {
        _targetYaw    = 0f;
        _currentYaw   = 0f;
        _targetPitch  = 0f;
        _currentPitch = 0f;
        _isZoomed     = false;
        _targetFov    = zoomNormal;
        _currentFov   = zoomNormal;
        transform.rotation = Quaternion.Euler(_initialPitch, _initialYaw, 0f);
        if (_cam != null) _cam.fieldOfView = zoomNormal;
    }

    public bool IsZoomed => _isZoomed;

    private void OnDrawGizmosSelected()
    {
        float yaw   = Application.isPlaying ? _initialYaw   : transform.eulerAngles.y;
        float pitch = Application.isPlaying ? _initialPitch : transform.eulerAngles.x;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * 3f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, Quaternion.Euler(pitch, yaw - maxPanLeft,  0) * Vector3.forward * 3f);
        Gizmos.DrawRay(transform.position, Quaternion.Euler(pitch, yaw + maxPanRight, 0) * Vector3.forward * 3f);
        Gizmos.DrawRay(transform.position, Quaternion.Euler(pitch - maxPanUp,   yaw, 0) * Vector3.forward * 3f);
        Gizmos.DrawRay(transform.position, Quaternion.Euler(pitch + maxPanDown, yaw, 0) * Vector3.forward * 3f);
    }
}