using System;
using NinjaTrader.Code;
using NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.SafeCore.Services;

namespace NinjaTrader.NinjaScript.AddOns.EssencialChartGuard.NinjaTraderBridge
{
    // ISafeCoreLogger implementation that writes to the NinjaTrader Output Window.
    // Lives in the bridge namespace because it depends on NinjaTrader.Code.Output -- the
    // Safe Core must stay free of NinjaTrader types. Hosts (Indicators / AddOns) build this
    // logger and pass it to Safe Core / bridge classes through the ISafeCoreLogger interface.
    //
    // Each prefix is a stable string so users can filter Output Window Tab 1 by [Essencial*].
    public sealed class OutputTabSafeCoreLogger : ISafeCoreLogger
    {
        private readonly int outputTab;

        public OutputTabSafeCoreLogger() : this(0)
        {
        }

        public OutputTabSafeCoreLogger(int outputTab)
        {
            this.outputTab = outputTab;
        }

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

        private void Write(string line)
        {
            try
            {
                string stamped = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line;
                Output.Process(stamped, outputTab == 1 ? PrintTo.OutputTab2 : PrintTo.OutputTab1);
            }
            catch
            {
                // Output Window can fail during very early/late lifecycle; never propagate.
            }
        }
    }
}
