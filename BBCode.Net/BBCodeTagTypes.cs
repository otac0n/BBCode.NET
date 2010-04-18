namespace BBCode
{
    using System;

    [Flags]
    public enum BBCodeTagTypes
    {
        Normal = 0x00,
        NewContext = 0x01,
        Literal = 0x02,
    }
}