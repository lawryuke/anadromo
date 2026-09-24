using UnityEngine;
using Anadromo.Mechanics;

public class AudioPlayTrigger : MonoBehaviour
{
    [Tooltip("El AudioSource que se va a reproducir (tu orca.wav)")]
    public AudioSource audioSource;
    
    [Tooltip("Tag del objeto que activará este trigger")]
    public string triggerTag = "Player";

    [Tooltip("Marcar si quieres que el sonido solo suene la primera vez que entras")]
    public bool playOnlyOnce = true;

    private bool hasPlayed = false;
    private ZoneLimit zoneLimit;
    private Transform targetPlayer;

    private void Start()
    {
        // Obtenemos tu componente ZoneLimit
        zoneLimit = GetComponent<ZoneLimit>();
        
        if (zoneLimit == null)
        {
            Debug.LogError("AudioPlayTrigger: No se encontró un ZoneLimit en este objeto.");
        }

        // Buscamos al jugador por su Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(triggerTag);
        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
        }
    }

    private void Update()
    {
        // Si ya sonó y solo queremos que suene una vez, o si falta algo, salimos
        if (playOnlyOnce && hasPlayed) return;
        if (targetPlayer == null || zoneLimit == null) return;

        // Comprobamos si el jugador cruzó la zona
        if (zoneLimit.Contains(targetPlayer.position))
        {
            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Play();
                hasPlayed = true;
            }
        }
    }
}
