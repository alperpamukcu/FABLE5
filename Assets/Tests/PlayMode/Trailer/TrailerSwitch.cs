namespace LastCall.PlayTests.Trailer
{
    /// <summary>
    /// The one switch that lets the trailer shots run (TrailerShots ignores itself otherwise), and what they are
    /// filmed with. Kept in the editor's SessionState so it survives the domain reload into play mode and dies with
    /// the editor session - a shot can never start by itself on a later suite run.
    /// </summary>
    public static class TrailerSwitch
    {
        private const string Key = "LastCall.Trailer.";
        private const string DefaultCast = "clubgirl,heavyset,silkwoman,pastelman,leopard,shaved,guard,driftgirl,salaryman";

#if UNITY_EDITOR
        public static bool On
        {
            get => UnityEditor.SessionState.GetBool(Key + "On", false);
            set => UnityEditor.SessionState.SetBool(Key + "On", value);
        }

        /// <summary>The run's seed: one that deals a good first night (a full counter, a varied crowd).</summary>
        public static string Seed
        {
            get => UnityEditor.SessionState.GetString(Key + "Seed", "MALIBU-TRAILER");
            set => UnityEditor.SessionState.SetString(Key + "Seed", value);
        }

        /// <summary>The language the game is filmed in (a Languages code). English unless a localised cut is wanted.</summary>
        public static string Language
        {
            get => UnityEditor.SessionState.GetString(Key + "Lang", LastCall.Core.Languages.Source);
            set => UnityEditor.SessionState.SetString(Key + "Lang", value);
        }
        /// <summary>The faces the shots deal (TrailerCast). clubgirl first: the best-animated drinker in the cast. A face
        /// that is not shipped (Resources/Patron/<slug>) is simply never dealt.</summary>
        public static string Cast
        {
            get => UnityEditor.SessionState.GetString(Key + "Cast", DefaultCast);
            set => UnityEditor.SessionState.SetString(Key + "Cast", value);
        }
#else
        public static string Cast => DefaultCast;
        public static bool On => false;
        public static string Seed => "MALIBU-TRAILER";
        public static string Language => LastCall.Core.Languages.Source;
#endif
    }
}
