using System;

namespace Spotlight.Battle
{
    [Serializable]
    public readonly struct BattlePosition : IEquatable<BattlePosition>
    {
        public BattlePosition(BattleLane lane, int column)
        {
            Lane = lane;
            Column = column;
        }

        public BattleLane Lane { get; }
        public int Column { get; }

        public bool Equals(BattlePosition other)
        {
            return Lane == other.Lane && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is BattlePosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Lane, Column);
        }

        public override string ToString()
        {
            return $"{Lane}[{Column}]";
        }

        public static bool operator ==(BattlePosition left, BattlePosition right) => left.Equals(right);
        public static bool operator !=(BattlePosition left, BattlePosition right) => !left.Equals(right);
    }
}
