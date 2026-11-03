using UnityEngine;

public class DungeonViewport : MonoBehaviour
{
    private Camera _camera;
    private RectTransform _canvas;
    private Rect _originalRect;
    private float _originalSize;
    private bool _initialized;
    private Camera _background;

    public void Initialize(Camera sceneCamera, RectTransform canvas, Transform generatedParent = null)
    {
        if (_initialized || sceneCamera == null || canvas == null) return;
        _camera = sceneCamera;
        _canvas = canvas;
        _originalRect = _camera.rect;
        _originalSize = _camera.orthographicSize;
        CreateBackgroundCamera(generatedParent != null ? generatedParent : _canvas);
        _initialized = true;
        Refresh();
    }

    private void CreateBackgroundCamera(Transform parent)
    {
        _background = new GameObject("Dungeon UI Background", typeof(Camera)).GetComponent<Camera>();
        _background.gameObject.hideFlags = _camera.gameObject.hideFlags;
        _background.transform.SetParent(parent, false);
        _background.depth = _camera.depth - 1f;
        _background.cullingMask = 0;
        _background.clearFlags = CameraClearFlags.SolidColor;
        _background.backgroundColor = new Color(.025f, .04f, .055f);
    }

    public void Refresh()
    {
        if (!_initialized || _camera == null || _canvas == null) return;
        float width = Mathf.Max(1f, _canvas.rect.width);
        float height = Mathf.Max(1f, _canvas.rect.height);
        float mapWidth = Mathf.Max(1f, width - 384f);
        float mapHeight = Mathf.Max(1f, height - 168f);
        _camera.rect = new Rect(16f / width, 72f / height, mapWidth / width, mapHeight / height);
        // En Editor el aspecto automatico puede tomar el tamaño de otro panel.
        _camera.aspect = mapWidth / mapHeight;
        if (_camera.orthographic)
            _camera.orthographicSize = Mathf.Max(22f, 33f / Mathf.Max(.01f, _camera.aspect));
    }

    public void Restore()
    {
        if (!_initialized) return;
        if (_camera != null)
        {
            _camera.rect = _originalRect;
            _camera.orthographicSize = _originalSize;
            _camera.ResetAspect();
        }
        _initialized = false;
        if (_background != null)
        {
            if (Application.isPlaying) Destroy(_background.gameObject);
            else DestroyImmediate(_background.gameObject);
        }
        _background = null;
    }

    private void OnDestroy() => Restore();
}
