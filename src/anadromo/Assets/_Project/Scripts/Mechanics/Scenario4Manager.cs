using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Anadromo.Mechanics
{
    public enum Scenario4Phase { Ready, Awakening, Escape, Won, Caught }

    /// <summary>Independent encounter: no food, orca or Act 1 progression dependencies.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class Scenario4Manager : MonoBehaviour
    {
        public Camera viewer;
        public Scenario4Swimmer swimmer;
        public FallingRockShowerSpawner rocks;
        public BloopMouthRisingController bloop;
        public Scenario4VisibilityController vision;
        public Transform exit;
        public Transform[] route;
        public float exitRadius = 2.6f;
        public float awakeningDuration = 5;
        public Scenario4Phase Phase { get; private set; }
        public float Progress { get; private set; }
        public bool Running => Phase == Scenario4Phase.Escape;
        public bool HazardsActive => Phase == Scenario4Phase.Awakening || Running;
        public float Threat => bloop ? bloop.Threat : 0;
        float phaseTime;
        Vector3 startPosition;
        Quaternion startRotation;
        bool xrPressed;
        TextMesh worldStatus;

        void Start()
        {
            if (!viewer || !swimmer || !rocks || !bloop || !vision || !exit)
            { Debug.LogError("Escenario 4: faltan referencias del encuentro.", this); enabled = false; return; }
            startPosition = swimmer.transform.position;
            startRotation = swimmer.transform.rotation;
            rocks.Initialize(this);
            bloop.Initialize(this);
            vision.Initialize(this);
            var sign = new GameObject("Instrucciones del encuentro");
            sign.transform.SetParent(transform, false);
            sign.transform.position = startPosition + viewer.transform.forward * 3;
            sign.transform.rotation = viewer.transform.rotation;
            worldStatus = sign.AddComponent<TextMesh>();
            worldStatus.anchor = TextAnchor.MiddleCenter;
            worldStatus.alignment = TextAlignment.Center;
            worldStatus.fontSize = 48;
            worldStatus.characterSize = .035f;
            worldStatus.color = new Color(.65f, 1, .92f);
            ResetEncounter();
        }

        public void ResetEncounter()
        {
            if (!rocks || !bloop || !vision) return;
            Phase = Scenario4Phase.Ready;
            phaseTime = 0;
            Progress = 0;
            swimmer.Teleport(startPosition, startRotation);
            swimmer.canMove = false;
            rocks.ResetShower();
            bloop.ResetPursuit();
            vision.ResetVision();
            SetCursor(false);
            if (worldStatus)
            {
                worldStatus.transform.position = viewer.transform.position + viewer.transform.forward * 3;
                worldStatus.transform.rotation = viewer.transform.rotation;
                worldStatus.text = "LA GARGANTA DEL BLOOP\nAvanza hacia la luz de la salida y esquiva el desprendimiento de rocas\nEl temblor y los crujidos avisan la caída\nEspacio / gatillo para comenzar";
                worldStatus.gameObject.SetActive(true);
            }
        }

        public void BeginEscape()
        {
            if (Phase != Scenario4Phase.Ready) return;
            Phase = Scenario4Phase.Awakening;
            phaseTime = 0;
            swimmer.canMove = true;
            if (worldStatus) worldStatus.gameObject.SetActive(false);
            SetCursor(true);
            rocks.StartShower();
        }

        void Update()
        {
            if (!viewer || !swimmer) return;
            var keyboard = Keyboard.current;
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool pressed = hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigger) && trigger;
            bool confirm = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) || (pressed && !xrPressed);
            xrPressed = pressed;
            if (Phase == Scenario4Phase.Ready && confirm) BeginEscape();
            else if ((Phase == Scenario4Phase.Won || Phase == Scenario4Phase.Caught) &&
                     (confirm || (keyboard != null && keyboard.rKey.wasPressedThisFrame))) ResetEncounter();
            phaseTime += Time.deltaTime;
            if (Phase == Scenario4Phase.Awakening && phaseTime >= awakeningDuration)
            { Phase = Scenario4Phase.Escape; phaseTime = 0; rocks.StartShower(); }
            if (Phase != Scenario4Phase.Awakening && !Running) return;
            Progress = Mathf.Clamp01(Mathf.InverseLerp(startPosition.y, exit.position.y, viewer.transform.position.y));
            if (Vector3.Distance(viewer.transform.position, exit.position) <= exitRadius) Finish(true);
            else if (Running && bloop.Contains(viewer.transform.position)) Finish(false);
        }

        public void RockHit(float diameter)
        {
            if (!Running) return;
            float severity = Mathf.InverseLerp(.25f, .7f, diameter);
            swimmer.Stagger(Mathf.Lerp(1.2f, 2.5f, severity));
            vision.HitFeedback(Mathf.Lerp(.45f, 1, severity));
            vision.AddSediment(.15f);
        }

        public void BloopStrike()
        {
            if (!Running) return;
            swimmer.Stagger(1.8f);
            vision.HitFeedback(.85f);
        }

        void Finish(bool won)
        {
            Phase = won ? Scenario4Phase.Won : Scenario4Phase.Caught;
            swimmer.canMove = false;
            rocks.ResetShower();
            SetCursor(false);
            if (!worldStatus) return;
            worldStatus.transform.position = viewer.transform.position + viewer.transform.forward * 2.8f;
            worldStatus.transform.rotation = viewer.transform.rotation;
            worldStatus.text = (won ? "ESCAPASTE DEL ABISMO" : "EL BLOOP TE ATRAPO") + "\nEspacio / gatillo: volver al inicio";
            worldStatus.gameObject.SetActive(true);
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnGUI()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
            GUI.Box(new Rect(18, 18, 470, 126), "ESCENARIO 4 · LA GARGANTA DEL BLOOP");
            GUI.Label(new Rect(30, 43, 450, 28), "WASD: nadar · E/Q: subir/bajar · ratón: mirar · Esc: cursor");
            GUI.Label(new Rect(30, 71, 450, 28), Phase == Scenario4Phase.Ready ? "Espacio para comenzar" :
                Phase == Scenario4Phase.Awakening ? "Algo despierta debajo de ti. Sigue la salida..." :
                Running ? swimmer.IsStaggered ? "Impacto: desorientado y velocidad reducida..." :
                bloop.IsWarning ? "¡El Bloop se acerca desde el fondo!" : $"Ascenso: {Progress:P0} · Esquiva la caída de rocas" :
                Phase == Scenario4Phase.Won ? "Escapaste. Espacio o R para reiniciar." : "El Bloop te atrapó. Espacio o R para reiniciar.");
            GUI.Label(new Rect(30, 101, 450, 28), "El temblor y polvo avisan el desprendimiento de rocas");
            if (Phase == Scenario4Phase.Ready && GUI.Button(new Rect(18, 155, 180, 38), "Comenzar")) BeginEscape();
        }

        void OnDisable() { SetCursor(false); if (rocks) rocks.ResetShower(); }
    }
}
