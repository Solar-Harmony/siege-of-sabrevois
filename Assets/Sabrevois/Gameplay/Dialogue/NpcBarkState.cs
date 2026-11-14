namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Coarse mood of an NPC. Derived every tick from persisted anger/fear and health,
    /// then used to pick bark lines and to steer the utility AI.
    /// </summary>
    public enum NpcBarkState
    {
        Calm,
        Alert,
        Angry,
        Wounded,
        Critical,
        Dead
    }

    /// <summary>What the NPC leans towards doing next. Read by the utility AI.</summary>
    public enum NpcBarkIntent
    {
        None,
        Confront,
        Retreat
    }

    /// <summary>How hard a single hit landed. Drives bark intensity and fallback lines.</summary>
    public enum DamageLevel
    {
        Light,
        Moderate,
        Severe,
        Critical
    }
}
