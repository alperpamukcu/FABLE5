namespace LastCall.Core
{
    /// <summary>
    /// Something that can say itself in the player's language (2026-09-13, localization L1). A rule
    /// that refuses throws its English message as it always has — logs and tests read that — and
    /// carries the same sentence as a <see cref="Line"/> for the screen.
    /// </summary>
    public interface IHasLine
    {
        Line Line { get; }
    }
}
