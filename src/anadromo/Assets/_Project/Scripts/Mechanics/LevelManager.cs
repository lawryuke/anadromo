using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Anadromo.Mechanics;

namespace Anadromo.Logic
{
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("Anadromo/Logic/Level Manager")]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }
        public GamePhase currentPhase = GamePhase.WaitingForStart;
        public SwimGroupController salmonesLeft, salmonesRight, salmonesTop;
        public SharkGroupMovement orcaGroup; // Compatibility with older scenes.
        public OrcaGroupMovement[] orcaGroups = new OrcaGroupMovement[0];
        public BloopFishMovement bloopMovement;
        public Transform krillNormalLeft, krillNormalRight1, krillNormalRight2;
        public Transform krillScaryLeft, krillScaryRight, krillScaryMid;
        public BoxObjectSpawner krillFirstMid;
        public PlayerFeeding playerFeeding;
        public Camera playerCamera;
        public Transform targetMid, topReference, orcaLimit, firstBloopLimit, secondBloopLimit;
        public ZoneLimit initialZone, abysmZone, caveZone;
        public LevelEndingEffects endingEffects;
        [Min(1)] public int requiredAbysmMeals = 5;
        public bool countMealsFromStart;
        [Tooltip("0 desactiva el tiempo alternativo. Se cuenta desde la subida de orcas.")]
        [Min(0)] public float bloopTimeout = 30;
        public float firstLimitBloop = -5;
        public float secondLimitBloop = 12;
        public int playerEatenCount;
        public bool isPlayerInAbysm;
        public bool showStartButton = true;
        public bool autoStart;
        public bool IsReady { get; private set; }
        public string ConfigurationError { get; private set; }
        readonly LevelProgression progress = new LevelProgression();
        readonly List<BoxObjectSpawner> preyGroups = new List<BoxObjectSpawner>();
        readonly List<Behaviour> movementControls = new List<Behaviour>();
        SwimGroupController[] salmon;
        float phaseTime;
        int abysmMealBaseline;
        bool legacyLeft, legacyCave;
        Rigidbody playerBody;
        bool bodyWasKinematic;

        void Awake()
        {
            if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
            { enabled = false; return; }
            Instance = this;
            currentPhase = GamePhase.WaitingForStart;
            playerEatenCount = 0;
            isPlayerInAbysm = false;
            salmon = new[] { salmonesLeft, salmonesRight, salmonesTop };
            foreach (var group in salmon) if (group != null) group.WaitForLevel();
            if (bloopMovement != null) bloopMovement.allowKeyboard = false;
            foreach (var group in orcaGroups) if (group != null) group.allowKeyboard = false;
            if (playerFeeding != null) playerFeeding.consumptionEnabled = false;
            // Reference animals are templates; clones are explicitly activated by the spawner.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var spawner in root.GetComponentsInChildren<BoxObjectSpawner>(true))
                    if (spawner.sourceObject != null && spawner.sourceObject.scene == gameObject.scene)
                        spawner.sourceObject.SetActive(false);
        }

        IEnumerator Start()
        {
            // Let Unity finish all Start callbacks (camera setup, spawners and input modes).
            yield return null;
            if (playerCamera == null && playerFeeding != null)
                playerCamera = playerFeeding.GetComponentInChildren<Camera>();
            if (!ValidateConfiguration(out string error))
            {
                ConfigurationError = error;
                Debug.LogError("Configuración del nivel: " + error, this);
                yield break;
            }
            RegisterPrey(krillNormalLeft, "Food_Krill");
            RegisterPrey(krillNormalRight1, "Food_Krill");
            RegisterPrey(krillNormalRight2, "Food_Krill");
            RegisterPrey(krillFirstMid.transform, "Food_PlayerOnly_First");
            RegisterPrey(krillScaryLeft, "Food_Krill_Scary");
            RegisterPrey(krillScaryRight, "Food_Krill_Scary");
            RegisterPrey(krillScaryMid, "Food_PlayerOnly");
            foreach (var group in salmon) group.GetComponent<BoxObjectSpawner>().Initialize();
            foreach (var group in orcaGroups) if (group != null) group.Prepare();
            foreach (var group in preyGroups)
                if (group.InitialPopulation == 0)
                {
                    ConfigurationError = group.name + " no generó ninguna presa; revisa sus límites y colliders.";
                    Debug.LogError(ConfigurationError, group);
                    yield break;
                }
            var scaryMid = krillScaryMid.GetComponent<BoxObjectSpawner>();
            if (!countMealsFromStart && scaryMid.AliveCount < requiredAbysmMeals)
            {
                ConfigurationError = "El grupo central Scary necesita al menos " + requiredAbysmMeals + " presas.";
                Debug.LogError(ConfigurationError, scaryMid);
                yield break;
            }
            CaptureMovementControls();
            SetPlayerMovement(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsReady = true;
            if (autoStart) StartGame();
        }

        void RegisterPrey(Transform root, string tag)
        {
            var spawner = root.GetComponent<BoxObjectSpawner>();
            spawner.spawnedTag = tag;
            spawner.markAsPrey = true;
            spawner.Initialize();
            foreach (var item in spawner.GeneratedObjects)
            {
                if (item == null) continue;
                item.tag = tag;
                if (item.GetComponent<Prey>() == null) item.AddComponent<Prey>();
            }
            preyGroups.Add(spawner);
            var controller = root.GetComponent<SwimGroupController>();
            if (controller != null) controller.NormalInSpawnBox();
        }

        public bool ValidateConfiguration(out string error)
        {
            error = null;
            foreach (var group in new[] { salmonesLeft, salmonesRight, salmonesTop })
                if (group == null) { error = "Falta un cardumen de salmones."; return false; }
            foreach (var root in new[] { krillNormalLeft, krillNormalRight1, krillNormalRight2,
                krillScaryLeft, krillScaryRight, krillScaryMid })
                if (root == null || root.GetComponent<BoxObjectSpawner>() == null)
                { error = "Falta un generador de krils."; return false; }
            if (krillFirstMid == null || playerFeeding == null || playerCamera == null ||
                initialZone == null || abysmZone == null || caveZone == null || targetMid == null ||
                topReference == null || orcaLimit == null || bloopMovement == null || endingEffects == null)
            { error = "Faltan referencias de jugador, zonas, límites o efectos."; return false; }
            if (orcaGroups == null || orcaGroups.Length == 0)
            { error = "Faltan los grupos de orcas."; return false; }
            foreach (var group in orcaGroups)
                if (group == null) { error = "Hay una referencia de orca vacía."; return false; }
            if (SecondLimit <= FirstLimit || FirstLimit <= bloopMovement.puntoInicio.y ||
                topReference.position.y < SecondLimit || topReference.position.y < orcaLimit.position.y)
            { error = "Los límites verticales están fuera de orden."; return false; }
            return true;
        }

        float FirstLimit => firstBloopLimit != null ? firstBloopLimit.position.y : firstLimitBloop;
        float SecondLimit => secondBloopLimit != null ? secondBloopLimit.position.y : secondLimitBloop;

        public void StartGame()
        {
            if (!IsReady || !progress.Start()) return;
            SetPlayerMovement(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            playerFeeding.consumptionEnabled = true;
            playerFeeding.edibleTags = new[] { "Food_PlayerOnly_First" };
            foreach (var group in salmon) group.HoldFacing(targetMid);
            EnterPhase();
        }

        void Update()
        {
            if (!IsReady || progress.Phase == GamePhase.WaitingForStart) return;
            phaseTime += Time.deltaTime;
            playerEatenCount = playerFeeding.TotalConsumed;
            Vector3 point = playerCamera.transform.position;
            isPlayerInAbysm = Contains(abysmZone, point);
            int meals = countMealsFromStart ? playerEatenCount :
                playerFeeding.ConsumedWithTag("Food_PlayerOnly") - abysmMealBaseline;
            bool allAbove = true;
            foreach (var group in orcaGroups) allAbove &= group != null && group.AllAbove(orcaLimit.position.y);
            bool changed = progress.Tick(!Contains(initialZone, point) || legacyLeft,
                krillFirstMid.IsInitialized && krillFirstMid.InitialPopulation > 0 && krillFirstMid.AliveCount == 0,
                isPlayerInAbysm, meals, requiredAbysmMeals, Contains(caveZone, point) || legacyCave,
                allAbove, bloopTimeout > 0 && phaseTime >= bloopTimeout, bloopMovement.transform.position.y,
                FirstLimit, SecondLimit, endingEffects.IsComplete);
            if (changed) EnterPhase();
        }

        void EnterPhase()
        {
            currentPhase = progress.Phase;
            phaseTime = 0;
            switch (currentPhase)
            {
                case GamePhase.KrillFeeding:
                    salmonesLeft.Hunt(krillNormalLeft.GetComponent<BoxObjectSpawner>(), "Food_Krill");
                    salmonesRight.Hunt(krillNormalRight1.GetComponent<BoxObjectSpawner>(), "Food_Krill");
                    salmonesTop.Hunt(krillNormalRight2.GetComponent<BoxObjectSpawner>(), "Food_Krill");
                    break;
                case GamePhase.AbysmDescent:
                    abysmMealBaseline = playerFeeding.ConsumedWithTag("Food_PlayerOnly");
                    playerFeeding.edibleTags = new[] { "Food_PlayerOnly" };
                    salmonesLeft.Hunt(krillScaryLeft.GetComponent<BoxObjectSpawner>(), "Food_Krill_Scary");
                    salmonesRight.Hunt(krillScaryRight.GetComponent<BoxObjectSpawner>(), "Food_Krill_Scary");
                    salmonesTop.Hunt(krillScaryRight.GetComponent<BoxObjectSpawner>(), "Food_Krill_Scary");
                    break;
                case GamePhase.OrcaAscent:
                    foreach (var group in salmon) group.Desperate();
                    foreach (var group in orcaGroups) group.BeginAscent(topReference.position.y);
                    break;
                case GamePhase.BloopAwakening:
                    bloopMovement.BeginAscent(topReference.position.y);
                    break;
                case GamePhase.Trembling:
                    endingEffects.StartTrembling(playerCamera);
                    break;
                case GamePhase.Rockfall:
                    endingEffects.BeginRockfall(playerCamera);
                    break;
                case GamePhase.Complete:
                    SetPlayerMovement(false);
                    playerFeeding.consumptionEnabled = false;
                    break;
            }
            Debug.Log("Fase: " + currentPhase, this);
        }

        public static bool Contains(ZoneLimit zone, Vector3 worldPoint)
        {
            return zone != null && zone.Contains(worldPoint);
        }

        void CaptureMovementControls()
        {
            foreach (var component in playerFeeding.transform.root.GetComponentsInChildren<MonoBehaviour>())
                if (component.enabled && (component is SimpleFlyCamera || component is DebugVuelo ||
                    component is Anadromo.Locomotion.FlapSwimController ||
                    component is Anadromo.Locomotion.SwimLocomotion))
                    movementControls.Add(component);
            playerBody = playerFeeding.GetComponentInParent<Rigidbody>();
            if (playerBody != null) bodyWasKinematic = playerBody.isKinematic;
        }

        void SetPlayerMovement(bool active)
        {
            foreach (var component in movementControls) if (component != null) component.enabled = active;
            if (playerBody == null) return;
            if (!playerBody.isKinematic) { playerBody.linearVelocity = Vector3.zero; playerBody.angularVelocity = Vector3.zero; }
            playerBody.isKinematic = !active || bodyWasKinematic;
        }

        void OnGUI()
        {
            if (!IsReady || progress.Phase != GamePhase.WaitingForStart || !showStartButton) return;
            if (GUI.Button(new Rect((Screen.width - 220) * .5f, (Screen.height - 64) * .5f, 220, 64), "Iniciar partida"))
                StartGame();
        }

        // Existing UnityEvents remain valid; occupancy in the wired boxes is evaluated every frame.
        public void OnPlayerLeaveInitZone() { if (initialZone == null) legacyLeft = true; }
        public void OnMidKrillsDepleted() { }
        public void OnPlayerEnterAbysm() { }
        public void CheckOrcaAscentCondition() { }
        public void OnPlayerEnterCave() { if (caveZone == null) legacyCave = true; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
