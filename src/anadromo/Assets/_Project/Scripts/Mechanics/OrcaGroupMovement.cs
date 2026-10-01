using System.Collections.Generic;
using UnityEngine;
using Anadromo.Mechanics;

/// <summary>Continuous vertical passes, recycling the spawned pod without growing it.</summary>
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
    sealed class Member
    {
        public NaturalSwimPath path;
        public Vector3 spawnPosition;
        public Quaternion spawnRotation;
        public float launchAt;
        public bool swimming;
    }
    readonly List<Member> pod = new List<Member>();
    bool prepared, started;
    float elapsed, duration;
    public bool IsSpawning { get; private set; }
    public int TotalLaunches { get; private set; }
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
        pod.Add(new Member { path = path, spawnPosition = child.localPosition, spawnRotation = child.localRotation });
    }

    void Update()
    {
        if (allowKeyboard && Input.GetKeyDown(teclaParaIniciar)) IniciarGrupo();
        if (!started) return;
        elapsed += Time.deltaTime;
        if (elapsed >= duration) StopAscent();
        foreach (var member in pod)
        {
            var path = member.path;
            if (path == null) continue;
            if (member.swimming)
            {
                if (path.IsSwimming) continue;
                member.swimming = false;
                path.gameObject.SetActive(false);
                member.launchAt = elapsed + Random.Range(Mathf.Max(.05f, delayMinimo), Mathf.Max(.05f, delayMinimo, delayMaximo));
            }
            if (!IsSpawning || elapsed < member.launchAt) continue;
            path.transform.SetLocalPositionAndRotation(member.spawnPosition, member.spawnRotation);
            path.gameObject.SetActive(true);
            Vector3 target = path.transform.position;
            target.y = Mathf.Max(target.y, puntoFin.y);
            path.Begin(target, Mathf.Max(.1f, velocidad) * Random.Range(1 - variacionVelocidad, 1 + variacionVelocidad));
            member.swimming = true;
            TotalLaunches++;
        }
    }

    public void IniciarGrupo() => BeginAscent(puntoFin.y);

    public void BeginAscent(float height, float spawnSeconds = float.PositiveInfinity)
    {
        if (started) return;
        Prepare();
        if (pod.Count == 0) { Debug.LogError("El grupo no tiene orcas generadas.", this); return; }
        puntoFin.y = height;
        started = true;
        duration = Mathf.Max(0, spawnSeconds);
        IsSpawning = duration > 0;
        elapsed = 0;
        float delay = 0;
        for (int i = 0; i < pod.Count; i++)
        {
            int j = Random.Range(i, pod.Count);
            var swap = pod[i]; pod[i] = pod[j]; pod[j] = swap;
            pod[i].launchAt = delay;
            pod[i].path.gameObject.SetActive(false);
            delay += Random.Range(Mathf.Max(0, delayMinimo), Mathf.Max(delayMinimo, delayMaximo));
        }
    }

    public bool AllAbove(float height)
    {
        if (!started || IsSpawning || pod.Count == 0) return false;
        foreach (var member in pod)
            if (member.swimming && member.path && member.path.transform.position.y < height) return false;
        return true;
    }

    public void StopAscent()
    {
        // Animals already ascending finish their pass; no pending or recycled launches.
        IsSpawning = false;
    }
}
