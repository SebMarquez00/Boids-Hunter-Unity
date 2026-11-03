using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BlockOneValidation
{
    private const int TestLayer = 30;
    private const int TestMask = 1 << TestLayer;
    private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);
    private static readonly List<GameObject> Created = new List<GameObject>();
    private static readonly List<Material> CreatedMaterials = new List<Material>();
    private static int _checks;

    [MenuItem("Tools/IA/Validar bloque 1")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("La validacion del bloque 1 requiere Edit Mode.");

        Scene previousScene = SceneManager.GetActiveScene();
        Scene temporaryScene = default;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        _checks = 0;
        try
        {
            // HideAndDontSave retira colliders de la escena fisica: usamos una escena desechable.
            temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporaryScene);
            Action[] tests = { ValidateProximity, ValidateBoidPerception, ValidateUnavailableHunter,
                ValidateHunterPerception, ValidateBounds, ValidateHealth, ValidateColors,
                ValidateInteraction, ValidateInteractionBounds };
            foreach (Action test in tests)
            {
                try { test(); }
                finally { DestroyCreatedObjects(); }
            }
            Debug.Log($"Bloque 1: {_checks} verificaciones correctas (Edit Mode).");
        }
        finally
        {
            DestroyCreatedObjects();
            UnityEngine.Random.state = randomState;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (temporaryScene.IsValid()) EditorSceneManager.CloseScene(temporaryScene, true);
        }
    }

    private static void ValidateProximity()
    {
        ProximitySensor sensor = new ProximitySensor();
        List<Transform> results = new List<Transform>();
        GameObject target = CreateObject("Objetivo", Vector3.right * 10f);
        Scan(sensor, results, 2f);
        Check(results.Count == 0, "El sensor no debe detectar objetivos fuera del radio.");

        target.transform.position = Origin + Vector3.right;
        Scan(sensor, results, 2f);
        Check(results.Count == 1 && results[0] == target.transform, "Debe adquirir un objetivo que entra al radio.");
        sensor.Scan(Origin, 2f, 1 << 29);
        sensor.GetDetected(results);
        Check(results.Count == 0, "La mascara debe excluir otras capas.");

        target.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;
        Scan(sensor, results, 2f);
        Check(results.Count == 1, "Dos colliders del mismo componente no deben duplicar el candidato.");
        for (int i = 0; i < 24; i++) CreateObject("Candidato " + i, new Vector3(0f, 0f, i * 0.02f));
        Scan(sensor, results, 2f);
        Check(results.Count == 25, "La consulta debe recuperar los 25 candidatos sin duplicarlos.");

        target.SetActive(false);
        sensor.GetDetected(results);
        Check(results.Count == 24 && !results.Contains(target.transform), "Un candidato inactivo deja de verse antes del proximo escaneo.");
        Scan(sensor, results, 2f);
        Check(results.Count == 24, "El nuevo escaneo debe conservar solo candidatos activos.");
        foreach (GameObject created in Created) created.SetActive(false);
        Scan(sensor, results, 2f);
        Check(results.Count == 0, "Un escaneo vacio no debe conservar candidatos anteriores.");
    }

    private static void ValidateBoidPerception()
    {
        BoidAgent owner = CreateBoid("Propietario", Vector3.zero);
        BoidAgent neighbor = CreateBoid("Vecino", Vector3.right);
        CreateBoid("Muerto", Vector3.right * 2f, false);
        CreateBoid("Lejano", Vector3.right * 9f);
        BoidPerception perception = new BoidPerception();
        Refresh(perception, owner);
        Check(perception.Neighbors.Count == 1 && perception.Neighbors[0] == neighbor,
            "El Boid debe excluirse a si mismo, a muertos y a vecinos fuera del radio.");
        Check(perception.VisibleNeighborCount == 1, "El contador debe reflejar vecinos realmente visibles.");
        neighbor.gameObject.SetActive(false);
        Refresh(perception, owner, 0f);
        Check(perception.Neighbors.Count == 0, "Un vecino inactivo debe descartarse sin esperar otro escaneo.");

        HunterAgent first = CreateObject("Cazador A", Vector3.forward * 5.5f).AddComponent<HunterAgent>();
        HunterAgent second = CreateObject("Cazador B", Vector3.forward * 8f).AddComponent<HunterAgent>();
        Refresh(perception, owner);
        Check(perception.Threat == null, "El margen de escape no debe adquirir un cazador nunca visto.");
        second.transform.position = Origin + Vector3.forward * 3f;
        Refresh(perception, owner);
        Check(perception.Threat == second, "Debe adquirir el cazador que entra al radio de vision.");
        first.transform.position = Origin + Vector3.forward * 2f;
        Refresh(perception, owner);
        Check(perception.Threat == first, "Debe poder elegir otro cazador percibido mas cercano.");
        first.gameObject.SetActive(false);
        Refresh(perception, owner, 0f);
        Check(perception.Threat == second, "Al desactivarse la amenaza debe elegir otro cazador visible.");
        second.transform.position = Origin + Vector3.forward * 5f;
        Refresh(perception, owner, 0f);
        Check(perception.Threat == second, "El margen debe conservar solo la amenaza previamente adquirida.");
        second.transform.position = Origin + Vector3.forward * 6.5f;
        Refresh(perception, owner, 0f);
        Check(perception.Threat == null, "Debe perder al cazador cuando supera vision mas margen.");
        first.gameObject.SetActive(true);
        Refresh(perception, owner);
        Check(perception.Threat == first, "Debe volver a adquirir un cazador activo dentro del radio.");
        Object.DestroyImmediate(first.gameObject);
        Refresh(perception, owner, 0f);
        Check(perception.Threat == null, "Una amenaza destruida no debe quedar referenciada.");
    }

    private static void ValidateHunterPerception()
    {
        BoidAgent live = CreateBoid("Vivo", Vector3.right);
        BoidAgent dead = CreateBoid("Muerto", Vector3.right * 2f, false);
        BoidAgent far = CreateBoid("Lejano", Vector3.right * 9f);
        HunterPerception perception = new HunterPerception();
        Physics.SyncTransforms();
        perception.Update(Origin, 4f, 2f, TestMask, 0.1f, 1f);
        Check(perception.FindClosestBoid(Origin, 4f, true) == live, "El Cazador debe elegir un Boid vivo percibido.");
        Check(perception.FindClosestBoid(Origin, 4f, false) == dead, "El Cazador debe distinguir los Boids eliminados.");
        Check(!perception.CanSee(far, Origin, 4f), "El Cazador no debe conocer Boids fuera de su radio.");
        live.gameObject.SetActive(false);
        Check(!perception.CanSee(live, Origin, 4f) && perception.FindClosestBoid(Origin, 4f, true) == null,
            "El Cazador debe perder inmediatamente un Boid desactivado.");
        far.transform.position = Origin + Vector3.right * 3f;
        Check(!perception.CanSee(far, Origin, 4f), "Acercar un Boid no debe detectarlo sin una consulta local.");
        Physics.SyncTransforms();
        perception.Update(Origin, 4f, 2f, TestMask, 0.1f, 1f);
        Check(perception.CanSee(far, Origin, 4f), "La siguiente consulta debe adquirir el Boid que ingreso.");
        far.transform.position = Origin + Vector3.right * 9f;
        Check(!perception.CanSee(far, Origin, 4f), "Debe perder un Boid al alejarse, incluso entre consultas.");
    }

    private static void ValidateUnavailableHunter()
    {
        BoidAgent owner = CreateBoid("Amenaza capturada", Vector3.zero);
        HunterAgent hunter = CreateObject("Cazador capturado", Vector3.forward * 2f).AddComponent<HunterAgent>();
        AgentNavigator navigation = hunter.gameObject.AddComponent<AgentNavigator>();
        ScenarioReferences scenario = CreateObject("Escenario temporal", Vector3.right * 20f).AddComponent<ScenarioReferences>();
        using (SerializedObject data = new SerializedObject(hunter))
        {
            data.FindProperty("_navigation").objectReferenceValue = navigation;
            data.FindProperty("_scenario").objectReferenceValue = scenario;
            data.FindProperty("_respawnTime").floatValue = 0.1f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        hunter.Initialize();
        BoidPerception perception = new BoidPerception();
        Refresh(perception, owner);
        Check(perception.Threat == hunter, "El Boid debe percibir al Cazador disponible de la fixture.");
        Check(hunter.RequestCapture(), "La fixture debe comunicar una captura valida al Cazador.");
        Refresh(perception, owner, 0f);
        Check(hunter.gameObject.activeInHierarchy && !hunter.IsAvailableToWolves && perception.Threat == null,
            "Una captura pendiente debe retirar la amenaza inmediatamente, sin otro escaneo ni desactivar el GameObject.");
        hunter.UpdateAgent(0.02f);
        Refresh(perception, owner, 0f);
        Check(hunter.IsDead && perception.Threat == null, "Un Cazador en Dead no debe mantener al Boid huyendo.");
        hunter.UpdateAgent(0.1f);
        Refresh(perception, owner, 0f);
        Check(!hunter.IsDead && hunter.IsAvailableToWolves && perception.Threat == hunter,
            "La reaparicion debe permitir percibir nuevamente al Cazador disponible.");
    }

    private static void ValidateBounds()
    {
        GameObject area = CreateObject("Coto trasladado", new Vector3(50f, 0f, 70f));
        area.SetActive(false);
        WorldBounds bounds = area.AddComponent<WorldBounds>();
        Vector3 center = area.transform.position;
        Vector3 wrapped = bounds.OutOfBounds(center + new Vector3(9f, 2f, -9f));
        Check(wrapped == center + new Vector3(-8f, 2f, 8f), "Los bounds deben envolver alrededor de su centro trasladado y conservar altura.");
        Check(bounds.OutOfBounds(center) == center, "El centro del coto debe permanecer dentro de los bounds.");
    }

    private static void ValidateHealth()
    {
        BoidHealth health = new BoidHealth();
        health.Reset(20f);
        Check(health.IsAlive && !health.IsSick && health.CurrentHealth == 20f, "Reaparecer debe restaurar vida y quitar enfermedad.");
        health.TakeDamage(-5f);
        health.TakeDamage(0f);
        Check(health.CurrentHealth == 20f, "El dano nulo o negativo no debe modificar la vida.");
        Check(!health.TakeDamage(5f) && health.CurrentHealth == 15f, "Un golpe no letal debe descontar vida sin informar muerte.");
        health.BecomeSick(5f);
        Check(health.IsSick && Mathf.Approximately(health.GetMaxSpeed(3f, 0.35f), 1.05f),
            "La enfermedad debe aplicar la velocidad reducida configurada.");
        health.Tick(4f);
        Check(health.IsSick, "La enfermedad debe durar el tiempo configurado.");
        health.Tick(1f);
        Check(!health.IsSick && health.GetMaxSpeed(3f, 0.35f) == 3f, "Al recuperarse debe volver la velocidad normal.");
        health.BecomeSick(5f);
        Check(health.TakeDamage(100f) && !health.IsAlive && !health.IsSick && health.CurrentHealth == 0f,
            "La muerte debe limitar vida a cero y cancelar la enfermedad.");
        Check(!health.TakeDamage(1f), "Golpear un Boid muerto no debe informar otra muerte.");
        health.Reset(20f);
        Check(health.IsAlive && !health.IsSick, "El mismo Boid debe poder iniciar otro ciclo despues de morir.");
    }

    private static void ValidateColors()
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Created.Add(body);
        body.transform.position = Origin;
        Renderer renderer = body.GetComponent<Renderer>();
        Material material = new Material(renderer.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
        CreatedMaterials.Add(material);
        material.color = Color.blue;
        renderer.sharedMaterial = material;
        BoidFeedback feedback = new BoidFeedback(material);

        feedback.Refresh(true, false, Color.green);
        Check(renderer.sharedMaterial.color == Color.blue, "Un Boid sano debe conservar su color original.");
        feedback.Refresh(true, true, Color.green);
        Check(renderer.sharedMaterial.color == Color.green, "La enfermedad debe mostrar su color configurado.");
        feedback.Refresh(false, true, Color.green);
        Check(renderer.sharedMaterial.color == Color.red, "El color de muerte debe tener prioridad sobre enfermedad.");
        feedback.Refresh(true, false, Color.green);
        Check(renderer.sharedMaterial.color == Color.blue, "La recuperacion o reaparicion debe restaurar el color original.");
    }

    private static void ValidateInteraction()
    {
        BoidAgent owner = CreateBoid("Reserva A", Vector3.zero);
        BoidAgent other = CreateBoid("Reserva B", Vector3.right * 2f);
        InterestObject interest = CreateObject("Interes", Vector3.forward * 0.5f).AddComponent<InterestObject>();
        // Inicializa el objeto sin incrementar manualmente el contador global de OnEnable.
        InvokeFixtureMethod(interest, "Initialize");
        BoidPerception perception = new BoidPerception();
        BoidMovement movement = new BoidMovement(owner.transform);
        BoidInteraction interaction = new BoidInteraction(owner, movement);
        Refresh(perception, owner);
        interaction.UpdateTarget(perception, 3f, false);
        Check(interaction.Target == interest && interest.ReservedBy == owner,
            "Un Boid debe adquirir y reservar el interes que percibe localmente.");
        Check(!interest.TryReserve(other) && !interest.Consume(other, 1f),
            "Un segundo Boid no debe reservar ni consumir el objeto reservado.");
        interaction.Tick(1.1f, 0.65f, 1f, 10f, 0.1f);
        Check(interaction.IsInteracting && movement.Velocity == Vector3.zero,
            "Al interactuar dentro del radio el Boid debe detenerse junto al objeto.");

        interaction.UpdateTarget(perception, 3f, true);
        Check(interaction.Target == null && !interaction.IsInteracting && interest.ReservedBy == null,
            "Una amenaza debe cancelar la interaccion y liberar la reserva.");
        Check(interest.TryReserve(other), "El objeto liberado debe quedar disponible para otro Boid.");
        interest.Release(other);

        interaction.UpdateTarget(perception, 3f, false);
        owner.TakeDamage(float.MaxValue);
        interaction.Tick(1.1f, 0.65f, 1f, 10f, 0f);
        Check(interaction.Target == null && !interaction.IsInteracting && interest.ReservedBy == null,
            "La muerte del propietario debe cancelar la interaccion sin dejar reserva.");

        InvokeFixtureMethod(owner, "ResetAgent");
        interaction.UpdateTarget(perception, 3f, false);
        Check(interest.ReservedBy == owner, "Tras reiniciarse el Boid debe poder reservar nuevamente.");
        owner.gameObject.SetActive(false);
        interaction.Tick(1.1f, 0.65f, 1f, 10f, 0f);
        Check(interaction.Target == null && !interaction.IsInteracting && interest.ReservedBy == null,
            "Desactivar al propietario debe cancelar la interaccion y liberar el objeto.");
    }

    private static void ValidateInteractionBounds()
    {
        GameObject area = CreateObject("Coto trasladado de interaccion", new Vector3(50f, 0f, 70f));
        area.SetActive(false);
        WorldBounds bounds = area.AddComponent<WorldBounds>();
        Vector3 center = area.transform.position;
        BoidAgent owner = CreateBoid("Interaccion junto al borde", Vector3.zero);
        BoidMovement movement = new BoidMovement(owner.transform);
        for (int sign = -1; sign <= 1; sign += 2)
        {
            owner.transform.position = center + new Vector3(sign * 7.95f, 2f, sign * 7.95f);
            movement.InitializeVelocity(3f);
            Vector3 interest = center + new Vector3(sign * 7.9f, 0f, sign * 7.9f);
            movement.StayBeside(interest, 0.65f, bounds);
            Vector3 expected = center + new Vector3(sign * 8f, 2f, sign * 8f);
            Check(Vector3.Distance(owner.transform.position, expected) < 0.002f,
                "Interactuar en una esquina debe conservar al Boid dentro del coto, junto al borde cercano y a la misma altura.");
            Check(bounds.OutOfBounds(owner.transform.position) == owner.transform.position && movement.Velocity == Vector3.zero,
                "El Boid debe permanecer detenido dentro de los bounds durante la interaccion.");
        }
    }

    private static GameObject CreateObject(string name, Vector3 offset)
    {
        GameObject created = new GameObject("Validacion IA - " + name);
        Created.Add(created);
        created.layer = TestLayer;
        created.transform.position = Origin + offset;
        created.AddComponent<SphereCollider>().radius = 0.05f;
        return created;
    }

    private static BoidAgent CreateBoid(string name, Vector3 offset, bool alive = true)
    {
        BoidAgent boid = CreateObject(name, offset).AddComponent<BoidAgent>();
        InvokeFixtureMethod(boid, "ResetAgent");
        if (!alive) boid.TakeDamage(float.MaxValue);
        return boid;
    }

    private static void Scan(ProximitySensor sensor, List<Transform> results, float radius)
    {
        Physics.SyncTransforms();
        sensor.Scan(Origin, radius, TestMask);
        sensor.GetDetected(results);
    }

    private static void Refresh(BoidPerception perception, BoidAgent owner, float deltaTime = 1f)
    {
        Physics.SyncTransforms();
        perception.Update(owner, 4f, 2f, 3f, 2f, TestMask, 0.1f, deltaTime);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Bloque 1: " + message);
        _checks++;
    }

    private static void InvokeFixtureMethod(Component target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException("Fixture: no existe " + target.GetType().Name + "." + methodName + ".");
        method.Invoke(target, null);
    }

    private static void DestroyCreatedObjects()
    {
        for (int i = Created.Count - 1; i >= 0; i--)
            if (Created[i] != null) Object.DestroyImmediate(Created[i]);
        Created.Clear();
        foreach (Material material in CreatedMaterials)
            if (material != null) Object.DestroyImmediate(material);
        CreatedMaterials.Clear();
        Physics.SyncTransforms();
    }
}
