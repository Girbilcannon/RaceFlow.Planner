using System;
using System.Diagnostics;

namespace RaceFlow.Planner.Telemetry
{
    internal static class ProcessHelpers
    {
        public static bool IsGw2Running()
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        string name = process.ProcessName;

                        if (name.Equals("Gw2-64", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Gw2", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("GuildWars2", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
