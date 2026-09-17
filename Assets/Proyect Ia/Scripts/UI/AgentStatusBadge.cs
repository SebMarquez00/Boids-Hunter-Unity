using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Indicador visual: lee al agente, nunca decide su comportamiento.
public class AgentStatusBadge : MonoBehaviour
{
    public Image Icon;
    public Image Background;
    public TMP_Text Caption;
    private Transform _target;
    private RectTransform _rect;

    public void Initialize(Transform target)
    {
        _target = target;
        _rect = (RectTransform)transform;
    }

    public void Refresh(Camera camera, RectTransform canvas, Sprite icon, string caption,
        Color color, bool visible)
    {
        if (_target == null) { gameObject.SetActive(false); return; }
        Vector3 screen = camera.WorldToScreenPoint(_target.position + Vector3.up * 1.2f);
        visible = visible && _target.gameObject.activeInHierarchy && screen.z > 0f
            && camera.pixelRect.Contains(screen);
        gameObject.SetActive(visible);
        if (!visible) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 point);
        _rect.anchoredPosition = point;
        Icon.sprite = icon;
        Icon.color = color;
        Caption.text = caption;
    }
}
