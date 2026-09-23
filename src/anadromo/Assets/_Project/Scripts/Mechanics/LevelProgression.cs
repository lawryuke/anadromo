namespace Anadromo.Logic
{
    public enum GamePhase
    {
        Init, KrillFeeding, AbysmDescent, OrcaAscent, BloopAwakening,
        WaitingForStart, Trembling, Rockfall, Complete
    }

    // Pure transition rules: independent of Unity callbacks and collider order.
    public sealed class LevelProgression
    {
        public GamePhase Phase { get; private set; } = GamePhase.WaitingForStart;
        public bool Start()
        {
            if (Phase != GamePhase.WaitingForStart) return false;
            Phase = GamePhase.Init;
            return true;
        }

        public bool Tick(bool outsideInitial, bool firstKrillDepleted, bool inAbysm, int meals, int requiredMeals,
            bool inCave, bool allOrcasAbove, bool timeout, float bloopHeight, float firstLimit, float secondLimit, bool endingDone)
        {
            GamePhase next = Phase;
            switch (Phase)
            {
                case GamePhase.Init:
                    if (outsideInitial) next = GamePhase.KrillFeeding;
                    break;
                case GamePhase.KrillFeeding:
                    if (firstKrillDepleted) next = GamePhase.AbysmDescent;
                    break;
                case GamePhase.AbysmDescent:
                    if (inAbysm && meals >= requiredMeals) next = GamePhase.OrcaAscent;
                    break;
                case GamePhase.OrcaAscent:
                    if ((inCave && allOrcasAbove) || timeout) next = GamePhase.BloopAwakening;
                    break;
                case GamePhase.BloopAwakening:
                    if (bloopHeight >= secondLimit) next = GamePhase.Rockfall;
                    else if (bloopHeight >= firstLimit) next = GamePhase.Trembling;
                    break;
                case GamePhase.Trembling:
                    if (bloopHeight >= secondLimit) next = GamePhase.Rockfall;
                    break;
                case GamePhase.Rockfall:
                    if (endingDone) next = GamePhase.Complete;
                    break;
            }
            if (next == Phase) return false;
            Phase = next;
            return true;
        }
    }
}
