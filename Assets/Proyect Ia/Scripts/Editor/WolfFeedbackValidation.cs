using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class WolfFeedbackValidation
{
    private static readonly Color Patrol = new Color(.38f, .53f, .68f);
    private static readonly Color Howl = new Color(1f, .86f, .32f);
    private static readonly Color Received = new Color(.2f, .85f, .94f);
    private static readonly Color Capture = new Color(.95f, .44f, .72f);
    private static int _checks;

    [MenuItem("Tools/IA/Entrega 2/Validar avisos visuales de lobos")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Esta prueba requiere Edit Mode.");
        Scene original = SceneManager.GetActiveScene();
        bool dirty = original.isDirty;
        Scene temporary = default;
        _checks = 0;
        try
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            ValidateSimultaneousAlerts();
            ValidateBurstAndCapture();
            ValidateEnableReset();
            Require(original.isDirty == dirty, "La prueba no debe modificar la escena original.");
            Debug.Log($"Feedback de lobos: {_checks} comprobaciones correctas; aullido y recepcion " +
                "simultaneos, duracion visible, rafagas, captura y reinicio validados.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
        }
    }

    private static void ValidateSimultaneousAlerts()
    {
        using (Fixture f = new Fixture())
        {
            f.HowlAt(0f);
            f.ReceiveAt(0f);
            f.Expect(0f, Howl, "Debe verse el aullido del frame compartido.");
            f.Expect(.4f, Howl, "La recepcion no debe cortar el pulso de aullido.");
            f.Expect(.81f, Received, "La recepcion simultanea debe verse despues del aullido.");
            f.Expect(1.2f, Received, "La recepcion necesita su propio tiempo visible.");
            f.Expect(1.62f, Patrol, "Al terminar los avisos debe recuperarse el color de estado.");
        }
    }

    private static void ValidateBurstAndCapture()
    {
        using (Fixture f = new Fixture())
        {
            f.HowlAt(0f);
            f.Expect(0f, Howl, "El primer aviso debe comenzar inmediatamente.");
            for (int i = 1; i <= 12; i++)
            {
                float now = i * .02f;
                f.ReceiveAt(now);
                f.Refresh(now);
            }
            f.CaptureAt(.3f);
            f.Expect(.3f, Howl, "La captura tampoco debe borrar avisos pendientes.");
            f.Expect(.81f, Received, "La rafaga debe conservar un turno visible de recepcion.");
            f.Expect(1.62f, Capture, "La captura debe verse despues de la recepcion pendiente.");
            f.Expect(2.43f, Patrol, "La rafaga no debe dejar una cola de avisos antiguos.");
        }
    }

    private static void ValidateEnableReset()
    {
        using (Fixture f = new Fixture())
        {
            f.HowlAt(0f);
            f.ReceiveAt(0f);
            f.Refresh(0f);
            f.Reenable();
            f.Expect(.1f, Patrol, "Reactivar el feedback no debe reproducir eventos anteriores.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    private sealed class Fixture : IDisposable
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly GameObject _root;
        private readonly WolfAgent _wolf;
        private readonly WolfFeedback _feedback;
        private readonly MeshRenderer _body;
        private readonly MaterialPropertyBlock _colors = new MaterialPropertyBlock();
        private float _howlAt = float.NegativeInfinity;
        private float _receivedAt = float.NegativeInfinity;
        private float _captureAt = float.NegativeInfinity;

        public Fixture()
        {
            _root = new GameObject("Validacion temporal de feedback") { hideFlags = HideFlags.HideAndDontSave };
            _root.transform.position = Vector3.one * 10000f;
            _wolf = _root.AddComponent<WolfAgent>();
            _wolf.enabled = false;
            _body = _root.AddComponent<MeshRenderer>();
            _feedback = _root.AddComponent<WolfFeedback>();
            typeof(WolfFeedback).GetField("_wolf", PrivateInstance).SetValue(_feedback, _wolf);
            typeof(WolfFeedback).GetField("_bodyRenderer", PrivateInstance).SetValue(_feedback, _body);
            Invoke("OnEnable");
        }

        public void HowlAt(float now) { _howlAt = now; Set("HowlCount", _wolf.HowlCount + 1); }
        public void ReceiveAt(float now) { _receivedAt = now; Set("ReceivedAlertCount", _wolf.ReceivedAlertCount + 1); }
        public void CaptureAt(float now) { _captureAt = now; Set("CaptureCount", _wolf.CaptureCount + 1); }

        public void Refresh(float now)
        {
            // Tambien permite ejecutar esta regresion con el selector anterior sin reloj inyectado.
            Set("LastAlertTime", Time.time - (now - _howlAt));
            Set("LastReceivedAlertTime", Time.time - (now - _receivedAt));
            Set("LastCaptureTime", Time.time - (now - _captureAt));
            MethodInfo actionMethod = typeof(WolfFeedback).GetMethod("GetRecentAction", PrivateInstance);
            object[] args = actionMethod.GetParameters().Length == 0 ? null : new object[] { now };
            string action = (string)actionMethod.Invoke(_feedback, args);
            Invoke("UpdateColor", Patrol, action);
        }

        public void Expect(float now, Color expected, string message)
        {
            Refresh(now);
            _body.GetPropertyBlock(_colors);
            Color actual = _colors.GetColor(Shader.PropertyToID("_BaseColor"));
            Require(((Vector4)(actual - expected)).sqrMagnitude < .0001f, message);
        }

        public void Reenable() { Invoke("OnDisable"); Invoke("OnEnable"); }

        private void Set(string property, object value)
        {
            typeof(WolfAgent).GetProperty(property).GetSetMethod(true).Invoke(_wolf, new[] { value });
        }

        private void Invoke(string method, params object[] args)
        {
            typeof(WolfFeedback).GetMethod(method, PrivateInstance).Invoke(_feedback, args);
        }

        public void Dispose() => Object.DestroyImmediate(_root);
    }
}
