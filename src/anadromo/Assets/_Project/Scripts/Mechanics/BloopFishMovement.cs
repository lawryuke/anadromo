using UnityEngine;

public class BloopFishMovement : MonoBehaviour
{
    [Header("Puntos de Movimiento")]
    [Tooltip("Punto de inicio abajo (donde empieza)")]
    public Vector3 puntoInicio = new Vector3(12.9899998f, -8.89000034f, 19.0599995f);
    
    [Tooltip("Punto final arriba")]
    public Vector3 puntoFin = new Vector3(12.9899998f, 80f, 19.0599995f);

    [Header("Configuración")]
    [Tooltip("Velocidad constante de movimiento de ascenso vertical")]
    public float velocidad = 5.0f;

    [Header("Control de Teclado")]
    [Tooltip("La tecla que debes presionar para que este pez empiece a subir")]
    public KeyCode teclaParaIniciar = KeyCode.Alpha5;

    private bool animacionIniciada = false;
    private NaturalSwimPath movimiento;

    void Start()
    {
        // Colocamos al pez en la posición de inicio al arrancar el juego
        transform.position = puntoInicio;
        movimiento = GetComponent<NaturalSwimPath>();
        if (!movimiento) movimiento = gameObject.AddComponent<NaturalSwimPath>();
        movimiento.acceleration = 0.9f;
        movimiento.courseWidth = .08f;
        // Se mantiene orientToCourse en false para conservar la postura vertical natural del modelo al emerger hacia arriba
        movimiento.orientToCourse = false;
        if (!GetComponent<BloopSwimAnimation>()) gameObject.AddComponent<BloopSwimAnimation>();
    }

    void Update()
    {
        // Detectar si el usuario presiona la tecla configurada en el Inspector
        if (!animacionIniciada && Input.GetKeyDown(teclaParaIniciar))
        {
            IniciarMovimiento();
        }

        if (animacionIniciada && !movimiento.IsSwimming) animacionIniciada = false;
    }

    public void IniciarMovimiento()
    {
        if (animacionIniciada || !movimiento) return;
        animacionIniciada = true;
        movimiento.Begin(puntoFin, velocidad);
    }
}
