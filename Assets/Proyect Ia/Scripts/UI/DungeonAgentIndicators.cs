using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonAgentIndicators : MonoBehaviour
{
    public AgentStatusBadge BadgePrefab;
    public RectTransform BadgeLayer;
    public Sprite SearchIcon, AttackIcon, GatherIcon, FleeIcon, ExitIcon, DeadIcon;
    public Sprite FlockIcon, BaitIcon, SickIcon, WolfIcon;

    private HunterAgent _hunter;
    private IReadOnlyList<BoidAgent> _boids;
    private WolfAgent[] _wolves;
    private Camera _camera;
    private RectTransform _canvas;
    private AgentStatusBadge _hunterBadge;
    private AgentStatusBadge[] _boidBadges, _wolfBadges;
    private bool _preview;

    public void Initialize(HunterAgent hunter, IReadOnlyList<BoidAgent> boids, WolfAgent[] wolves,
        Camera sceneCamera, RectTransform canvas, bool preview = false)
    {
        _preview = preview;
        _hunter = hunter;
        _boids = boids ?? System.Array.Empty<BoidAgent>();
        _wolves = wolves ?? System.Array.Empty<WolfAgent>();
        _camera = sceneCamera;
        _canvas = canvas;
        _hunterBadge = CreateBadge(hunter != null ? hunter.transform : null);
        _boidBadges = new AgentStatusBadge[_boids.Count];
        for (int i = 0; i < _boids.Count; i++)
            _boidBadges[i] = CreateBadge(_boids[i] != null ? _boids[i].transform : null);
        _wolfBadges = new AgentStatusBadge[_wolves.Length];
        for (int i = 0; i < _wolves.Length; i++)
            _wolfBadges[i] = CreateBadge(_wolves[i] != null ? _wolves[i].transform : null);
    }

    private AgentStatusBadge CreateBadge(Transform target)
    {
        if (target == null || BadgePrefab == null || BadgeLayer == null) return null;
        AgentStatusBadge badge = Instantiate(BadgePrefab, BadgeLayer);
        if (_preview) badge.gameObject.hideFlags = HideFlags.HideAndDontSave;
        badge.Initialize(target);
        ConfigureBadge(badge);
        return badge;
    }

    private static void ConfigureBadge(AgentStatusBadge badge)
    {
        // Ajustes de esta instancia: el prefab compartido conserva el aspecto de P1.
        badge.Background.enabled = false;
        RectTransform icon = badge.Icon.rectTransform;
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(.5f, .5f);
        icon.anchoredPosition = Vector2.zero;
        icon.sizeDelta = new Vector2(20f, 20f);
        Outline outline = badge.Icon.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.025f, .04f, .055f, .9f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        RectTransform caption = badge.Caption.rectTransform;
        caption.anchorMin = caption.anchorMax = caption.pivot = new Vector2(.5f, .5f);
        caption.anchoredPosition = new Vector2(0f, 20f);
        caption.sizeDelta = new Vector2(72f, 16f);
        badge.Caption.fontSize = 12f;
        badge.Caption.alignment = TextAlignmentOptions.Center;
    }

    private static void OffsetAboveAgent(AgentStatusBadge badge)
    {
        // Desde una cámara cenital, la altura mundial no separa el icono del cuerpo.
        if (badge.gameObject.activeSelf)
            ((RectTransform)badge.transform).anchoredPosition += new Vector2(0f, 28f);
    }

    public void Refresh(bool visible)
    {
        if (_camera == null || _canvas == null || _boidBadges == null) return;
        RefreshHunter(visible);
        for (int i = 0; i < _boids.Count; i++) RefreshBoid(i, visible);
        for (int i = 0; i < _wolves.Length; i++) RefreshWolf(i, visible);
    }

    private void RefreshHunter(bool visible)
    {
        if (_hunter == null || _hunterBadge == null) return;
        State state = _hunter.CurrentState;
        Sprite icon = state is HunterFleeState ? FleeIcon : state is HunterExitState ? ExitIcon
            : state is HunterGatherState ? GatherIcon : state is HunterAttackState ? AttackIcon : SearchIcon;
        _hunterBadge.Refresh(_camera, _canvas, icon, "Cazador", new Color(1f, .66f, .3f),
            visible && !_hunter.IsDead && !_hunter.IsCompletingExit && _hunter.isActiveAndEnabled);
        OffsetAboveAgent(_hunterBadge);
    }

    private void RefreshBoid(int index, bool visible)
    {
        BoidAgent boid = _boids[index];
        AgentStatusBadge badge = _boidBadges[index];
        if (boid == null || badge == null) return;
        Sprite icon = FlockIcon;
        Color color = new Color(.35f, .89f, .92f);
        if (_preview)
        {
            badge.Refresh(_camera, _canvas, icon, "", color, visible && boid.gameObject.activeInHierarchy);
            OffsetAboveAgent(badge);
            return;
        }
        if (!boid.IsAlive)
        {
            icon = DeadIcon;
            color = new Color(1f, .4f, .42f);
        }
        else if (boid.ThreatDetected) icon = FleeIcon;
        else if (boid.InterestTarget != null)
        {
            icon = BaitIcon;
            color = new Color(1f, .83f, .38f);
        }
        if (boid.IsAlive && boid.IsSick)
        {
            color = new Color(.35f, .86f, .65f);
            if (!boid.ThreatDetected) icon = SickIcon;
        }
        badge.Refresh(_camera, _canvas, icon, "", color, visible && boid.gameObject.activeInHierarchy);
        OffsetAboveAgent(badge);
    }

    private void RefreshWolf(int index, bool visible)
    {
        WolfAgent wolf = _wolves[index];
        AgentStatusBadge badge = _wolfBadges[index];
        if (wolf == null || badge == null) return;
        Color color = wolf.HasVisualContact ? new Color(1f, .4f, .42f)
            : wolf.HasLastKnownPosition ? new Color(1f, .66f, .3f) : new Color(.35f, .89f, .92f);
        badge.Refresh(_camera, _canvas, WolfIcon, "L" + (index + 1), color, visible && wolf.isActiveAndEnabled);
        OffsetAboveAgent(badge);
    }

    public void Clear()
    {
        DestroyBadge(_hunterBadge);
        if (_boidBadges != null) foreach (AgentStatusBadge badge in _boidBadges) DestroyBadge(badge);
        if (_wolfBadges != null) foreach (AgentStatusBadge badge in _wolfBadges) DestroyBadge(badge);
        _hunterBadge = null;
        _boidBadges = _wolfBadges = null;
    }

    private static void DestroyBadge(AgentStatusBadge badge)
    {
        if (badge == null) return;
        if (Application.isPlaying) Destroy(badge.gameObject);
        else DestroyImmediate(badge.gameObject);
    }
}
