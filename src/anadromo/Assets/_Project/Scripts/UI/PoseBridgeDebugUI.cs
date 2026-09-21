using UnityEngine;

namespace Anadromo.Systems
{
    /// <summary>
    /// Overlay de debug en pantalla que muestra el estado de conexión del PoseBridge,
    /// paquetes por segundo, y valores clave de los landmarks.
    /// 
    /// Fase 4 — F4.2: Visualización de debug del feed de cámara externa.
    /// Solo visible en el Editor (se desactiva automáticamente en builds).
    /// </summary>
    public class PoseBridgeDebugUI : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField] private ExternalCameraReceiver receiver;
        [SerializeField] private Anadromo.Locomotion.FlapDetector flapDetector;

        [Header("Configuración Visual")]
        [SerializeField] private bool showInBuild = false;
        [SerializeField] private bool showLandmarkValues = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private bool visible = true;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private bool stylesInitialized;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.75f));
            boxStyle.padding = new RectOffset(10, 10, 8, 8);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 13;
            labelStyle.normal.textColor = Color.white;
            labelStyle.richText = true;

            headerStyle = new GUIStyle(labelStyle);
            headerStyle.fontSize = 15;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            #if !UNITY_EDITOR
            if (!showInBuild) return;
            #endif

            if (!visible || receiver == null) return;

            InitStyles();

            float panelWidth = 280f;
            float x = Screen.width - panelWidth - 10f;
            float y = 10f;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, 400f), boxStyle);

            GUILayout.Label("🐟 Anádromo — Pose Bridge", headerStyle);
            GUILayout.Space(4);

            // Estado de conexión
            string statusIcon = receiver.IsReceiving ? "<color=#00FF00>●</color>" : "<color=#FF4444>●</color>";
            string statusText = receiver.IsReceiving ? "CONECTADO" : "SIN DATOS";
            GUILayout.Label($"{statusIcon} Estado: {statusText}", labelStyle);

            // FlapDetector
            if (flapDetector != null)
            {
                string opIcon = flapDetector.IsOperational ? "<color=#00FF00>●</color>" : "<color=#FFAA00>●</color>";
                string opText = flapDetector.IsOperational ? "OPERATIVO" : "ESPERANDO";
                GUILayout.Label($"{opIcon} Detector: {opText}", labelStyle);
            }

            GUILayout.Space(6);

            if (showLandmarkValues && receiver.IsReceiving)
            {
                GUILayout.Label("─── Muñecas ───", labelStyle);
                GUILayout.Label($"  Izq Y: <color=#00FFFF>{receiver.LeftWrist.y:F3}</color>", labelStyle);
                GUILayout.Label($"  Der Y: <color=#FF00FF>{receiver.RightWrist.y:F3}</color>", labelStyle);

                GUILayout.Space(4);
                GUILayout.Label("─── Hombros ───", labelStyle);
                GUILayout.Label($"  Izq Y: {receiver.LeftShoulder.y:F3}", labelStyle);
                GUILayout.Label($"  Der Y: {receiver.RightShoulder.y:F3}", labelStyle);
            }

            GUILayout.Space(4);
            GUILayout.Label($"<color=#888888>Presiona [{toggleKey}] para ocultar</color>", labelStyle);

            GUILayout.EndArea();
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
