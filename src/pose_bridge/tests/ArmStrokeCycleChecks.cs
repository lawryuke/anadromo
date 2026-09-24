using System;
using UnityEngine;
using Anadromo.Locomotion;

// Executes the production detector, without a webcam or a Unity scene.
public static class ArmStrokeCycleChecks
{
    private static readonly ArmStrokeCycle.Parameters Settings = ArmStrokeCycle.Parameters.Default;
    private static int checks;

    private static void Equal(string name, int actual, int expected)
    {
        if (actual != expected) throw new Exception(name + ": expected " + expected + ", got " + actual);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    private static int Replay(Func<float, Vector2> trajectory, float seconds, int fps)
    {
        var cycle = new ArmStrokeCycle();
        int count = 0;
        float dt = 1f / fps;
        for (int i = 0; i <= (int)(seconds * fps); i++)
            if (cycle.Step(trajectory(i * dt), dt, Settings, out float intensity))
            {
                if (intensity < Settings.minimumIntensity || intensity > 1f)
                    throw new Exception("Intensity outside configured range");
                count++;
            }
        return count;
    }

    private static float Stroke(float t)
    {
        float phase = t % 1.2f;
        if (phase < 0.4f) return 0.8f * phase / 0.4f;
        if (phase < 0.5f) return 0.8f;
        if (phase < 0.9f) return 0.8f * (0.9f - phase) / 0.4f;
        return 0f;
    }

    public static int Main()
    {
        foreach (int fps in new[] { 15, 30, 60 })
        {
            Equal("vertical cycles at " + fps, Replay(t => new Vector2(0, Stroke(t)), 3.6f, fps), 3);
            Equal("lateral cycles at " + fps, Replay(t => new Vector2(Stroke(t), 0), 3.6f, fps), 3);
            Equal("diagonal cycles at " + fps, Replay(t => new Vector2(Stroke(t), Stroke(t)) * 0.7071f, 3.6f, fps), 3);
            Equal("circular cycles at " + fps, Replay(t => new Vector2(
                0.4f * (float)Math.Cos(t * Math.PI * 2 / 1.6),
                0.4f * (float)Math.Sin(t * Math.PI * 2 / 1.6)), 4.8f, fps), 3);
            Equal("stationary jitter at " + fps, Replay(t => new Vector2(
                0.01f * (float)Math.Sin(t * 15), 0.01f * (float)Math.Cos(t * 17)), 10, fps), 0);
            Equal("extended hold at " + fps, Replay(t => new Vector2(Math.Min(t * 2, 0.8f), 0), 5, fps), 1);
        }

        Equal("return does not propel", Replay(t => new Vector2(t < 1.2f ? Stroke(t) : 0, 0), 2, 30), 1);
        Equal("subthreshold movement", Replay(t => new Vector2(0.2f * (float)Math.Sin(t * 5), 0), 3, 30), 0);
        Equal("landmark jump", Replay(t => new Vector2(t < 0.2f ? 0 : 4, 0), 2, 30), 0);

        var arm = new ArmStrokeCycle();
        arm.Step(Vector2.zero, 1f / 30, Settings, out _);
        arm.Reset();
        Equal("reacquisition starts without impulse", arm.Step(new Vector2(2, 2), 1f / 30, Settings, out _) ? 1 : 0, 0);
        Equal("duplicate sample has no impulse", arm.Step(new Vector2(3, 3), 0, Settings, out _) ? 1 : 0, 0);

        Console.WriteLine(checks + " checks passed.");
        return 0;
    }
}
