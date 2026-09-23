using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Preserves the two authored encounter formations and their keyboard trigger.</summary>
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
    readonly List<NaturalSwimPath> pod = new List<NaturalSwimPath>();
    bool started;

    void Start()
    {
        CollectPod();
    }

    void CollectPod()
    {
        pod.Clear();
        foreach (Transform child in transform)
        {
            var path = child.GetComponent<NaturalSwimPath>();
            if (!path) path = child.gameObject.AddComponent<NaturalSwimPath>();
            path.acceleration = aceleracion;
            path.courseWidth = amplitudTrayectoria;
            path.turnSpeed = 28;
            path.maxBankAngle = 9;
            path.orientToCourse = true;
            if (!child.GetComponent<OrcaSwimAnimation>()) child.gameObject.AddComponent<OrcaSwimAnimation>();
            pod.Add(path);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(teclaParaIniciar) ||
            (teclaParaIniciar == KeyCode.Alpha4 && Input.GetKeyDown(KeyCode.Keypad4))) IniciarGrupo();
    }

    public void IniciarGrupo()
    {
        if (started) return;
        if (pod.Count == 0) CollectPod();
        if (pod.Count == 0) { Debug.LogWarning("No se encontraron orcas hijas para mover.", this); return; }
        started = true;
        StartCoroutine(Launch());
    }

    IEnumerator Launch()
    {
        // Also supports a UnityEvent fired before this component's Start.
        if (pod.Count == 0) yield return null;
        var order = new List<NaturalSwimPath>(pod);
        for (int i = 0; i < order.Count; i++)
        {
            int j = Random.Range(i, order.Count);
            var swap = order[i]; order[i] = order[j]; order[j] = swap;
        }
        foreach (var path in order)
        {
            if (path) path.Begin(path.transform.position + puntoFin - puntoInicio,
                velocidad * Random.Range(1 - variacionVelocidad, 1 + variacionVelocidad));
            yield return new WaitForSeconds(Random.Range(Mathf.Max(0, delayMinimo), Mathf.Max(delayMinimo, delayMaximo)));
        }
    }
}
