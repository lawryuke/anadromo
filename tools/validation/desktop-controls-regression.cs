// Run through unity command eval_file in a fresh Play Mode session in Blockout_Test.
if (!UnityEngine.Application.isPlaying)
    throw new System.Exception("Play Mode required.");
var flight = UnityEngine.Object.FindAnyObjectByType<Anadromo.Mechanics.DebugVuelo>();
var swim = flight.GetComponent<Anadromo.Locomotion.SwimLocomotion>();
var body = flight.GetComponent<UnityEngine.Rigidbody>();
var camera = flight.cameraTransform;
var manager = UnityEngine.Object.FindAnyObjectByType<Anadromo.AI.FlockManager>();
if (swim.enabled || !flight.enabled || !body.isKinematic)
    throw new System.Exception("Desktop mode is not isolated from swimming.");
if (camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled ||
    UnityEngine.Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>() != null)
    throw new System.Exception("XR simulation is still active.");

var previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
var previousMouse = UnityEngine.InputSystem.Mouse.current;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
var originalMode = UnityEngine.Physics.simulationMode;
var originalPosition = body.position;
var originalRotation = camera.localRotation;
var results = new System.Collections.Generic.List<string>();
void Step(UnityEngine.InputSystem.LowLevel.KeyboardState keys, UnityEngine.InputSystem.LowLevel.MouseState pointer)
{
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, keys);
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, pointer);
    UnityEngine.InputSystem.InputSystem.Update();
    flight.SendMessage("Update");
    flight.SendMessage("FixedUpdate");
    UnityEngine.Physics.SyncTransforms();
    UnityEngine.Physics.Simulate(0.02f);
}
try
{
    UnityEngine.Physics.simulationMode = UnityEngine.SimulationMode.Script;
    Step(new UnityEngine.InputSystem.LowLevel.KeyboardState(),
        new UnityEngine.InputSystem.LowLevel.MouseState { delta = new UnityEngine.Vector2(100f, 40f) });
    if (UnityEngine.Vector3.Distance(body.position, originalPosition) > 0.001f ||
        UnityEngine.Quaternion.Angle(camera.localRotation, originalRotation) > 0.001f)
        throw new System.Exception("Unheld mouse moved the player or camera.");
    results.Add("PASS: free mouse does not move or rotate player");

    Step(new UnityEngine.InputSystem.LowLevel.KeyboardState(),
        new UnityEngine.InputSystem.LowLevel.MouseState { delta = new UnityEngine.Vector2(100f, 40f) }
            .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));
    if (UnityEngine.Quaternion.Angle(camera.localRotation, originalRotation) < 1f ||
        UnityEngine.Vector3.Distance(body.position, originalPosition) > 0.001f)
        throw new System.Exception("Right drag must rotate without translating.");
    results.Add("PASS: right drag rotates only");

    var bindings = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.S,
        UnityEngine.InputSystem.Key.A, UnityEngine.InputSystem.Key.D,
        UnityEngine.InputSystem.Key.Q, UnityEngine.InputSystem.Key.E };
    var directions = new[] { camera.forward, -camera.forward, -camera.right, camera.right,
        UnityEngine.Vector3.down, UnityEngine.Vector3.up };
    for (int i = 0; i < bindings.Length; i++)
    {
        var before = body.position;
        Step(new UnityEngine.InputSystem.LowLevel.KeyboardState(bindings[i]), new UnityEngine.InputSystem.LowLevel.MouseState());
        if (UnityEngine.Vector3.Dot(body.position - before, directions[i]) < 0.01f)
            throw new System.Exception("Translation failed for " + bindings[i]);
    }
    results.Add("PASS: WASD + Q/E translate in expected directions");
    var stoppedPosition = body.position;
    var stoppedRotation = camera.localRotation;
    Step(new UnityEngine.InputSystem.LowLevel.KeyboardState(), new UnityEngine.InputSystem.LowLevel.MouseState { delta = new UnityEngine.Vector2(80f, -30f) });
    if (UnityEngine.Vector3.Distance(body.position, stoppedPosition) > 0.001f ||
        UnityEngine.Quaternion.Angle(camera.localRotation, stoppedRotation) > 0.001f)
        throw new System.Exception("Movement or mouse look persisted after release.");
    results.Add("PASS: release stops movement and mouse look");
    Step(new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.J), new UnityEngine.InputSystem.LowLevel.MouseState());
    manager.SendMessage("Update");
    if (!manager.isGameStarted || swim.enabled || !flight.enabled)
        throw new System.Exception("J changed desktop/VR controls.");
    results.Add("PASS: J starts flock without enabling swimming");
}
finally
{
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
    if (previousKeyboard != null) previousKeyboard.MakeCurrent();
    if (previousMouse != null) previousMouse.MakeCurrent();
    UnityEngine.Physics.simulationMode = originalMode;
}
return results;
