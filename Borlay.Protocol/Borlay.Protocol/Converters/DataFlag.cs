
namespace Borlay.Protocol.Converters
{
    public struct DataFlag
    {
        public byte InternalValue { get; private set; }

        public static readonly byte Header = 0;
        public static readonly byte Scope = 1;
        public static readonly byte Action = 2;
        public static readonly byte ParameterHash = 3;
        public static readonly byte ActionHash = 4;
        public static readonly byte Data = 5;
        public static readonly byte Authorization = 6;
        public static readonly byte Roles = 7;
        public static readonly byte ShardKey = 10;
        public static readonly byte Kind = 11;
        public static readonly byte Location = 12;

        public override bool Equals(object obj)
        {
            DataFlag otherObj = (DataFlag)obj;
            return otherObj.InternalValue.Equals(this.InternalValue);
        }

        public override int GetHashCode()
        {
            return InternalValue.GetHashCode();
        }

        public static bool operator >(DataFlag left, DataFlag right)
        {
            return (left.InternalValue > right.InternalValue);
        }

        public static bool operator <(DataFlag left, DataFlag right)
        {
            return (left.InternalValue < right.InternalValue);
        }

        public static bool operator ==(DataFlag left, DataFlag right)
        {
            return (left.InternalValue == right.InternalValue);
        }

        public static bool operator !=(DataFlag left, DataFlag right)
        {
            return (left.InternalValue != right.InternalValue);
        }

        public static implicit operator DataFlag(byte otherType)
        {
            return new DataFlag
            {
                InternalValue = otherType
            };
        }

        public override string ToString()
        {
            return InternalValue.ToString();
        }
    }
}
