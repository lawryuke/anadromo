using UnityEngine;

public class BloopFishMovement : MonoBehaviour
{
    public Vector3 puntoInicio = new Vector3(11, -19, 21);
    public Vector3 puntoFin = new Vector3(11, 80, 21);
    public float velocidad = 5;
    public KeyCode teclaParaIniciar = KeyCode.Alpha5;
    public bool allowKeyboard = true;
    bool started;
    NaturalSwimPath movement;

    void Awake() => Initialize();

    void Initialize()
    {
        if (movement != null) return;
        transform.position = puntoInicio;
        movement = GetComponent<NaturalSwimPath>();
        if (!movement) movement = gameObject.AddComponent<NaturalSwimPath>();
        movement.acceleration = .9f;
        movement.courseWidth = 0;
        movement.orientToCourse = false;
        if (!GetComponent<BloopSwimAnimation>()) gameObject.AddComponent<BloopSwimAnimation>();
    }

    void Update()
    {
        if (allowKeyboard && Input.GetKeyDown(teclaParaIniciar)) IniciarMovimiento();
    }

    public void IniciarMovimiento() => BeginAscent(puntoFin.y);

    public void BeginAscent(float height)
    {
        if (started) return;
        Initialize();
        started = true;
        puntoFin = new Vector3(transform.position.x, Mathf.Max(height, transform.position.y), transform.position.z);
        movement.Begin(puntoFin, Mathf.Max(.1f, velocidad));
    }
}
