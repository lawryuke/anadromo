using UnityEngine;
using UnityEngine.InputSystem;
using Anadromo.Config;

public class SimpleFlyCamera : MonoBehaviour
{
    [Header("Settings (se sobreescriben con GameSettings)")]
    public float movementSpeed = 10f;
    public float fastMovementSpeed = 25f;
    public float mouseSensitivity = 0.2f;
    [System.NonSerialized] public bool allowTranslation = true;
    [System.NonSerialized] public bool menuLook;
    [System.NonSerialized] public Vector3 externalVelocity;
    [System.NonSerialized] public float externalSpeedMultiplier = 1f;

    private float pitch = 0f;
    private float yaw = 0f;
    private Rigidbody rb;
    private Anadromo.Systems.PlayerEnergyController energy;

    private void Start()
    {
        // Set initial rotation based on current transform
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;
        
        // Bloquear el cursor en el centro y ocultarlo
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb = GetComponent<Rigidbody>();
        energy=GetComponent<Anadromo.Systems.PlayerEnergyController>();
    }

    public void SyncLookRotation()
    {
        pitch=transform.eulerAngles.x;
        if(pitch>180) pitch-=360;
        yaw=transform.eulerAngles.y;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null || mouse == null) return;

        // Liberar el cursor si pulsamos Escape
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Camera Rotation (Siempre activa si el cursor está bloqueado)
        if (Cursor.lockState == CursorLockMode.Locked || (menuLook && mouse.rightButton.isPressed))
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float sens = GameSettings.I ? GameSettings.I.mouseSensitivity : mouseSensitivity;
            sens *= energy ? energy.TurnMultiplier : 1f;
            yaw += mouseDelta.x * sens;
            pitch -= mouseDelta.y * sens;
            
            // Clamp pitch to avoid flipping over
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // Camera Movement
        if (!allowTranslation) return;
        float baseSpeed = GameSettings.I ? GameSettings.I.playerSpeed : movementSpeed;
        float sprintSpeed = GameSettings.I ? GameSettings.I.playerSprintSpeed : fastMovementSpeed;
        float currentSpeed = (keyboard.leftShiftKey.isPressed ? sprintSpeed : baseSpeed) * Mathf.Clamp(externalSpeedMultiplier,.25f,1f) * (energy ? energy.SwimMultiplier : 1f);
        Vector3 direction = Vector3.zero;

        if (keyboard.wKey.isPressed) direction += transform.forward;
        if (keyboard.sKey.isPressed) direction -= transform.forward;
        if (keyboard.dKey.isPressed) direction += transform.right;
        if (keyboard.aKey.isPressed) direction -= transform.right;
        if (keyboard.eKey.isPressed) direction += transform.up;
        if (keyboard.qKey.isPressed) direction -= transform.up;

        direction.Normalize();
        if(energy) energy.Energy.SetSprinting(keyboard.leftShiftKey.isPressed && direction.sqrMagnitude>0);
        Vector3 current=externalVelocity*(energy ? energy.CurrentMultiplier : 1f);

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = direction * currentSpeed + current;
        }
        else
        {
            transform.position += (direction * currentSpeed + current) * Time.deltaTime;
        }
    }
}
