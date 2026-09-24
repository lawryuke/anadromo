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
    ///   - Versión 2 añade version, tracked, aspect y visibilidad v (lw,rw,le,re,ls,rs).
    ///     t es el tiempo monotónico de captura; tracked=false indica pérdida de pose.
    /// 
    /// Uso:
    ///   1. Agregar este componente a un GameObject en la escena
    ///   2. Ejecutar el script Python: python mediapipe_bridge.py --port 5555
    ///   3. Al dar Play en Unity, este componente empieza a escuchar automáticamente
    ///   4. FlapDetector lee las propiedades LeftWrist, RightWrist, etc.
    /// </summary>
    [DefaultExecutionOrder(-100)]
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
        public uint SampleId { get; private set; }
        public float SampleDeltaTime { get; private set; }
        public float PoseAge => hasValidPacket ? Time.realtimeSinceStartup - lastPacketTime : float.PositiveInfinity;
        private double sampleTimestamp;
        private float aspectRatio = 4f / 3f;
        private bool hasPose;
        private float[] visibility;

        // Use a shoulder-based frame: translation, image size and body roll cancel out.
        public bool TryGetArmPoint(bool left, float minimumVisibility, out Vector2 point)
        {
            point = default;
            int wrist = left ? 0 : 1;
            int elbow = left ? 2 : 3;
            if (!hasPose || visibility == null || visibility[wrist] < minimumVisibility ||
                visibility[elbow] < minimumVisibility || visibility[4] < minimumVisibility ||
                visibility[5] < minimumVisibility) return false;

            Vector2 ls = ImagePoint(LeftShoulder), rs = ImagePoint(RightShoulder);
            Vector2 across = rs - ls;
            float width = across.magnitude;
            if (width < 0.04f) return false; // Too small or side-on for a reliable body frame.
            Vector2 horizontal = across / width;
            Vector2 vertical = new Vector2(-horizontal.y, horizontal.x);
            Vector2 shoulder = left ? ls : rs;
            Vector2 arm = (ImagePoint(left ? LeftWrist : RightWrist) * 0.8f +
                           ImagePoint(left ? LeftElbow : RightElbow) * 0.2f - shoulder) / width;
            point = new Vector2(Vector2.Dot(arm, horizontal), Vector2.Dot(arm, vertical));
            return true;
        }

        private Vector2 ImagePoint(Vector3 landmark) => new Vector2(landmark.x * aspectRatio, landmark.y);

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
        private bool hasValidPacket;

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
            hasValidPacket = false;
            hasPose = false;
            lock (dataLock) { latestPacket = null; hasNewData = false; }
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
                if (ParsePacket(packet))
                {
                    lastPacketTime = Time.realtimeSinceStartup;
                    hasValidPacket = true;
                    packetCount++;
                }
            }

            // Actualizar estado de conexión
            timeSinceLastPacket = Time.realtimeSinceStartup - lastPacketTime;
            isReceiving = hasValidPacket && timeSinceLastPacket < 1.0f;

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

        private bool ParsePacket(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<PoseData>(json);

                if (data == null || double.IsNaN(data.t) || double.IsInfinity(data.t) || data.t <= 0)
                    return false;
                if (hasValidPacket && data.t == sampleTimestamp) return false;

                SampleDeltaTime = hasValidPacket ? (float)(data.t - sampleTimestamp) : 0f;
                sampleTimestamp = data.t;
                aspectRatio = data.aspect > 0f && data.aspect < 10f ? data.aspect : 4f / 3f;
                visibility = data.version >= 2 && data.v != null && data.v.Length == 6
                    ? data.v : new float[] { 1, 1, 1, 1, 1, 1 };
                hasPose = data.version < 2 || data.tracked;
                if (data.version >= 2 && (data.v == null || data.v.Length != 6)) hasPose = false;
                float[][] points = { data.lw, data.rw, data.le, data.re, data.ls, data.rs };
                for (int i = 0; i < 6; i++)
                    if (!ValidPoint(points[i]) || float.IsNaN(visibility[i]) || float.IsInfinity(visibility[i]))
                        visibility[i] = 0f;
                LeftWrist = ReadPoint(data.lw);
                RightWrist = ReadPoint(data.rw);
                LeftElbow = ReadPoint(data.le);
                RightElbow = ReadPoint(data.re);
                LeftShoulder = ReadPoint(data.ls);
                RightShoulder = ReadPoint(data.rs);
                SampleId++;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Anadromo] Error parseando paquete pose: {e.Message}\nJSON: {json}");
                return false;
            }
        }

        private static bool ValidPoint(float[] values)
        {
            if (values == null || values.Length < 3) return false;
            for (int i = 0; i < 3; i++)
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) return false;
            return true;
        }

        private static Vector3 ReadPoint(float[] values) => ValidPoint(values)
            ? new Vector3(values[0], values[1], values[2]) : Vector3.zero;

        /// <summary>
        /// Estructura para deserializar el JSON de MediaPipe bridge.
        /// Los nombres de campo deben coincidir exactamente con las claves JSON.
        /// </summary>
        [Serializable]
        private class PoseData
        {
            public double t;     // Capture timestamp; float loses frame precision at epoch magnitudes.
            public int version;
            public bool tracked;
            public float aspect;
            public float[] v;    // lw, rw, le, re, ls, rs
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
