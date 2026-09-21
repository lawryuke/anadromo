using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Anadromo.Systems
{
    /// <summary>
    /// Recibe datos de pose corporal del bridge Python (MediaPipe) vía UDP.
    /// Parsea los landmarks del tren superior y los expone como propiedades Vector3.
    /// 
    /// Protocolo:
    ///   - Python envía un paquete UDP por frame con JSON:
    ///     {"t":timestamp, "lw":[x,y,z], "rw":[x,y,z], "le":[x,y,z], "re":[x,y,z], "ls":[x,y,z], "rs":[x,y,z]}
    ///   - Coordenadas normalizadas: X[0,1] izq-der, Y[0,1] abajo-arriba (invertido desde MediaPipe), Z profundidad
    /// 
    /// Uso:
    ///   1. Agregar este componente a un GameObject en la escena
    ///   2. Ejecutar el script Python: python mediapipe_bridge.py --port 5555
    ///   3. Al dar Play en Unity, este componente empieza a escuchar automáticamente
    ///   4. FlapDetector lee las propiedades LeftWrist, RightWrist, etc.
    /// </summary>
    public class ExternalCameraReceiver : MonoBehaviour
    {
        [Header("Conexión UDP")]
        [Tooltip("Puerto UDP donde escuchar. Debe coincidir con el --port del script Python.")]
        [SerializeField] private int udpPort = 5555;

        [Tooltip("Iniciar la escucha automáticamente al arrancar.")]
        [SerializeField] private bool autoStart = true;

        [Header("Estado (Solo Lectura)")]
        [SerializeField] private bool isReceiving;
        [SerializeField] private float lastPacketTime;
        [SerializeField] private int packetsPerSecond;
        [SerializeField] private float timeSinceLastPacket;

        // ─── Landmarks públicos (leídos por FlapDetector) ───
        /// <summary>Posición normalizada de la muñeca izquierda. Y invertido: 1=arriba, 0=abajo.</summary>
        public Vector3 LeftWrist { get; private set; }
        /// <summary>Posición normalizada de la muñeca derecha.</summary>
        public Vector3 RightWrist { get; private set; }
        /// <summary>Posición normalizada del codo izquierdo.</summary>
        public Vector3 LeftElbow { get; private set; }
        /// <summary>Posición normalizada del codo derecho.</summary>
        public Vector3 RightElbow { get; private set; }
        /// <summary>Posición normalizada del hombro izquierdo.</summary>
        public Vector3 LeftShoulder { get; private set; }
        /// <summary>Posición normalizada del hombro derecho.</summary>
        public Vector3 RightShoulder { get; private set; }

        /// <summary>True si se han recibido datos en el último segundo.</summary>
        public bool IsReceiving => isReceiving;

        // ─── UDP internals ───
        private UdpClient udpClient;
        private Thread receiveThread;
        private volatile bool threadRunning;

        // Buffer thread-safe
        private readonly object dataLock = new object();
        private string latestPacket;
        private bool hasNewData;

        // Estadísticas
        private int packetCount;
        private float statsTimer;

        // ─── Lifecycle ───

        private void Start()
        {
            if (autoStart) StartReceiving();
        }

        private void OnDestroy()
        {
            StopReceiving();
        }

        private void OnApplicationQuit()
        {
            StopReceiving();
        }

        // ─── API pública ───

        /// <summary>Inicia la escucha UDP en el puerto configurado.</summary>
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
                    Name = "Anadromo_PoseReceiver"
                };
                receiveThread.Start();

                Debug.Log($"[Anadromo] 📷 ExternalCameraReceiver escuchando en UDP:{udpPort}");
                Debug.Log("[Anadromo] Ejecuta el bridge Python: python mediapipe_bridge.py --port " + udpPort);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Anadromo] Error al iniciar UDP en puerto {udpPort}: {e.Message}");
            }
        }

        /// <summary>Detiene la escucha UDP y limpia recursos.</summary>
        public void StopReceiving()
        {
            threadRunning = false;

            try
            {
                udpClient?.Close();
            }
            catch (Exception) { /* Ignorar errores al cerrar */ }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(2000);
            }

            receiveThread = null;
            udpClient = null;
            isReceiving = false;
        }

        // ─── Thread de recepción ───

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
                        latestPacket = message;
                        hasNewData = true;
                    }
                }
                catch (SocketException ex)
                {
                    // Timeout es normal (permite revisar threadRunning periódicamente)
                    if (ex.SocketErrorCode != SocketError.TimedOut && threadRunning)
                    {
                        Debug.LogWarning($"[Anadromo] Socket error: {ex.SocketErrorCode}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        // ─── Update (main thread) ───

        private void Update()
        {
            // Leer datos del thread de recepción
            string packet = null;
            lock (dataLock)
            {
                if (hasNewData)
                {
                    packet = latestPacket;
                    hasNewData = false;
                }
            }

            if (packet != null)
            {
                ParsePacket(packet);
                lastPacketTime = Time.time;
                packetCount++;
            }

            // Actualizar estado de conexión
            timeSinceLastPacket = Time.time - lastPacketTime;
            isReceiving = timeSinceLastPacket < 1.0f;

            // Estadísticas: paquetes por segundo
            statsTimer += Time.deltaTime;
            if (statsTimer >= 1.0f)
            {
                packetsPerSecond = packetCount;
                packetCount = 0;
                statsTimer = 0f;
            }
        }

        // ─── Parsing ───

        private void ParsePacket(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<PoseData>(json);

                if (data.lw != null && data.lw.Length >= 3)
                    LeftWrist = new Vector3(data.lw[0], data.lw[1], data.lw[2]);
                if (data.rw != null && data.rw.Length >= 3)
                    RightWrist = new Vector3(data.rw[0], data.rw[1], data.rw[2]);
                if (data.le != null && data.le.Length >= 3)
                    LeftElbow = new Vector3(data.le[0], data.le[1], data.le[2]);
                if (data.re != null && data.re.Length >= 3)
                    RightElbow = new Vector3(data.re[0], data.re[1], data.re[2]);
                if (data.ls != null && data.ls.Length >= 3)
                    LeftShoulder = new Vector3(data.ls[0], data.ls[1], data.ls[2]);
                if (data.rs != null && data.rs.Length >= 3)
                    RightShoulder = new Vector3(data.rs[0], data.rs[1], data.rs[2]);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Anadromo] Error parseando paquete pose: {e.Message}\nJSON: {json}");
            }
        }

        /// <summary>
        /// Estructura para deserializar el JSON de MediaPipe bridge.
        /// Los nombres de campo deben coincidir exactamente con las claves JSON.
        /// </summary>
        [Serializable]
        private class PoseData
        {
            public float t;      // timestamp
            public float[] lw;   // left wrist  [x, y, z]
            public float[] rw;   // right wrist [x, y, z]
            public float[] le;   // left elbow  [x, y, z]
            public float[] re;   // right elbow [x, y, z]
            public float[] ls;   // left shoulder [x, y, z]
            public float[] rs;   // right shoulder [x, y, z]
        }

        // ─── Debug: Gizmos ───

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !isReceiving) return;

            // Visualizar landmarks en Scene View (útil para debug)
            float scale = 2f;
            Vector3 center = transform.position + Vector3.up * 2f;

            // Brazo izquierdo (cyan)
            Gizmos.color = Color.cyan;
            DrawLandmark(LeftShoulder, center, scale);
            DrawLandmark(LeftElbow, center, scale);
            DrawLandmark(LeftWrist, center, scale);
            DrawConnection(LeftShoulder, LeftElbow, center, scale);
            DrawConnection(LeftElbow, LeftWrist, center, scale);

            // Brazo derecho (magenta)
            Gizmos.color = Color.magenta;
            DrawLandmark(RightShoulder, center, scale);
            DrawLandmark(RightElbow, center, scale);
            DrawLandmark(RightWrist, center, scale);
            DrawConnection(RightShoulder, RightElbow, center, scale);
            DrawConnection(RightElbow, RightWrist, center, scale);

            // Línea de hombros
            Gizmos.color = Color.white;
            DrawConnection(LeftShoulder, RightShoulder, center, scale);
        }

        private Vector3 LandmarkToWorld(Vector3 lm, Vector3 center, float scale)
        {
            return center + new Vector3(
                (lm.x - 0.5f) * scale,
                (lm.y - 0.5f) * scale,
                0
            );
        }

        private void DrawLandmark(Vector3 lm, Vector3 center, float scale)
        {
            Gizmos.DrawSphere(LandmarkToWorld(lm, center, scale), 0.04f);
        }

        private void DrawConnection(Vector3 from, Vector3 to, Vector3 center, float scale)
        {
            Gizmos.DrawLine(
                LandmarkToWorld(from, center, scale),
                LandmarkToWorld(to, center, scale)
            );
        }
    }
}
