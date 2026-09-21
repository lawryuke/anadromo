using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;

namespace Anadromo.Editor
{
    /// <summary>
    /// Configura automáticamente una escena para prototipado de locomoción VR por aleteo.
    /// 
    /// Menú: Anadromo > Setup Flap Locomotion Scene (Fase 1)
    ///        Anadromo > Fix XR Tracking (corrige TrackedPoseDrivers existentes)
    /// 
    /// Acciones del Setup:
    ///   1. Desactiva cámaras existentes (SimpleFlyCamera, etc.)
    ///   2. Crea XR Origin (VR) con:
    ///      - Camera Offset > Main Camera (TrackedPoseDriver → HMD)
    ///      - Camera Offset > Left Controller (TrackedPoseDriver → Mano izquierda)
    ///      - Camera Offset > Right Controller (TrackedPoseDriver → Mano derecha)
    ///   3. Crea SalmonBody (transform independiente para orientación del cuerpo)
    ///   4. Configura Rigidbody para física submarina
    /// </summary>
    public static class SetupFlapLocomotionScene
    {
        // ──────────────────────────────────────────────────
        //  BINDINGS XR (OpenXR estándar)
        // ──────────────────────────────────────────────────
        private const string HMD_POSITION    = "<XRHMD>/centerEyePosition";
        private const string HMD_ROTATION    = "<XRHMD>/centerEyeRotation";
        private const string LEFT_POSITION   = "<XRController>{LeftHand}/devicePosition";
        private const string LEFT_ROTATION   = "<XRController>{LeftHand}/deviceRotation";
        private const string RIGHT_POSITION  = "<XRController>{RightHand}/devicePosition";
        private const string RIGHT_ROTATION  = "<XRController>{RightHand}/deviceRotation";

        // ──────────────────────────────────────────────────
        //  MENU: Setup completo de escena
        // ──────────────────────────────────────────────────
        [MenuItem("Anadromo/Setup Flap Locomotion Scene (Fase 1)")]
        public static void Setup()
        {
            // --- Verificar si ya existe un XR Origin ---
            var existingOrigin = Object.FindAnyObjectByType<XROrigin>();
            if (existingOrigin != null)
            {
                bool continuar = EditorUtility.DisplayDialog(
                    "XR Origin ya existe",
                    $"Ya existe un XR Origin en la escena: '{existingOrigin.gameObject.name}'.\n\n" +
                    "¿Deseas eliminarlo y crear uno nuevo configurado correctamente?",
                    "Reemplazar", "Cancelar");

                if (!continuar) return;

                Undo.DestroyObjectImmediate(existingOrigin.gameObject);
                Debug.Log($"[Anadromo] XR Origin anterior eliminado.");
            }

            // --- Desactivar cámaras existentes ---
            DesactivarCamarasExistentes();

            // --- Crear jerarquía XR Origin ---
            GameObject xrOriginGO = CrearXROrigin();

            // --- Crear SalmonBody ---
            CrearSalmonBody(xrOriginGO);

            // --- Configurar Rigidbody ---
            ConfigurarRigidbody(xrOriginGO);

            // --- Seleccionar y marcar escena como modificada ---
            Selection.activeGameObject = xrOriginGO;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            LogResumen();
        }

        // ──────────────────────────────────────────────────
        //  MENU: Corregir tracking en escena existente
        // ──────────────────────────────────────────────────
        [MenuItem("Anadromo/Fix XR Tracking")]
        public static void FixTracking()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("No hay XR Origin",
                    "No se encontró un XR Origin en la escena.\n" +
                    "Usa 'Anadromo > Setup Flap Locomotion Scene' primero.",
                    "OK");
                return;
            }

            int fixed_count = 0;

