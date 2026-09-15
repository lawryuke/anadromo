// Run with unity command eval_file after entering Play Mode in Blockout_Test.
// This changes runtime objects. Stop Play Mode afterwards; do not save the scene.
if (!UnityEngine.Application.isPlaying)
    throw new System.Exception("Enter Play Mode in Blockout_Test first.");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Blockout_Test")
    throw new System.Exception("Expected Blockout_Test.");

var manager = UnityEngine.Object.FindAnyObjectByType<Anadromo.AI.FlockManager>();
var swim = UnityEngine.Object.FindAnyObjectByType<Anadromo.Locomotion.SwimLocomotion>();
var flight = swim.GetComponent<Anadromo.Mechanics.DebugVuelo>();
var energy = swim.GetComponent<Anadromo.Systems.EnergySystem>();
var mouth = swim.GetComponentInChildren<Anadromo.Mechanics.PlayerFeeding>();
var fish = manager.allFish.Select(go => go.GetComponent<Anadromo.AI.FishBoid>()).ToArray();
if (manager.isGameStarted || swim.enabled || !flight.enabled || fish.Length != 40)
    throw new System.Exception("Waiting phase: expected 40 fish and desktop controls only.");
if (manager.krillZoneTarget == null || mouth == null || energy == null)
    throw new System.Exception("Feeding scene references are incomplete.");

manager.StartGame();
if (swim.enabled || !flight.enabled)
    throw new System.Exception("StartGame changed the desktop/VR control selection.");

var originalPlayer = manager.player;
var originalCameraPosition = originalPlayer.position;
var originalPhysicsMode = UnityEngine.Physics.simulationMode;
var originalEnergyEnabled = energy.enabled;
var results = new System.Collections.Generic.List<string>();
try
{
    results.Add("PASS: waiting and StartGame");
    manager.player = null;
    var arrival = typeof(Anadromo.AI.FishBoid).GetField("hasArrived",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    float delta = UnityEngine.Time.deltaTime;
    if (delta <= 0f) throw new System.Exception("A running frame is required.");
    float initialDistance = fish.Average(f => UnityEngine.Vector3.Distance(f.transform.position, manager.krillZoneTarget.position));
    int limit = UnityEngine.Mathf.CeilToInt(120f / delta);
    int steps = 0;
    // Exercise real movement updates for up to 120 simulated seconds, without waiting in real time.
    for (; steps < limit; steps++)
    {
        foreach (var boid in fish) boid.SendMessage("Update");
        if (fish.All(f => (bool)arrival.GetValue(f))) break;
    }
    float finalDistance = fish.Average(f => UnityEngine.Vector3.Distance(f.transform.position, manager.krillZoneTarget.position));
    if (steps == limit || finalDistance >= initialDistance || finalDistance > 8f)
        throw new System.Exception("Migration did not bring all fish into the krill zone.");
    results.Add("PASS: 40 fish arrived; mean distance " + initialDistance + " -> " + finalDistance);

    // After arrival, crossing the migration radius must still allow player avoidance.
    var testFish = fish[0];
    testFish.transform.position = manager.krillZoneTarget.position + UnityEngine.Vector3.right * (manager.migrationStopDistance + 1f);
    originalPlayer.position = testFish.transform.position + UnityEngine.Vector3.back * 0.2f;
    manager.player = originalPlayer;
    testFish.SendMessage("Update");
    if (testFish.speed <= manager.maxSpeed)
        throw new System.Exception("Fish re-entered migration and ignored the nearby player.");
    manager.player = null;
    testFish.transform.position = manager.krillZoneTarget.position + UnityEngine.Vector3.right * 100f;
    testFish.SendMessage("Update");
    if (testFish.speed > manager.maxSpeed)
        throw new System.Exception("Flee speed persisted after losing the player.");
    results.Add("PASS: avoidance beyond arrival radius and recovery of normal speed");

    originalPlayer.position = originalCameraPosition;
    energy.enabled = false;
    energy.ConsumeEnergy(100f);
    energy.RestoreEnergy(30f);
    var prey = UnityEngine.Object.FindObjectsByType<Anadromo.Mechanics.Prey>(UnityEngine.FindObjectsSortMode.None)[0];
    prey.GetComponent<Anadromo.Mechanics.Krill>().enabled = false;
    prey.transform.position = mouth.transform.position;
    UnityEngine.Physics.simulationMode = UnityEngine.SimulationMode.Script;
    UnityEngine.Physics.SyncTransforms();
    UnityEngine.Physics.Simulate(0.02f);
    float expected = (30f + prey.energyValue) / 100f;
    if (prey.enabled || UnityEngine.Mathf.Abs(energy.GetEnergyPercentage() - expected) > 0.001f)
        throw new System.Exception("Physics contact did not consume prey and restore energy.");
    mouth.SendMessage("OnTriggerEnter", prey.GetComponent<UnityEngine.Collider>());
    if (UnityEngine.Mathf.Abs(energy.GetEnergyPercentage() - expected) > 0.001f)
        throw new System.Exception("Repeated contact restored energy twice.");
    results.Add("PASS: physics contact restores 15 energy exactly once");
}
finally
{
    manager.player = originalPlayer;
    originalPlayer.position = originalCameraPosition;
    UnityEngine.Physics.simulationMode = originalPhysicsMode;
    energy.enabled = originalEnergyEnabled;
}
return results;
