using System;
using System.Collections.Generic;
using System.Text;

namespace Python.Runtime
{
    public static class PyConvert
    {
        public static DateTime ToDateTime(dynamic value)
        {
            // Unix timestamp is seconds past epoch
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((int)PyInt.AsInt(value));
        }
        public static double ToDouble(dynamic value)
        {
            return (double)PyFloat.AsFloat(value);
        }
        public static float ToFloat(dynamic value)
        {
            return (float)PyFloat.AsFloat(value);
        }
        public static int ToInt(dynamic value)
        {
            return (int)PyInt.AsInt(value);
        }
        public static bool ToBool(dynamic value)
        {
            return (int)PyInt.AsInt(value) == 1;
        }
        public static MqlRates ToMqlRates(dynamic value)
        {
            try
            {
                return new MqlRates()
                {
                    Timestamp = PyConvert.ToDateTime(value[0]),
                    Open = PyConvert.ToFloat(value[1]),
                    High = PyConvert.ToFloat(value[2]),
                    Low = PyConvert.ToFloat(value[3]),
                    Close = PyConvert.ToFloat(value[4])
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static MacdRates ToMacdRates(dynamic value)
        {
            try
            {
                return new MacdRates()
                {
                    Timestamp = PyConvert.ToDateTime(value[0]),
                    Open = PyConvert.ToFloat(value[1]),
                    High = PyConvert.ToFloat(value[2]),
                    Low = PyConvert.ToFloat(value[3]),
                    Close = PyConvert.ToFloat(value[4]),
                    Macd = 0,
                    MetaData = new MetaData()
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static TradePosition ToTradePosition(dynamic value)
        {
            try
            {
                return new TradePosition()
                {
                    Ticket = PyConvert.ToInt(value.ticket),
                    Time = PyConvert.ToDateTime(value.time),
                    Profit = PyConvert.ToFloat(value.profit),
                    Symbol = (string)value.symbol,
                    Comment = (string)value.comment,
                    Volume = PyConvert.ToFloat(value.volume),
                    PriceCurrent = PyConvert.ToFloat(value.price_current),
                    PriceOpen = PyConvert.ToFloat(value.price_open),
                    Type = PyConvert.ToInt(value.type),
                    Magic = PyConvert.ToInt(value.magic)
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
    public static class ChartConvert
    {
        public static int ToRelativeValue(int value, double maxPoints, int panelHeight)
        {
            try
            {
                var valuePercentage = value / maxPoints * 100;

                return (int)Math.Floor(valuePercentage / 100 * panelHeight);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
