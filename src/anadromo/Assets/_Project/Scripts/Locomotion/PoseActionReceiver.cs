using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Recibe acciones del pose_swim_server.py por UDP (ej: {"action": "forward", "speed": 1.0})
    /// y lanza eventos de aleteo para que los consuma FlapSwimController u otros.
    /// </summary>
    public class PoseActionReceiver : MonoBehaviour
    {
        [Header("Configuracin de Red")]
        public int udpPort = 5065;
        public bool autoStart = true;

        [Header("Eventos de salida")]
        public UnityEngine.Events.UnityEvent<float> OnLeftFlap;
        public UnityEngine.Events.UnityEvent<float> OnRightFlap;

        [Header("Estado (Solo lectura)")]
        public bool isReceiving;
        public string currentAction = "idle";
        public float currentSpeed = 0f;

        // Privado
        private UdpClient udpClient;
        private Thread receiveThread;
        private bool threadRunning = false;
        private readonly object dataLock = new object();
        
        private string latestJson = null;
        private bool hasNewData = false;

        private float lastPacketTime;
        
        // Estado anterior para detectar flancos de subida
        private string previousAction = "idle";
        
        private FlapDetector flapDetector;

        [Serializable]
        private class ActionData
        {
            public string action;
            public float speed;
        }

        private void Start()
        {
            flapDetector = FindFirstObjectByType<FlapDetector>();
            if (autoStart) StartReceiving();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindFirstObjectByType<PoseActionReceiver>() != null) return;
            // Auto-instanciar al iniciar el juego
            var go = new GameObject("PoseActionReceiver_Auto");
            var instance = go.AddComponent<PoseActionReceiver>();
            DontDestroyOnLoad(go);
            Debug.Log("[Anadromo] PoseActionReceiver auto-instanciado.");
        }

        private void OnDestroy()
        {
            StopReceiving();
        }

        private void OnApplicationQuit()
        {
            StopReceiving();
        }

        public void StartReceiving()
        {
            if (threadRunning) return;

            try
            {
                udpClient = new UdpClient(udpPort);
                udpClient.Client.ReceiveTimeout = 1000;
                threadRunning = true;

                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "PoseActionReceiverThread"
                };
                receiveThread.Start();

                Debug.Log($"[Anadromo] PoseActionReceiver escuchando en UDP:{udpPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Anadromo] Error al iniciar UDP en puerto {udpPort}: {e.Message}");
            }
        }

        public void StopReceiving()
        {
            threadRunning = false;
            try { udpClient?.Close(); } catch { }
            if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(2000);
            receiveThread = null;
            udpClient = null;
            isReceiving = false;
        }

        private void ReceiveLoop()
        {
            while (threadRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);

                    lock (dataLock)
                    {
                        latestJson = message;
                        hasNewData = true;
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.TimedOut && threadRunning)
                        Debug.LogWarning($"[Anadromo] Socket error: {ex.SocketErrorCode}");
                }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void Update()
        {
            string json = null;
            lock (dataLock)
            {
                if (hasNewData)
                {
                    json = latestJson;
                    hasNewData = false;
                }
            }

            if (json != null)
            {
                ParsePacket(json);
                lastPacketTime = Time.time;
            }

            isReceiving = (Time.time - lastPacketTime) < 1.0f;
            
            // Si dejamos de recibir, reseteamos a idle
            if (!isReceiving && currentAction != "idle")
            {
                currentAction = "idle";
                currentSpeed = 0f;
            }

            // Deteccin de flanco para disparar el aleteo
            if (currentAction == "forward" && previousAction != "forward")
            {
                // Disparamos ambos brazos con la intensidad dictada por la velocidad
                float intensity = currentSpeed > 0 ? currentSpeed : 1f;
                OnLeftFlap?.Invoke(intensity);
                OnRightFlap?.Invoke(intensity);
                
                if (flapDetector != null)
                {
                    flapDetector.OnLeftFlap?.Invoke(intensity);
                    flapDetector.OnRightFlap?.Invoke(intensity);
                }
            }
            
            previousAction = currentAction;
        }

        private void ParsePacket(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<ActionData>(json);
                if (data != null && !string.IsNullOrEmpty(data.action))
                {
                    currentAction = data.action;
                    currentSpeed = data.speed;
                }
            }
            catch
            {
                // Ignorar paquetes corruptos
            }
        }
    }
}
