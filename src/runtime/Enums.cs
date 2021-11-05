using System;
using System.Collections.Generic;
using System.Text;

namespace Python.Runtime
{
    public enum Resolution
    {
        M1  = 1,
        M2  = 2,
        M3  = 3,
        M4  = 4,
        M5  = 5,
        M10 = 10,
        M15 = 15,
        M30 = 30,
        H1  = 16385,
        H2  = 16386,
        H3  = 16387,
        H4  = 16388,
    }
    public enum MacdColour
    {
        Neutral = 0,
        LimeGreen = 1,
        Green = 2,
        Red = 3,
        Firebrick = 4
    }
    public enum EmaPeriod
    {
        Fast = 8,
        Slow = 17
    }
}
