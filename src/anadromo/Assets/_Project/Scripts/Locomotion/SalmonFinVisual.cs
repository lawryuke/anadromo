using UnityEngine;
using System.Collections;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Maneja la representación visual y animación de las aletas pectorales del salmón 
    /// visibles en la vista VR del jugador.
    /// </summary>
    public class SalmonFinVisual : MonoBehaviour
    {
        [Header("Dependencias")]
        [Tooltip("Referencia al detector de aleteo.")]
        [SerializeField] private FlapDetector flapDetector;

        [Header("Animación de Aleteo")]
        [Tooltip("Ángulo máximo de rotación (power stroke) en grados.")]
        [SerializeField] private float flapRotationAngle = 40f;

        [Tooltip("Velocidad de la animación de aleteo.")]
        [SerializeField] private float flapAnimSpeed = 8f;

        [Tooltip("Curva de animación para el aleteo (EaseInOut recomendado).")]
        [SerializeField] private AnimationCurve flapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Ondulación Idle")]
        [Tooltip("Amplitud de la oscilación idle en grados.")]
        [SerializeField] private float idleAmplitude = 4f;

        [Tooltip("Frecuencia de la oscilación idle en Hz.")]
        [SerializeField] private float idleFrequency = 1.5f;

        [Header("Apariencia")]
        [Tooltip("Escala de las aletas.")]
        [SerializeField] private Vector3 finScale = new Vector3(1f, 1f, 1f);

        [Tooltip("Color de las aletas (silvery-blue recomendado).")]
        [SerializeField] private Color finColor = new Color(0.6f, 0.8f, 0.9f);

        [Tooltip("Opacidad de las aletas (0 = transparente, 1 = opaco).")]
        [Range(0f, 1f)]
        [SerializeField] private float finOpacity = 0.25f;

        // Referencias internas
        private GameObject leftFin;
        private GameObject rightFin;
        
        private MeshRenderer leftRenderer;
        private MeshRenderer rightRenderer;

        private Coroutine leftFlapCoroutine;
        private Coroutine rightFlapCoroutine;

        // Rotaciones base
        private Quaternion leftBaseRot;
        private Quaternion rightBaseRot;

        private void Start()
        {
            CreateFins();
        }

        private void OnEnable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.AddListener(OnLeftFlap);
                flapDetector.OnRightFlap.AddListener(OnRightFlap);
            }
        }

        private void OnDisable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.RemoveListener(OnLeftFlap);
                flapDetector.OnRightFlap.RemoveListener(OnRightFlap);
            }
        }

        private void Update()
        {
            // Aplicar idle ondulation si no están en plena animación de flap
            if (leftFin != null && leftFlapCoroutine == null)
            {
                float leftIdleAngle = Mathf.Sin(Time.time * idleFrequency * Mathf.PI * 2f) * idleAmplitude;
                leftFin.transform.localRotation = leftBaseRot * Quaternion.Euler(leftIdleAngle, 0f, 0f);
            }

            if (rightFin != null && rightFlapCoroutine == null)
            {
                // Offset de fase para la aleta derecha
                float rightIdleAngle = Mathf.Sin((Time.time * idleFrequency * Mathf.PI * 2f) + Mathf.PI) * idleAmplitude;
                rightFin.transform.localRotation = rightBaseRot * Quaternion.Euler(rightIdleAngle, 0f, 0f);
            }
        }

        private void CreateFins()
        {
            // Crear aleta izquierda
            leftFin = new GameObject("LeftFin");
            leftFin.transform.SetParent(this.transform, false);
            leftFin.layer = LayerMask.NameToLayer("Default");
            
            // Crear aleta derecha
            rightFin = new GameObject("RightFin");
            rightFin.transform.SetParent(this.transform, false);
            rightFin.layer = LayerMask.NameToLayer("Default");

            // Configurar Mesh y Material desde ProceduralFinMesh
            Mesh finMesh = null;
            Material finMaterial = null;
            
            try 
            {
                finMesh = ProceduralFinMesh.CreateFinMesh();
                finMaterial = ProceduralFinMesh.CreateFinMaterial();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Anadromo] No se pudo generar la malla/material procedural: {e.Message}");
            }

            // Aplicar color y opacidad
            if (finMaterial != null)
            {
                Color finalColor = finColor;
                finalColor.a = finOpacity;
                
                if (finMaterial.HasProperty("_BaseColor"))
                {
                    finMaterial.SetColor("_BaseColor", finalColor);
                }
                else if (finMaterial.HasProperty("_Color"))
                {
                    finMaterial.SetColor("_Color", finalColor);
                }
            }

            // Componentes Left
            var leftFilter = leftFin.AddComponent<MeshFilter>();
            leftFilter.mesh = finMesh;
            leftRenderer = leftFin.AddComponent<MeshRenderer>();
            leftRenderer.sharedMaterial = finMaterial;

            leftFin.transform.localPosition = new Vector3(-0.15f, -0.1f, 0.1f);
            // Rotar hacia afuera
            leftBaseRot = Quaternion.Euler(0f, -15f, 45f);
            leftFin.transform.localRotation = leftBaseRot;
            leftFin.transform.localScale = finScale;

            // Componentes Right
            var rightFilter = rightFin.AddComponent<MeshFilter>();
            rightFilter.mesh = finMesh;
            rightRenderer = rightFin.AddComponent<MeshRenderer>();
            rightRenderer.sharedMaterial = finMaterial;

            rightFin.transform.localPosition = new Vector3(0.15f, -0.1f, 0.1f);
            // Rotar hacia afuera (espejo)
            rightBaseRot = Quaternion.Euler(0f, 15f, -45f);
            rightFin.transform.localRotation = rightBaseRot;
            
            // Invertir en X para simetría
            Vector3 rightScale = finScale;
            rightScale.x = -rightScale.x;
            rightFin.transform.localScale = rightScale;
        }

        private void OnLeftFlap(float intensity)
        {
            if (leftFlapCoroutine != null) StopCoroutine(leftFlapCoroutine);
            leftFlapCoroutine = StartCoroutine(FlapAnimation(leftFin.transform, leftBaseRot, intensity, true));
        }

        private void OnRightFlap(float intensity)
        {
            if (rightFlapCoroutine != null) StopCoroutine(rightFlapCoroutine);
            rightFlapCoroutine = StartCoroutine(FlapAnimation(rightFin.transform, rightBaseRot, intensity, false));
        }

        private IEnumerator FlapAnimation(Transform finTransform, Quaternion baseRot, float intensity, bool isLeft)
        {
            float timer = 0f;
            float totalDuration = 1f / flapAnimSpeed;
            float targetAngle = flapRotationAngle * intensity;
            
            // El power stroke es hacia abajo. Asumimos rotar sobre el eje X local
            Quaternion targetRot = baseRot * Quaternion.Euler(targetAngle, 0f, 0f);

            // Fase 1: Bajar rápido (power stroke) ~0.1s
            float phase1Duration = totalDuration * 0.25f;
            while (timer < phase1Duration)
            {
                timer += Time.deltaTime;
                float t = timer / phase1Duration;
                float curveVal = flapCurve.Evaluate(t);
                finTransform.localRotation = Quaternion.Lerp(baseRot, targetRot, curveVal);
                yield return null;
            }

            // Fase 2: Regresar lento ~0.3s
            timer = 0f;
            float phase2Duration = totalDuration * 0.75f;
            while (timer < phase2Duration)
            {
                timer += Time.deltaTime;
                float t = timer / phase2Duration;
                float curveVal = flapCurve.Evaluate(1f - t); // Evaluate backwards
                finTransform.localRotation = Quaternion.Lerp(baseRot, targetRot, curveVal);
                yield return null;
            }

            finTransform.localRotation = baseRot;
            
            if (isLeft)
                leftFlapCoroutine = null;
            else
                rightFlapCoroutine = null;
        }

        /// <summary>
        /// Activa o desactiva la visibilidad de las aletas.
        /// </summary>
        public void SetFinsVisible(bool visible)
        {
            if (leftRenderer != null) leftRenderer.enabled = visible;
            if (rightRenderer != null) rightRenderer.enabled = visible;
        }
    }
}
