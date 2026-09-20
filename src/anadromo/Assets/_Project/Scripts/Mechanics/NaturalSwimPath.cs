using UnityEngine;

/// <summary>Finite swim pass: smooth acceleration, a small curved course, exact arrival, and natural banking roll.</summary>
[DisallowMultipleComponent]
public sealed class NaturalSwimPath : MonoBehaviour
{
    public float acceleration = 1.8f;
    public float courseWidth = .22f;
    public float turnSpeed = 45f;
    public float maxBankAngle = 18f;
    public bool orientToCourse = true;
    public float Speed { get; private set; }
    public float Effort { get; private set; }
    public bool IsSwimming { get; private set; }
    Vector3 start, destination, side, direction, previousPosition;
    Quaternion restingRotation;
    float distance, travelled, cruiseSpeed, phase, currentBank;

    void Awake()
    {
        restingRotation = transform.rotation;
        phase = Random.value * Mathf.PI * 2;
    }

    public void Begin(Vector3 target, float speed)
    {
        restingRotation = transform.rotation;
        start = previousPosition = transform.position;
        destination = target;
        distance = Vector3.Distance(start, target);
        cruiseSpeed = Mathf.Max(.01f, speed);
        travelled = Speed = currentBank = 0;
        direction = distance > .001f ? (target - start) / distance : Vector3.forward;
        side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < .01f) side = Vector3.Cross(direction, Vector3.right);
        if (side.sqrMagnitude < .01f) side = Vector3.Cross(direction, Vector3.forward);
        side.Normalize();
        IsSwimming = distance > .001f;
    }

    void Update() => Tick(Time.deltaTime);

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0) return;
        if (!IsSwimming)
        {
            Effort = Mathf.MoveTowards(Effort, 0, deltaTime * 2f);
            currentBank = Mathf.MoveTowards(currentBank, 0, deltaTime * 20f);
            return;
        }
        float remaining = distance - travelled;
        float a = Mathf.Max(.1f, acceleration);
        float desiredSpeed = Mathf.Min(cruiseSpeed, Mathf.Sqrt(2 * a * remaining));
        Speed = Mathf.MoveTowards(Speed, desiredSpeed, a * deltaTime);
        travelled = Mathf.Min(distance, travelled + Speed * deltaTime);
        float t = travelled / distance;
        // Zero displacement and tangent at each end; no sideways snap on arrival.
        float envelope = Mathf.Sin(t * Mathf.PI);
        float lateral = Mathf.Sin(t * Mathf.PI * 2 + phase) * envelope * envelope * courseWidth;
        Vector3 position = Vector3.Lerp(start, destination, t) + side * lateral;
        Vector3 tangent = position - previousPosition;
        if (orientToCourse && tangent.sqrMagnitude > .000001f)
        {
            Vector3 tangentDir = tangent.normalized;
            Quaternion targetHeading = Quaternion.FromToRotation(restingRotation * Vector3.forward, tangentDir) * restingRotation;
            
            // Calculate lateral curvature to derive natural banking roll
            Vector3 localTangent = Quaternion.Inverse(transform.rotation) * tangentDir;
            float yawCurvature = localTangent.x;
            float targetBank = Mathf.Clamp(-yawCurvature * maxBankAngle * 4f, -maxBankAngle, maxBankAngle);
            currentBank = Mathf.Lerp(currentBank, targetBank, deltaTime * 6f);
            
            Quaternion bankRot = Quaternion.AngleAxis(currentBank, tangentDir);
            Quaternion finalTarget = bankRot * targetHeading;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, finalTarget, turnSpeed * deltaTime);
        }
        transform.position = previousPosition = position;
        Effort = Mathf.MoveTowards(Effort, Speed / cruiseSpeed, deltaTime * 2);
        if (remaining < .005f || travelled >= distance)
        {
            transform.position = destination;
            IsSwimming = false;
            Speed = 0;
        }
    }

    void OnDisable() { IsSwimming = false; Speed = Effort = currentBank = 0; }
}
