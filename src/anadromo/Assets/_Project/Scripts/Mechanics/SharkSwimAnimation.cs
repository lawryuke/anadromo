using System.Collections.Generic;
using UnityEngine;

/// <summary>GPU body/caudal motion for the unrigged, vertex-coloured WhiteShark model.</summary>
[DisallowMultipleComponent]
public sealed class SharkSwimAnimation : MonoBehaviour
{
    static readonly int PhaseId = Shader.PropertyToID("_SwimPhase");
    static readonly int StrengthId = Shader.PropertyToID("_SwimStrength");
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Material[]> originals = new List<Material[]>();
    readonly List<Material> generated = new List<Material>();
    readonly List<Bounds> bounds = new List<Bounds>();
    MaterialPropertyBlock block;
    NaturalSwimPath movement;
    float phase, cadence;

    public void Configure(Shader shader)
    {
        if (!shader || renderers.Count > 0) return;
        movement = GetComponent<NaturalSwimPath>();
        block = new MaterialPropertyBlock();
        phase = Random.value * Mathf.PI * 2;
        cadence = Random.Range(.9f, 1.1f);
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            renderers.Add(renderer);
            bounds.Add(renderer.localBounds);
            var materials = renderer.sharedMaterials;
            originals.Add(materials);
            var replacements = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var material = new Material(shader) { name = "WhiteShark natural swim", hideFlags = HideFlags.DontSave };
                replacements[i] = material;
                generated.Add(material);
            }
            renderer.sharedMaterials = replacements;
            Bounds expanded = renderer.localBounds;
            expanded.Expand(new Vector3(2.2f, 1.0f, 0.6f));
            renderer.localBounds = expanded;
        }
    }

    void LateUpdate() => Tick(Time.deltaTime);

    public void Tick(float deltaTime)
    {
        float effort = movement ? movement.Effort : 0;
        // Fins use half the tail frequency, so wrap only after both cycles close.
        phase = Mathf.Repeat(phase + Mathf.Max(0, deltaTime) * Mathf.Lerp(.32f, 1.15f, effort) * cadence * Mathf.PI * 2, Mathf.PI * 4);
        foreach (var renderer in renderers)
        {
            if (!renderer) continue;
            renderer.GetPropertyBlock(block);
            block.SetFloat(PhaseId, phase);
            block.SetFloat(StrengthId, Mathf.Lerp(.3f, 1f, effort));
            renderer.SetPropertyBlock(block);
        }
    }

    void OnDisable()
    {
        foreach (var renderer in renderers)
        {
            if (!renderer) continue;
            renderer.GetPropertyBlock(block);
            block.SetFloat(StrengthId, 0);
            renderer.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i]) { renderers[i].sharedMaterials = originals[i]; renderers[i].localBounds = bounds[i]; }
        foreach (var material in generated)
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
    }
}
