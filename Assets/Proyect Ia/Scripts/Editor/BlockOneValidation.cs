using System;
using System.Collections.Generic;
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
            Action[] tests = { ValidateProximity, ValidateBoidPerception, ValidateHunterPerception, ValidateBounds };
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
        Check(results.Count == 25, "El buffer debe crecer sin perder candidatos cuando supera 16 colliders.");

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
        using (SerializedObject data = new SerializedObject(boid))
        {
            data.FindProperty("_currentHealth").floatValue = alive ? 20f : 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
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

    private static void DestroyCreatedObjects()
    {
        for (int i = Created.Count - 1; i >= 0; i--)
            if (Created[i] != null) Object.DestroyImmediate(Created[i]);
        Created.Clear();
        Physics.SyncTransforms();
    }
}
