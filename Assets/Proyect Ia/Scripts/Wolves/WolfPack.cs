using System;
using UnityEngine;

public class WolfPack : MonoBehaviour
{
    // El aviso contiene una observacion pasada, nunca una referencia al objetivo.
    public event Action<WolfAgent, Vector3> AlertRaised;

    public void ReportSighting(WolfAgent source, Vector3 position)
    {
        if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled || source.Pack != this) return;
        AlertRaised?.Invoke(source, position);
    }
}
