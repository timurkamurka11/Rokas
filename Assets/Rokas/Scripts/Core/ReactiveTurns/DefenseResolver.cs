using System;

namespace Rokas.Core.ReactiveTurns
{
    public enum DefenseKind { Dodge, Parry }

    public enum DefenseOutcome
    {
        Ignored,
        Miss,
        EarlyFail,
        LateFail,
        WrongDefense,
        Dodge,
        Parry,
        Perfect
    }

    public static class DefenseResolver
    {
        public static DefenseOutcome Classify(DefenseKind kind, long errorUs, DefenseWindowProfile profile)
        {
            return Classify(kind, errorUs, profile, DefenseResponseMask.Dodge | DefenseResponseMask.Parry);
        }

        public static DefenseOutcome Classify(DefenseKind kind, long errorUs, DefenseWindowProfile profile,
            DefenseResponseMask allowedResponses)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (kind != DefenseKind.Dodge && kind != DefenseKind.Parry) throw new ArgumentOutOfRangeException("kind");
            if (errorUs < -profile.AcquireEarlyUs || errorUs > profile.AcquireLateUs)
                return DefenseOutcome.Ignored;

            DefenseResponseMask selected = kind == DefenseKind.Dodge ? DefenseResponseMask.Dodge : DefenseResponseMask.Parry;
            if ((allowedResponses & selected) == 0) return DefenseOutcome.WrongDefense;

            if (kind == DefenseKind.Dodge)
            {
                if (errorUs < -profile.DodgeEarlyUs) return DefenseOutcome.EarlyFail;
                if (errorUs > profile.DodgeLateUs) return DefenseOutcome.LateFail;
                return DefenseOutcome.Dodge;
            }

            if (errorUs < -profile.ParryEarlyUs) return DefenseOutcome.EarlyFail;
            if (errorUs > profile.ParryLateUs) return DefenseOutcome.LateFail;
            if (errorUs >= -profile.PerfectEarlyUs && errorUs <= profile.PerfectLateUs)
                return DefenseOutcome.Perfect;
            return DefenseOutcome.Parry;
        }
    }
}
