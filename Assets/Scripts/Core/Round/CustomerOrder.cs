using System;

namespace LastCall.Core
{
    /// <summary>A customer's order: reach <see cref="TargetScore"/> before Mixes run out.</summary>
    public sealed class CustomerOrder
    {
        public string Name { get; }
        public double TargetScore { get; }

        public CustomerOrder(string name, double targetScore)
        {
            if (targetScore <= 0) throw new ArgumentOutOfRangeException(nameof(targetScore));
            Name = string.IsNullOrWhiteSpace(name) ? "Customer" : name;
            TargetScore = targetScore;
        }
    }
}
