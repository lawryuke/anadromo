using UnityEngine;

public class BloopFishMovement : MonoBehaviour
{
    [Header("Puntos de Movimiento")]
    [Tooltip("Punto de inicio abajo (donde empieza)")]
    public Vector3 puntoInicio = new Vector3(12.9899998f, -8.89000034f, 19.0599995f);
    
    [Tooltip("Punto final arriba")]
    public Vector3 puntoFin = new Vector3(12.9899998f, 80f, 19.0599995f);

    [Header("Configuración")]
    [Tooltip("Velocidad constante de movimiento")]
    public float velocidad = 5f;

    [Header("Control de Teclado")]
    [Tooltip("La tecla que debes presionar para que este pez empiece a subir")]
    public KeyCode teclaParaIniciar = KeyCode.Alpha5;

    private bool animacionIniciada = false;

    void Start()
    {
        // Colocamos al pez en la posición de inicio al arrancar el juego
        transform.position = puntoInicio;
    }

    void Update()
    {
        // Detectar si el usuario presiona la tecla configurada en el Inspector
        if (!animacionIniciada && Input.GetKeyDown(teclaParaIniciar))
        {
            animacionIniciada = true; // Empieza el movimiento
        }

        // Si ya presionaste 5, el pez se empieza a mover
        if (animacionIniciada)
        {
            // Mover progresivamente al pez a velocidad constante hacia el punto final
            transform.position = Vector3.MoveTowards(transform.position, puntoFin, velocidad * Time.deltaTime);

            // Si el pez llega exactamente a su objetivo, se detiene
            if (Vector3.Distance(transform.position, puntoFin) < 0.01f)
            {
                animacionIniciada = false;
            }
        }
    }
}
