using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// The best a run has made of one recipe (2026-08-20 perfect-pour respec): how close it
    /// came, and the shares it was poured at, band for band. The menu prints this under the
    /// boxes — your own record, in your own numbers — which is how a player triangulates
    /// toward a perfect they have never been shown.
    /// </summary>
    public sealed class RecipeBestMake
    {
        /// <summary>Closeness to the recipe's perfect pour, 0–1.</summary>
        public double Accuracy { get; }

        /// <summary>The shares this make was poured at, aligned with the recipe's bands.</summary>
        public IReadOnlyList<double> Shares { get; }

        public RecipeBestMake(double accuracy, IReadOnlyList<double> shares)
        {
            Accuracy = accuracy;
            Shares = shares ?? Array.Empty<double>();
        }
    }
}
