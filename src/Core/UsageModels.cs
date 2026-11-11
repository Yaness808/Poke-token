using System;

namespace TokenPet.Core
{
    internal struct Tokens
    {
        internal long Input, Cached, Output;
        internal long Total { get { return Input + Output; } }

        public static Tokens operator +(Tokens a, Tokens b)
        {
            return new Tokens { Input = a.Input + b.Input, Cached = a.Cached + b.Cached, Output = a.Output + b.Output };
        }

        public static Tokens operator -(Tokens a, Tokens b)
        {
            return new Tokens { Input = a.Input - b.Input, Cached = a.Cached - b.Cached, Output = a.Output - b.Output };
        }

        internal bool IsValid { get { return Input >= Cached && Cached >= 0 && Output >= 0; } }
    }

    internal sealed class Sample
    {
        internal DateTimeOffset Time;
        internal string Key, Model, Tier, Provider;
        internal Tokens Tokens;
    }

    internal sealed class Summary
    {
        internal Tokens Tokens;
        internal Sample Latest;
        internal decimal Dollars, Credits;
        internal long Unpriced;
        internal bool Unavailable;
        internal WeeklyQuota Weekly;
    }

}
