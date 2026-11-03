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
        if (_target == null)
        {
            gameObject.SetActive(false);
            return;
        }
        Vector3 viewport = camera.WorldToViewportPoint(_target.position + Vector3.up * 1.2f);
        visible = visible && _target.gameObject.activeInHierarchy && viewport.z > 0f
            && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        gameObject.SetActive(visible);
        if (!visible) return;
        // Coordenadas normalizadas: no dependen del panel que Unity este dibujando.
        Vector2 normalized = camera.rect.min + Vector2.Scale(camera.rect.size, (Vector2)viewport);
        _rect.anchoredPosition = Vector2.Scale(canvas.rect.size, normalized - _rect.anchorMin);
        Icon.sprite = icon;
        Icon.color = color;
        Caption.text = caption;
    }
}
