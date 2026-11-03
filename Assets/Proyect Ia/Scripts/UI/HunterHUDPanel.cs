using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HunterHUDPanel : MonoBehaviour
{
    public TMP_Text StateText, DetailText, ObjectiveText, ProgressText;
    public Image StateIcon, ProgressFill;
    public GameObject ProgressRoot;
    public Sprite SearchIcon, AttackIcon, GatherIcon, FleeIcon, ExitIcon, DeadIcon;

    private HunterAgent _hunter;
    private ScenarioReferences _scenario;

    public void Initialize(HunterAgent hunter, ScenarioReferences scenario)
    {
        _hunter = hunter;
        _scenario = scenario;
    }

    public void Refresh()
    {
        if (_hunter == null) return;
        RefreshAction();
        ObjectiveText.text = $"Recolecciones {_hunter.GatherCount}/{_hunter.RequiredGathers}";
        RefreshProgress();
    }

    private void RefreshAction()
    {
        State state = _hunter.CurrentState;
        Sprite icon = SearchIcon;
        string label = "Search · Buscando";
        string detail = _hunter.IsInHuntingGround ? "Recorre el coto y busca boids." : "Se dirige al coto de caza.";
        if (state is HunterAttackState)
        {
            icon = AttackIcon;
            label = "Attack · Cazando";
            detail = _hunter.HasActiveProjectile ? "Espera el resultado del proyectil."
                : _hunter.Velocity.sqrMagnitude > .01f ? "Persigue al boid que puede ver." : "Ataca al boid objetivo.";
        }
        else if (state is HunterGatherState)
        {
            icon = GatherIcon;
            label = "Gather · Recolectando";
            detail = _hunter.GatherProgress > 0f ? "Recoge el boid eliminado." : "Se acerca al boid eliminado.";
        }
        else if (state is HunterFleeState)
        {
            icon = FleeIcon;
            label = "Flee · Huyendo";
            string refuge = SafeZoneName(_hunter.SelectedSafeZone);
            detail = _hunter.IsWaitingInSafeZone ? "Espera protegido en " + refuge + "."
                : _hunter.SelectedSafeZone != null ? "Avanza hacia " + refuge + "." : "Busca un refugio con camino disponible.";
        }
        else if (state is HunterExitState)
        {
            icon = ExitIcon;
            label = "Exit · Saliendo";
            detail = _hunter.IsCompletingExit ? "Objetivo cumplido. Reinicia el ciclo." : "Lleva las recolecciones a la salida.";
        }
        else if (state is HunterDeadState)
        {
            icon = DeadIcon;
            label = "Dead · Capturado";
            detail = "Reaparecera en la zona inicial.";
        }
        else if (state is HunterPatrolState)
        {
            label = "Patrol · Patrullando";
            detail = "Recorre los puntos de su ruta.";
        }
        else if (state is HunterPlaceInterestState)
        {
            label = "PlaceInterest · Colocando cebo";
            detail = "Se detiene y deja un objeto de interes.";
        }
        StateText.text = label;
        DetailText.text = detail;
        StateIcon.sprite = icon;
        StateIcon.color = state is HunterDeadState ? new Color(1f, .4f, .42f) : new Color(1f, .66f, .3f);
    }

    private void RefreshProgress()
    {
        float progress = 0f;
        string label = string.Empty;
        bool visible = false;
        if (_hunter.IsDead)
        {
            visible = true;
            progress = 1f - _hunter.RespawnRemaining / Mathf.Max(.001f, _hunter.RespawnTime);
            label = $"Reaparicion: {_hunter.RespawnRemaining:0.0} s";
        }
        else if (_hunter.IsWaitingInSafeZone)
        {
            visible = true;
            progress = 1f - _hunter.SafeZoneWaitRemaining / Mathf.Max(.001f, _hunter.SafeZoneWaitTime);
            label = $"Espera segura: {_hunter.SafeZoneWaitRemaining:0.0} s";
        }
        else if (_hunter.CurrentState is HunterGatherState)
        {
            visible = true;
            progress = _hunter.GatherProgress / Mathf.Max(.001f, _hunter.GatherDuration);
            label = $"Recoleccion: {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)} %";
        }
        ProgressRoot.SetActive(visible);
        ProgressFill.fillAmount = Mathf.Clamp01(progress);
        ProgressText.text = label;
    }

    private string SafeZoneName(ScenarioZone zone)
    {
        if (_scenario != null)
            for (int i = 0; i < _scenario.SafeZones.Count; i++)
                if (_scenario.SafeZones[i] == zone) return "Refugio " + (i + 1);
        return "Refugio";
    }
}