            // Buscar todos los TrackedPoseDriver bajo el XR Origin
            var tpds = origin.GetComponentsInChildren<TrackedPoseDriver>(true);
            foreach (var tpd in tpds)
            {
                string goName = tpd.gameObject.name.ToLower();

                if (goName.Contains("camera") || tpd.CompareTag("MainCamera"))
                {
                    ConfigurarTPD(tpd, HMD_POSITION, HMD_ROTATION, "HMD");
                    fixed_count++;
                }
                else if (goName.Contains("left"))
                {
                    ConfigurarTPD(tpd, LEFT_POSITION, LEFT_ROTATION, "Left Controller");
                    fixed_count++;
                }
                else if (goName.Contains("right"))
                {
                    ConfigurarTPD(tpd, RIGHT_POSITION, RIGHT_ROTATION, "Right Controller");
                    fixed_count++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (fixed_count > 0)
            {
                Debug.Log($"✅ [Anadromo] {fixed_count} TrackedPoseDriver(s) configurados correctamente.");
                Debug.Log("[Anadromo] Guarda la escena (Ctrl+S) y dale Play para verificar.");
            }
            else
            {
                Debug.LogWarning("[Anadromo] No se encontraron TrackedPoseDrivers para corregir.");
            }
        }

        // ──────────────────────────────────────────────────
        //  MÉTODOS PRIVADOS
        // ──────────────────────────────────────────────────

        private static void DesactivarCamarasExistentes()
        {
            // Buscar y desactivar SimpleFlyCamera
            var flyCameras = Object.FindObjectsByType<SimpleFlyCamera>(FindObjectsSortMode.None);
            foreach (var fc in flyCameras)
            {
                fc.gameObject.SetActive(false);
                Debug.Log($"[Anadromo] '{fc.gameObject.name}' con SimpleFlyCamera DESACTIVADO.");
            }

            // Desactivar otras cámaras activas con tag MainCamera
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.CompareTag("MainCamera"))
                {
                    cam.gameObject.SetActive(false);
                    Debug.Log($"[Anadromo] Cámara '{cam.gameObject.name}' DESACTIVADA.");
                }
            }
        }

        private static GameObject CrearXROrigin()
        {
            // --- XR Origin root ---
            var xrOriginGO = new GameObject("XR Origin (VR)");
            Undo.RegisterCreatedObjectUndo(xrOriginGO, "Create XR Origin for Flap Locomotion");

            var xrOrigin = xrOriginGO.AddComponent<XROrigin>();
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // --- Camera Offset ---
            var cameraOffsetGO = new GameObject("Camera Offset");
            cameraOffsetGO.transform.SetParent(xrOriginGO.transform, false);
            xrOrigin.CameraFloorOffsetObject = cameraOffsetGO;

            // --- Main Camera (HMD) ---
            var mainCameraGO = CrearCamaraVR(cameraOffsetGO.transform);
            xrOrigin.Camera = mainCameraGO.GetComponent<Camera>();

            // --- Left Controller ---
            CrearController("Left Controller", cameraOffsetGO.transform,
                            LEFT_POSITION, LEFT_ROTATION);

            // --- Right Controller ---
            CrearController("Right Controller", cameraOffsetGO.transform,
                            RIGHT_POSITION, RIGHT_ROTATION);

            return xrOriginGO;
        }

        private static GameObject CrearCamaraVR(Transform parent)
        {
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(parent, false);

            // Camera
            var camera = cameraGO.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.Skybox;

            // Audio
            cameraGO.AddComponent<AudioListener>();

            // TrackedPoseDriver con bindings del HMD
            var tpd = cameraGO.AddComponent<TrackedPoseDriver>();
            ConfigurarTPD(tpd, HMD_POSITION, HMD_ROTATION, "HMD (Main Camera)");

            return cameraGO;
        }

        private static GameObject CrearController(string nombre, Transform parent,
                                                   string posBinding, string rotBinding)
        {
            var controllerGO = new GameObject(nombre);
            controllerGO.transform.SetParent(parent, false);

            // TrackedPoseDriver con bindings del controller
            var tpd = controllerGO.AddComponent<TrackedPoseDriver>();
            ConfigurarTPD(tpd, posBinding, rotBinding, nombre);

            return controllerGO;
        }

        /// <summary>
        /// Configura un TrackedPoseDriver con los bindings de posición y rotación especificados.
        /// Crea InputActions inline con los paths de control correctos.
        /// </summary>
        private static void ConfigurarTPD(TrackedPoseDriver tpd, string posBinding,
                                           string rotBinding, string label)
        {
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

            // Crear InputAction inline para posición
            var posAction = new InputAction(
                name: $"{label} Position",
                type: InputActionType.Value,
                binding: posBinding,
                expectedControlType: "Vector3"
            );

            // Crear InputAction inline para rotación
            var rotAction = new InputAction(
                name: $"{label} Rotation",
                type: InputActionType.Value,
                binding: rotBinding,
                expectedControlType: "Quaternion"
            );

            // Asignar al TrackedPoseDriver como acciones inline (no referencias)
            tpd.positionInput = new InputActionProperty(posAction);
            tpd.rotationInput = new InputActionProperty(rotAction);

            // Marcar el componente como modificado para que se serialice
            EditorUtility.SetDirty(tpd);

            Debug.Log($"[Anadromo] TrackedPoseDriver '{label}' configurado → pos: {posBinding}, rot: {rotBinding}");
        }

        private static void CrearSalmonBody(GameObject xrOriginGO)
        {
            var salmonBody = new GameObject("SalmonBody");
            salmonBody.transform.SetParent(xrOriginGO.transform, false);

            // Icono visual en el editor
            var icon = EditorGUIUtility.IconContent("sv_icon_dot6_pix16_gizmo");
            if (icon != null && icon.image != null)
            {
                EditorGUIUtility.SetIconForObject(salmonBody, (Texture2D)icon.image);
            }

            Debug.Log("[Anadromo] SalmonBody creado — representa la dirección de avance del salmón.");
        }

        private static void ConfigurarRigidbody(GameObject xrOriginGO)
        {
            var rb = xrOriginGO.AddComponent<Rigidbody>();

            rb.useGravity = false;
            rb.mass = 1f;
            rb.linearDamping = 3f;
            rb.angularDamping = 5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Solo rotación en Y (yaw por aleteo), no en X ni Z
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;

            EditorUtility.SetDirty(rb);

            Debug.Log("[Anadromo] Rigidbody: sin gravedad, drag=3, angularDrag=5, rot libre solo en Y.");
        }

        private static void LogResumen()
        {
            Debug.Log("══════════════════════════════════════════════════════════════");
            Debug.Log("✅ [Anadromo] Setup completado con TRACKING configurado");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Jerarquía:");
            Debug.Log("    XR Origin (VR)  [Rigidbody]");
            Debug.Log("    ├── Camera Offset");
            Debug.Log("    │   ├── Main Camera    [TPD → HMD center eye]");
            Debug.Log("    │   ├── Left Controller [TPD → Left hand]");
            Debug.Log("    │   └── Right Controller[TPD → Right hand]");
            Debug.Log("    └── SalmonBody");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Bindings configurados:");
            Debug.Log($"    HMD:   {HMD_POSITION} / {HMD_ROTATION}");
            Debug.Log($"    Left:  {LEFT_POSITION} / {LEFT_ROTATION}");
            Debug.Log($"    Right: {RIGHT_POSITION} / {RIGHT_ROTATION}");
            Debug.Log("──────────────────────────────────────────────────────────────");
            Debug.Log("  Siguiente:");
            Debug.Log("    1. Posiciona el XR Origin donde quieras empezar");
            Debug.Log("    2. Guarda la escena (Ctrl+S)");
            Debug.Log("    3. Conecta el Quest 2 y dale Play");
            Debug.Log("    4. Deberías poder mirar alrededor girando la cabeza ✅");
            Debug.Log("══════════════════════════════════════════════════════════════");
        }
    }
}
