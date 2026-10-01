using UnityEngine;

public class BloopFishMovement : MonoBehaviour
{
    public Vector3 puntoInicio = new Vector3(11, -19, 21);
    public Vector3 puntoFin = new Vector3(11, 80, 21);
    public float velocidad = 5;
    public KeyCode teclaParaIniciar = KeyCode.Alpha5;
    public bool allowKeyboard = true;
    [Header("Contacto letal")]
    [Tooltip("Opcional: cápsula manual. Si está vacía, se genera una ajustada al cuerpo al iniciar.")]
    public CapsuleCollider lethalBody;
    public bool HasStarted => started;
    bool started;
    NaturalSwimPath movement;

    void Awake() => Initialize();

    void Initialize()
    {
        if (movement != null) return;
        transform.position = puntoInicio;
        PrepareLethalBody();
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
        lethalBody.enabled = true;
        puntoFin = new Vector3(transform.position.x, Mathf.Max(height, transform.position.y), transform.position.z);
        movement.Begin(puntoFin, Mathf.Max(.1f, velocidad));
    }

    void PrepareLethalBody()
    {
        if (!lethalBody) lethalBody = GetComponent<CapsuleCollider>();
        if (!lethalBody)
        {
            lethalBody = gameObject.AddComponent<CapsuleCollider>();
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                Bounds local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (found)
            {
                int axis = bounds.size.x > bounds.size.y ? 0 : 1;
                if (bounds.size.z > bounds.size[axis]) axis = 2;
                lethalBody.direction = axis;
                lethalBody.center = bounds.center;
                lethalBody.radius = Mathf.Max(.01f, Mathf.Min(bounds.extents[(axis + 1) % 3], bounds.extents[(axis + 2) % 3]) * .9f);
                lethalBody.height = Mathf.Max(lethalBody.radius * 2, bounds.size[axis]);
            }
        }
        lethalBody.isTrigger = true;
        lethalBody.enabled = false;
    }

    void OnTriggerEnter(Collider other) => TryKillPlayer(other);
    void OnTriggerStay(Collider other) => TryKillPlayer(other);

    void TryKillPlayer(Collider other)
    {
        if (!started || !isActiveAndEnabled || !lethalBody.enabled) return;
        var manager = Anadromo.Logic.LevelManager.Instance;
        if (manager && manager.gameObject.scene == gameObject.scene)
            manager.KillPlayerFromBloop(other);
    }
}
