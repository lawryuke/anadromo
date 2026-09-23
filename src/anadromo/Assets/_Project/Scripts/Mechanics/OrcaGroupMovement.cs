using System.Collections.Generic;
using UnityEngine;
using Anadromo.Mechanics;

/// <summary>One vertical pass, using only spawned animals (never the reference model).</summary>
public sealed class OrcaGroupMovement : MonoBehaviour
{
    public Vector3 puntoInicio;
    public Vector3 puntoFin = new Vector3(0, 42, 0);
    public float velocidad = 4;
    [Min(.1f)] public float aceleracion = 1.2f;
    [Range(0, .5f)] public float variacionVelocidad = .1f;
    [Range(0, 1)] public float amplitudTrayectoria = .22f;
    public float delayMinimo;
    public float delayMaximo = 1.5f;
    public KeyCode teclaParaIniciar = KeyCode.Alpha4;
    public bool allowKeyboard = true;
    readonly List<NaturalSwimPath> pod = new List<NaturalSwimPath>();
    readonly List<float> launchTimes = new List<float>();
    bool prepared, started;
    float elapsed;
    int launched;
    public int MemberCount => pod.Count;

    public void Prepare()
    {
        if (prepared) return;
        var spawner = GetComponent<BoxObjectSpawner>();
        if (spawner != null)
        {
            spawner.Initialize();
            foreach (var item in spawner.GeneratedObjects)
                if (item != null && item.activeInHierarchy) AddMember(item.transform);
        }
        else foreach (Transform child in transform)
            if (child.gameObject.activeInHierarchy) AddMember(child);
        prepared = true;
    }

    void AddMember(Transform child)
    {
        var path = child.GetComponent<NaturalSwimPath>();
        if (!path) path = child.gameObject.AddComponent<NaturalSwimPath>();
        path.acceleration = aceleracion;
        path.courseWidth = 0;
        path.turnSpeed = 28;
        path.maxBankAngle = 0;
        path.orientToCourse = true;
        if (!child.GetComponent<OrcaSwimAnimation>()) child.gameObject.AddComponent<OrcaSwimAnimation>();
        pod.Add(path);
    }

    void Update()
    {
        if (allowKeyboard && Input.GetKeyDown(teclaParaIniciar)) IniciarGrupo();
        if (!started) return;
        elapsed += Time.deltaTime;
        while (launched < pod.Count && elapsed >= launchTimes[launched])
        {
            var path = pod[launched++];
            if (path == null) continue;
            Vector3 target = path.transform.position;
            target.y = Mathf.Max(target.y, puntoFin.y);
            path.Begin(target, Mathf.Max(.1f, velocidad) * Random.Range(1 - variacionVelocidad, 1 + variacionVelocidad));
        }
    }

    public void IniciarGrupo() => BeginAscent(puntoFin.y);

    public void BeginAscent(float height)
    {
        if (started) return;
        Prepare();
        if (pod.Count == 0) { Debug.LogError("El grupo no tiene orcas generadas.", this); return; }
        puntoFin.y = height;
        started = true;
        elapsed = 0;
        float delay = 0;
        for (int i = 0; i < pod.Count; i++)
        {
            int j = Random.Range(i, pod.Count);
            var swap = pod[i]; pod[i] = pod[j]; pod[j] = swap;
            launchTimes.Add(delay);
            delay += Random.Range(Mathf.Max(0, delayMinimo), Mathf.Max(delayMinimo, delayMaximo));
        }
    }

    public bool AllAbove(float height)
    {
        if (!started || pod.Count == 0 || launched < pod.Count) return false;
        foreach (var path in pod)
            if (path == null || !path.gameObject.activeInHierarchy || path.transform.position.y < height) return false;
        return true;
    }
}
