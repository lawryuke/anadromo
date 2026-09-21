using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Proporciona feedback auditivo y visual cuando se detecta un aleteo válido.
    /// Reproduce un sonido configurable y/o un flash breve en la pantalla.
    /// 
    /// Fase 5 — F5.4: Feedback de "tu aleteo fue registrado".
    /// 
    /// Se suscribe a los mismos eventos del FlapDetector que usa FlapSwimController.
    /// </summary>
    public class FlapFeedback : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField] private FlapDetector flapDetector;

        [Header("Audio")]
        [Tooltip("Sonido corto de 'splash' al detectar un aleteo. Dejar vacío = sin audio.")]
        [SerializeField] private AudioClip flapSound;

        [Tooltip("Volumen del sonido de aleteo.")]
        [Range(0f, 1f)]
        [SerializeField] private float flapSoundVolume = 0.3f;

        [Tooltip("Variación aleatoria del pitch para que no suene repetitivo.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float pitchVariation = 0.15f;

        [Tooltip("Sonido diferente para aleteo simultáneo (avance). Dejar vacío = usa flapSound.")]
        [SerializeField] private AudioClip simultaneousFlapSound;

        [Header("Visual Flash (Pantalla)")]
        [Tooltip("Activar flash visual sutil al aletear.")]
        [SerializeField] private bool enableFlash = false;

        [Tooltip("Color del flash. Recomendado: azul/cyan sutil con alpha bajo.")]
        [SerializeField] private Color flashColor = new Color(0.2f, 0.6f, 1f, 0.15f);

        [Tooltip("Duración del flash en segundos.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float flashDuration = 0.12f;

        private AudioSource audioSource;
        private float flashTimer;
        private float flashAlpha;

        // Detección de aleteo simultáneo
        private float lastFlapTime = -10f;
        private bool wasSimultaneous;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D (no posicional, suena igual en ambos oídos)
            audioSource.loop = false;
        }

        private void OnEnable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.AddListener(OnFlap);
                flapDetector.OnRightFlap.AddListener(OnFlap);
            }
        }

        private void OnDisable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.RemoveListener(OnFlap);
                flapDetector.OnRightFlap.RemoveListener(OnFlap);
            }
        }

        private void OnFlap(float intensity)
        {
            // Detectar si es simultáneo (dos flaps muy seguidos)
            float timeSinceLast = Time.time - lastFlapTime;
            wasSimultaneous = timeSinceLast < 0.25f;
            lastFlapTime = Time.time;

            PlayFlapSound(intensity);

            if (enableFlash)
            {
                flashTimer = flashDuration;
                flashAlpha = flashColor.a * intensity;
            }
        }

        private void PlayFlapSound(float intensity)
        {
            AudioClip clip = (wasSimultaneous && simultaneousFlapSound != null)
                ? simultaneousFlapSound
                : flapSound;

            if (clip == null) return;

            audioSource.clip = clip;
            audioSource.volume = flapSoundVolume * intensity;
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.Play();
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (!enableFlash || flashTimer <= 0f) return;

            float alpha = flashAlpha * (flashTimer / flashDuration);
            Color c = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
