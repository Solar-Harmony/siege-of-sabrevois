namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Why an NPC is barking. Drives both the LLM prompt and the local fallback line set.
    /// </summary>
    public enum BarkSituation
    {
        Idle,
        AngryIdle,
        Sight,
        HurtLight,
        HurtModerate,
        HurtHeavy,
        Desperate,
        Severed,
        Death,
        Reaction
    }
}
