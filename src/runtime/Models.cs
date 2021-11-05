using System;
using System.Collections.Generic;
using System.Text;

namespace Python.Runtime
{
    public class MetaData
    {
        public int WickHeight { get; set; }
        public int WickWidth { get; set; }
        public int BodyHeight { get; set; }
        public int BodyWidth { get; set; }
    }
    public class MqlRates
    {
        public DateTime Timestamp;
        public float Open;
        public float High;
        public float Low;
        public float Close;
    }
    public class MacdRates
    {
        public DateTime Timestamp;
        public float Open;
        public float High;
        public float Low;
        public float Close;
        public float Macd;
        public int Colour;

        public MetaData MetaData;
        public void SetCandleDimensions(int bodyWidth = 3, float points = 1_000)
        {
            MetaData.BodyWidth = bodyWidth;
            MetaData.WickWidth = bodyWidth / 3;
            MetaData.BodyHeight = (int)((Open > Close ? Open - Close : Close - Open) * points);
            MetaData.WickHeight = (int)((High - Low) * points);
        }
        public int GetRelativeValue(int value, double maxPoints, int panelHeight)
        {
            try
            {
                var valuePercentage = value / maxPoints * 100;

                var results = (int)Math.Floor(valuePercentage / 100 * panelHeight);

                return results == 0 ? 1 : results;
            }
            catch (Exception)
            {
                return 1;
            }
        }
    }
    //ticket time  type magic  identifier reason  volume price_open       sl tp  price_current swap  profit symbol comment
    public class TradePosition
    {
        public int Ticket;
        public DateTime Time;
        public int Type;
        public int Magic;
        public int Identifier;
        public int Reason;
        public float Volume;
        public float PriceOpen;
        public float StopLoss;
        public float TakeProfit;
        public float PriceCurrent;
        public float Swap;
        public float Profit;
        public string Symbol;
        public string Comment;
    }
}
