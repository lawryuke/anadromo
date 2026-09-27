using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anadromo.CavernMVP
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CavernPlayer : MonoBehaviour
    {
        public Camera view;
        public float quietSpeed = 2.4f, sprintSpeed = 6f;
        public float Health { get; private set; } = 100f;
        public Vector3 Velocity { get; private set; }
        public bool Finished { get; private set; }
        public bool Active => Health > 0 && !Finished;
        public float SpeedMultiplier => Mathf.Max(.25f, 1f - .15f * attached.Count);
        public int AttachedCount => attached.Count;
        public string Zone { get; set; } = "A · Umbral";
        public float DamagePulse { get; private set; }
        readonly List<CavernLamprey> attached = new List<CavernLamprey>();
        CharacterController controller;
        float yaw, pitch, lastSideTime = -10, shakeCooldown;
        int lastSide;

        void Awake() { controller = GetComponent<CharacterController>(); yaw = transform.eulerAngles.y; }
        void Start() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        void Update()
        {
            DamagePulse = Mathf.MoveTowards(DamagePulse, 0, Time.deltaTime * 1.5f);
            shakeCooldown -= Time.deltaTime;
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (m != null && m.leftButton.wasPressedThisFrame && Active) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (k.rKey.wasPressedThisFrame) { GetComponentInParent<CavernMvpWorld>().Restart(); return; }
            Velocity = Vector3.zero;
            if (!Active || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 mouse = m == null ? Vector2.zero : m.delta.ReadValue();
            yaw += mouse.x * .13f;
            pitch = Mathf.Clamp(pitch - mouse.y * .13f, -80, 80);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            // Shake is visual only; it cannot feed back into the input detector.
            view.transform.localPosition = Vector3.right * (Mathf.Sin(Time.time * 65) * DamagePulse * .045f);
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            float y = (k.eKey.isPressed ? 1 : 0) - (k.qKey.isPressed ? 1 : 0);
            Vector3 direction = Vector3.ClampMagnitude(view.transform.forward * z + transform.right * x + Vector3.up * y, 1);
            Vector3 before = transform.position;
            controller.Move(direction * ((k.leftShiftKey.isPressed ? sprintSpeed : quietSpeed) * SpeedMultiplier * Time.deltaTime));
            Velocity = (transform.position - before) / Mathf.Max(Time.deltaTime, .0001f);
            int side = k.aKey.wasPressedThisFrame ? -1 : k.dKey.wasPressedThisFrame ? 1 : 0;
            bool alternated = side != 0 && lastSide != 0 && side != lastSide && Time.time - lastSideTime <= .6f;
            if (side != 0) { lastSide = side; lastSideTime = Time.time; }
            if (shakeCooldown <= 0 && (Mathf.Abs(mouse.x) > 50 || alternated))
            {
                Shake(alternated ? 35 : 25);
                shakeCooldown = .12f;
            }
        }

        public void TakeDamage(float damage)
        {
            if (!Active) return;
            Health = Mathf.Max(0, Health - damage);
            DamagePulse = 1;
            if (Health <= 0) { Velocity = Vector3.zero; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        public void Add(CavernLamprey lamprey) { if (!attached.Contains(lamprey)) attached.Add(lamprey); DamagePulse = .5f; }
        public void Remove(CavernLamprey lamprey) { attached.Remove(lamprey); }
        public void Shake(float power) { for (int i = attached.Count - 1; i >= 0; i--) attached[i].ReduceGrip(power); }
        public void Complete() { if (!Active) return; Finished = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
