using System;

namespace Rokas.Core.ReactiveTurns
{
    public sealed class DamageRequest
    {
        public double Power { get; private set; }
        public double Attack { get; private set; }
        public double Defense { get; private set; }
        public double Element { get; set; } = 1;
        public double Status { get; set; } = 1;
        public double Critical { get; set; } = 1;
        public double Timing { get; set; } = 1;
        public double Broken { get; set; } = 1;
        public double Difficulty { get; set; } = 1;
        public double DefenseOutcomeMultiplier { get; set; } = 1;
        public double DefendMultiplier { get; set; } = 1;

        public DamageRequest(double power, double attack, double defense)
        {
            Power = power;
            Attack = attack;
            Defense = defense;
        }
    }

    public static class DamageResolver
    {
        public static double AttackFromLegacyClickDamage(double clickDamage, int weaponLevel)
        {
            if (!IsFiniteNonnegative(clickDamage) || weaponLevel < 1) throw new ArgumentOutOfRangeException("clickDamage");
            double attack = 4 * clickDamage * (1 + .2 * (weaponLevel - 1));
            if (!IsFiniteNonnegative(attack)) throw new OverflowException("Attack is not finite.");
            return attack;
        }

        public static int Resolve(DamageRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            double[] factors = { request.Power, request.Attack, request.Defense, request.Element,
                request.Status, request.Critical, request.Timing, request.Broken, request.Difficulty,
                request.DefenseOutcomeMultiplier, request.DefendMultiplier };
            foreach (double factor in factors)
                if (!IsFiniteNonnegative(factor)) throw new ArgumentOutOfRangeException("request", "Damage factors must be finite and nonnegative.");
            double raw = request.Power * request.Attack * 100 / (100 + request.Defense)
                * request.Element * request.Status * request.Critical * request.Timing
                * request.Broken * request.Difficulty * request.DefenseOutcomeMultiplier * request.DefendMultiplier;
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw > int.MaxValue - .5)
                throw new OverflowException("Damage exceeds the supported integer range.");
            return (int)Math.Floor(Math.Max(0, raw) + .5);
        }

        private static bool IsFiniteNonnegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        }
    }
}
