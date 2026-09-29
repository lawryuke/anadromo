using UnityEngine;

namespace Anadromo.Locomotion
{
    // Samples tracking-space positions, never the animated camera or world-space hands.
    public sealed class ShakeStrokeDetector
    {
        bool initialized;
        Vector3 previous, anchor, direction;
        float clock, lastStrokeAt;

        public void Reset() { initialized = false; direction = Vector3.zero; clock = 0; }

        public bool Sample(Vector3 point, float dt, float minimumTravel, float minimumSpeed,
            float reversalWindow, float maximumSpeed = 5f)
        {
            if (dt <= 0 || dt > .15f || !float.IsFinite(point.x) ||
                !float.IsFinite(point.y) || !float.IsFinite(point.z)) { Reset(); return false; }
            clock += dt;
            if (!initialized) { initialized = true; previous = anchor = point; return false; }
            float speed = Vector3.Distance(point, previous) / dt;
            previous = point;
            if (speed > maximumSpeed) { Reset(); return false; }
            if (speed < minimumSpeed) { anchor = point; return false; }
            Vector3 travel = point - anchor;
            if (travel.magnitude < minimumTravel) return false;
            Vector3 nextDirection = travel.normalized;
            bool reversed = direction.sqrMagnitude > 0 && clock - lastStrokeAt <= reversalWindow &&
                Vector3.Dot(direction, nextDirection) < -.5f;
            anchor = point; direction = nextDirection; lastStrokeAt = clock;
            return reversed;
        }
    }
}
