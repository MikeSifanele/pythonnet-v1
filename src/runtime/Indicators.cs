using System;
using System.Collections.Generic;
using System.Text;

namespace Python.Runtime
{
    public static class Macd
    {

        public static float CalculateEMA(float close, float previousEma, int period)
        {
            try
            {
                float alpha = (float)(2.0 / (1.0 + period));

                return previousEma + alpha * (close - previousEma);
            }
            catch (Exception)
            {
                return close;
            }
        }
        public static float CalculateMacd(float fastEMA, float slowEMA)
        {
            try
            {
                return fastEMA - slowEMA;
            }
            catch (Exception)
            {
                return 0f;
            }
        }
        public static int CalculateCandleColour(float prevMacd, float currMacd)
        {
            try
            {
                if (currMacd > 0)
                {
                    if (currMacd > prevMacd)
                        return (int)MacdColour.LimeGreen;
                    if (currMacd < prevMacd)
                        return (int)MacdColour.Green;
                }
                else if (currMacd < 0)
                {
                    if (currMacd < prevMacd)
                        return (int)MacdColour.Red;
                    if (currMacd > prevMacd)
                        return (int)MacdColour.Firebrick;
                }

                return (int)MacdColour.Neutral;
            }
            catch (Exception)
            {
                return (int)MacdColour.Neutral;
            }
        }
    }
}
