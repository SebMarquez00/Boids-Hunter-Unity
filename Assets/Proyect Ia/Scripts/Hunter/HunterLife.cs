using UnityEngine;

// Conserva la presencia física y visual mientras la FSM administra Dead y su tiempo.
public class HunterLife
{
    private readonly Renderer[] _renderers;
    private readonly Collider[] _colliders;
    private readonly bool[] _rendererStates;
    private readonly bool[] _colliderStates;
    private bool _hidden;

    public HunterLife(Transform body)
    {
        _renderers = body.GetComponentsInChildren<Renderer>(true);
        _colliders = body.GetComponentsInChildren<Collider>(true);
        _rendererStates = new bool[_renderers.Length];
        _colliderStates = new bool[_colliders.Length];
    }

    public void Hide()
    {
        if (_hidden) return;
        _hidden = true;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _rendererStates[i] = _renderers[i].enabled;
            _renderers[i].enabled = false;
        }
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null) continue;
            _colliderStates[i] = _colliders[i].enabled;
            _colliders[i].enabled = false;
        }
    }

    public void Restore()
    {
        if (!_hidden) return;
        _hidden = false;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = _rendererStates[i];
        for (int i = 0; i < _colliders.Length; i++)
            if (_colliders[i] != null) _colliders[i].enabled = _colliderStates[i];
    }
}
