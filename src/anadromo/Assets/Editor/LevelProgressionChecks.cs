using System;
using Anadromo.Logic;

public static class LevelProgressionChecks
{
    static int checks;
    static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new InvalidOperationException(label);
    }

    static bool Tick(LevelProgression p, bool outside = false, bool empty = false,
        bool abysm = false, int meals = 0, bool cave = false, bool orcas = false,
        bool timeout = false, float bloop = -19, bool done = false, bool scary = false)
        => p.Tick(outside, empty, abysm, meals, 5, cave, orcas, timeout, bloop, -5, 12, done, scary);

    public static int Run()
    {
        checks = 0;
        var p = new LevelProgression();
        Check(!Tick(p, true, true, true, 99, true, true, true, 99, true), "Cannot advance before start");
        Check(p.Start() && p.Phase == GamePhase.Init, "Start enters Init");
        Check(!p.Start(), "Start is idempotent");
        Check(!Tick(p, empty: true, abysm: true, meals: 99, cave: true, orcas: true), "Stay in initial zone");
        Check(Tick(p, outside: true) && p.Phase == GamePhase.KrillFeeding, "Exit begins normal hunt");
        Check(!Tick(p, abysm: true, meals: 99), "Wait until depletion OR Scary activation");
        Check(Tick(p, empty: true) && p.Phase == GamePhase.AbysmDescent, "Depletion begins Scary hunt");
        Check(!Tick(p, meals: 5), "Five meals outside abysm do not launch orcas");
        Check(!Tick(p, abysm: true, meals: 4), "Four meals inside abysm do not launch orcas");
        Check(Tick(p, abysm: true, meals: 5) && p.Phase == GamePhase.OrcaAscent, "Five meals inside abysm launch orcas");
        Check(!Tick(p, cave: true), "Early cave entry waits for all orcas");
        Check(!Tick(p, orcas: true), "Leaving cave before orcas finish does not launch Bloop");
        Check(Tick(p, cave: true, orcas: true) && p.Phase == GamePhase.BloopAwakening, "Conditions reevaluated while inside cave");
        Check(!Tick(p, bloop: -6), "No tremor below first limit");
        Check(Tick(p, bloop: -5) && p.Phase == GamePhase.Trembling, "First limit starts tremor");
        Check(!Tick(p, bloop: 11), "Continue tremor between limits");
        Check(Tick(p, bloop: 12) && p.Phase == GamePhase.Rockfall, "Second limit begins collapse");
        Check(!Tick(p, bloop: 30), "Collapse is not restarted each frame");
        Check(Tick(p, done: true) && p.Phase == GamePhase.Complete, "Fade completes sequence");
        Check(!Tick(p, true, true, true, 99, true, true, true, 99, true), "Final state is terminal");

        var q = new LevelProgression();
        q.Start(); Tick(q, outside: true); Tick(q, empty: true); Tick(q, abysm: true, meals: 5);
        Check(Tick(q, timeout: true) && q.Phase == GamePhase.BloopAwakening, "Timeout alternative launches Bloop");
        Check(Tick(q, bloop: 20) && q.Phase == GamePhase.Rockfall, "Crossing both thresholds in one frame still collapses");
        var r = new LevelProgression();
        Check(!Tick(r, scary: true), "Scary activation cannot bypass start button");
        r.Start();
        Check(!Tick(r, scary: true), "Scary activation cannot bypass initial exit");
        Tick(r, outside: true);
        Check(Tick(r, empty: false, scary: true) && r.Phase == GamePhase.AbysmDescent,
            "Scary activation unlocks abysm with first krill remaining");
        Check(!Tick(r, abysm: true, meals: 4, scary: true), "Scary activation keeps minimum meal requirement");
        Check(Tick(r, abysm: true, meals: 5, scary: true) && r.Phase == GamePhase.OrcaAscent,
            "Scary route reaches orcas without emptying first group");
        Check(!Tick(r, empty: true, scary: true), "Later depletion does not restart abysm phase");
        return checks;
    }

#if ANADROMO_STANDALONE_CHECKS
    public static int Main()
    {
        try { Console.WriteLine("PASS: " + Run() + " level progression checks."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
#endif
}
