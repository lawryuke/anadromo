using System.Collections;
using UnityEngine;
using Anadromo.Mechanics;

public class AudioFadeTrigger : MonoBehaviour
{
    [Header("Configuración de Audio")]
    [Tooltip("El AudioSource que se va a APAGAR (tu intro.ogg)")]
    public AudioSource audioSourceOut;
    
    [Tooltip("El AudioSource que se va a ENCENDER (tu abysm.ogg)")]
    public AudioSource audioSourceIn;

    [Tooltip("Volumen máximo que alcanzará la nueva música (0 a 1)")]
    [Range(0f, 1f)]
    public float maxVolumeIn = 1.0f;

    [Tooltip("Tiempo en segundos que tardará la transición")]
    public float fadeDuration = 3.0f;

    [Header("Configuración de Trigger")]
    [Tooltip("Tag del objeto que activará este trigger (usualmente 'Player' o 'MainCamera')")]
    public string triggerTag = "Player";

    private bool isFading = false;
    private ZoneLimit zoneLimit;
    private Transform targetPlayer;

    private void Start()
    {
        // Obtenemos tu componente ZoneLimit personalizado
        zoneLimit = GetComponent<ZoneLimit>();
        
        if (zoneLimit == null)
        {
            Debug.LogError("AudioFadeTrigger: No se encontró un ZoneLimit en este objeto.");
        }

        // Buscamos al jugador en la escena usando el Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(triggerTag);
        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("AudioFadeTrigger: No se encontró ningún objeto con el tag '" + triggerTag + "'. Verifica el tag de tu personaje.");
        }
    }

    private void Update()
    {
        // Si ya estamos haciendo la transición, o si falta el jugador o el zoneLimit, no hacemos nada
        if (isFading || targetPlayer == null || zoneLimit == null) return;

        // Comprobamos si la posición del jugador está dentro de la zona matemática
        if (zoneLimit.Contains(targetPlayer.position))
        {
            isFading = true;
            StartCoroutine(Crossfade());
        }
    }

    private IEnumerator Crossfade()
    {
        float startVolumeOut = 0f;
        
        // Preparar el audio que se va a apagar
        if (audioSourceOut != null && audioSourceOut.isPlaying)
        {
            startVolumeOut = audioSourceOut.volume;
        }

        // Preparar el audio que se va a encender
        if (audioSourceIn != null)
        {
            audioSourceIn.volume = 0f;
            if (!audioSourceIn.isPlaying)
            {
                audioSourceIn.Play();
            }
        }

        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;

            // Bajar volumen del primero
            if (audioSourceOut != null)
            {
                audioSourceOut.volume = Mathf.Lerp(startVolumeOut, 0f, progress);
            }

            // Subir volumen del segundo
            if (audioSourceIn != null)
            {
                audioSourceIn.volume = Mathf.Lerp(0f, maxVolumeIn, progress);
            }

            yield return null;
        }

        // Asegurar que queden en sus valores finales exactos
        if (audioSourceOut != null)
        {
            audioSourceOut.volume = 0f;
            audioSourceOut.Stop();
        }

        if (audioSourceIn != null)
        {
            audioSourceIn.volume = maxVolumeIn;
        }
    }
}
