using UnityEngine;

/// <summary>Individual stroke/glide cadence for the +Z-facing OrcaSwimMesh.</summary>
[DisallowMultipleComponent]
public sealed class OrcaSwimAnimation : MonoBehaviour
{
    static readonly int PhaseId = Shader.PropertyToID("_SwimPhase");
    static readonly int StrengthId = Shader.PropertyToID("_SwimStrength");
    Renderer[] surfaces;
    MaterialPropertyBlock block;
    NaturalSwimPath movement;
    float phase, cadence, glidePhase;

    void Awake() => Initialize();

    public void Initialize()
    {
        if (surfaces != null) return;
        surfaces = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        phase = Random.value * Mathf.PI * 4;
        cadence = Random.Range(.92f, 1.08f);
        glidePhase = Random.value * Mathf.PI * 2;
    }

    void LateUpdate() => Tick(Time.deltaTime);

    public void Tick(float deltaTime)
    {
        Initialize();
        if (!movement) movement = GetComponent<NaturalSwimPath>();
        float effort = movement ? movement.Effort : 0;
        float dt = Mathf.Max(0, deltaTime);
        phase = Mathf.Repeat(phase + dt * Mathf.Lerp(.18f, .72f, effort) * cadence * Mathf.PI * 2, Mathf.PI * 4);
        glidePhase = Mathf.Repeat(glidePhase + dt * .65f, Mathf.PI * 2);
        float glide = Mathf.SmoothStep(.52f, 1, .5f + .5f * Mathf.Sin(glidePhase));
        Apply(Mathf.Lerp(.12f, 1f, effort) * glide);
    }

    void Apply(float strength)
    {
        foreach (var surface in surfaces)
        {
            if (!surface) continue;
            surface.GetPropertyBlock(block);
            block.SetFloat(PhaseId, phase);
            block.SetFloat(StrengthId, strength);
            surface.SetPropertyBlock(block);
        }
    }

    void OnDisable() { if (surfaces != null) Apply(0); }
}
