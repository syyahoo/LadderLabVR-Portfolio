// 프로젝트 확장용 (실제 PLC Backend 연결)

using System;

namespace LadderLab.PLC
{
    public enum PlcMemoryArea
    {
        Input,
        Output,
        InternalRelay
    }

    public readonly struct PlcAddress : IEquatable<PlcAddress>
    {
        public PlcAddress(PlcMemoryArea area, int index)
        {
            Area = area;
            Index = index;
        }

        public PlcMemoryArea Area { get; }
        public int Index { get; }

        public static PlcAddress Parse(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("PLC address is empty.", nameof(address));

            var normalized = address.Trim().ToUpperInvariant();
            if (normalized.Length < 2 || !int.TryParse(normalized.Substring(1), out var index))
                throw new ArgumentException($"Unsupported PLC address format: {address}", nameof(address));

            var area = normalized[0] switch
            {
                'X' => PlcMemoryArea.Input,
                'Y' => PlcMemoryArea.Output,
                'M' => PlcMemoryArea.InternalRelay,
                _ => throw new ArgumentException($"Unsupported PLC memory area: {address}", nameof(address))
            };

            return new PlcAddress(area, index);
        }

        public bool Equals(PlcAddress other)
        {
            return Area == other.Area && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is PlcAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Area * 397) ^ Index;
        }

        public override string ToString()
        {
            var prefix = Area switch
            {
                PlcMemoryArea.Input => "X",
                PlcMemoryArea.Output => "Y",
                PlcMemoryArea.InternalRelay => "M",
                _ => throw new ArgumentOutOfRangeException()
            };

            return prefix + Index;
        }
    }
}
