using System;
using System.Diagnostics;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services
{
    public interface ISafeCoreLogger
    {
        void Command(string message);
        void Order(string message);
        void Protect(string message);
        void Risk(string message);
        void UI(string message);
    }

    public sealed class TraceSafeCoreLogger : ISafeCoreLogger
    {
        public void Command(string message)
        {
            Write("[EssencialCommand] " + message);
        }

        public void Order(string message)
        {
            Write("[EssencialOrder] " + message);
        }

        public void Protect(string message)
        {
            Write("[EssencialProtect] " + message);
        }

        public void Risk(string message)
        {
            Write("[EssencialRisk] " + message);
        }

        public void UI(string message)
        {
            Write("[EssencialUI] " + message);
        }

        private static void Write(string line)
        {
            try
            {
                Trace.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + line);
            }
            catch
            {
            }
        }
    }
}
