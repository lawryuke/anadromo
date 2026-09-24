using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.Mechanics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class MedusaGlow : MonoBehaviour
    {
        public Shader glowShader;
        [Tooltip("Material que controla Start/End de la visibilidad submarina.")]
        public Material waterSettings;
        [Min(0.01f)] public float bodyDiameter = 0.24f;
        [Min(0.01f)] public float haloDiameter = 1.2f;
        [Min(0)] public float brightness = 3f;
        [Range(0, 1)] public float haloOpacity = 0.22f;
        [Range(0, 0.3f)] public float pulseAmount = 0.08f;
        [Min(0)] public float pulseSpeed = 1.5f;
        [Tooltip("Solo diagnóstico: permite ver el halo más allá de la niebla, pero no a través de paredes.")]
        public bool debugIgnoreDistanceFade;

        GameObject body, halo;
        Material bodyMaterial, haloMaterial;
        Light sourceLight;

        void OnEnable()
        {
            if (!glowShader || !glowShader.isSupported)
            {
                Debug.LogError($"[{name}] Medusa Glow: shader ausente o no compatible.", this);
                return;
            }
            sourceLight = GetComponent<Light>();
            bodyMaterial = new Material(glowShader) { name = "Medusa body (runtime)" };
            haloMaterial = new Material(glowShader) { name = "Medusa halo (runtime)" };
            haloMaterial.SetFloat("_Halo", 1);
            haloMaterial.SetFloat("_Cull", 0);
            body = CreateVisual("Cuerpo luminoso", PrimitiveType.Sphere, bodyMaterial);
            halo = CreateVisual("Halo suave", PrimitiveType.Quad, haloMaterial);
            LateUpdate();
        }

        GameObject CreateVisual(string label, PrimitiveType shape, Material material)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = label;
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            var collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // The halo rotates in the vertex shader: its CPU culling bounds must have depth too.
            if (shape == PrimitiveType.Quad) renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 1.5f);
            return visual;
        }

        void LateUpdate()
        {
            if (!body || !halo) return;
            float pulse = 1 + pulseAmount * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2);
            body.transform.localScale = Vector3.one * bodyDiameter * pulse;
            halo.transform.localScale = Vector3.one * haloDiameter * pulse;
            Color color = sourceLight.color * brightness;
            color.a = 1;
            bodyMaterial.SetColor("_Color", color);
            color.a = haloOpacity;
            haloMaterial.SetColor("_Color", color);
            float start = waterSettings ? waterSettings.GetFloat("_VisibilityStart") : 5;
            float end = waterSettings ? waterSettings.GetFloat("_VisibilityEnd") : 10;
            if (debugIgnoreDistanceFade) { start = 9999; end = 10000; }
            bodyMaterial.SetVector("_Visibility", new Vector4(start, end, 0, 0));
            haloMaterial.SetVector("_Visibility", new Vector4(start, end, 0, 0));
        }

        public string VisibilityDebug(Camera camera)
        {
            float start = waterSettings ? waterSettings.GetFloat("_VisibilityStart") : 5;
            float end = waterSettings ? waterSettings.GetFloat("_VisibilityEnd") : 10;
            float distance = Vector3.Distance(camera.transform.position, transform.position);
            float fade = debugIgnoreDistanceFade ? 1 : 1 - Mathf.Clamp01((distance - start) / Mathf.Max(0.01f, end - start));
            bool layerVisible = (camera.cullingMask & (1 << gameObject.layer)) != 0;
            string status = $"Cuerpo/halo: {(body && halo ? "creados" : "NO creados")} | Shader: {(glowShader && glowShader.isSupported ? "compatible" : "ERROR")}\n" +
                $"Visibilidad: {fade:P0} | Start/End: {start:F1}/{end:F1} m | capa: {(layerVisible ? "visible" : "EXCLUIDA")} | ignorar distancia: {debugIgnoreDistanceFade}\n" +
                $"Luz real: {(sourceLight && sourceLight.isActiveAndEnabled ? "activa" : "apagada")} | intensidad {(sourceLight ? sourceLight.intensity : 0):F1} | alcance {(sourceLight ? sourceLight.range : 0):F1} m";
            if (Physics.Linecast(camera.transform.position, transform.position, out RaycastHit hit, camera.cullingMask, QueryTriggerInteraction.Ignore))
                status += $"\nCollider en línea de visión: {hit.collider.name} (comprobar si tapa la medusa)";
            return status;
        }

        void OnDisable()
        {
            if (body) body.SetActive(false);
            if (halo) halo.SetActive(false);
            Destroy(body);
            Destroy(halo);
            Destroy(bodyMaterial);
            Destroy(haloMaterial);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = GetComponent<Light>().color;
            Gizmos.DrawWireSphere(transform.position, bodyDiameter * 0.5f);
            Gizmos.DrawWireSphere(transform.position, haloDiameter * 0.5f);
        }
    }
}
