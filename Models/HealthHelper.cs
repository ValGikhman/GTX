using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class CpuSampler
{
    private class Sample
    {
        public DateTime Utc;
        public TimeSpan TotalCpu;
    }

    private static readonly ConcurrentDictionary<int, Sample> _last = new();

    // Returns CPU % (0..100). First call returns 0 until we have a previous sample.
    public static double GetCpuPercent(Process p)
    {
        var now = DateTime.UtcNow;
        var totalCpu = p.TotalProcessorTime;

        var prev = _last.AddOrUpdate(
            p.Id,
            _ => new Sample { Utc = now, TotalCpu = totalCpu },
            (_, old) => old
        );

        // First time (or too soon) -> store and return 0
        var elapsedMs = (now - prev.Utc).TotalMilliseconds;
        if (elapsedMs < 250)
        {
            _last[p.Id] = new Sample { Utc = now, TotalCpu = totalCpu };
            return 0;
        }

        var cpuDeltaMs = (totalCpu - prev.TotalCpu).TotalMilliseconds;
        _last[p.Id] = new Sample { Utc = now, TotalCpu = totalCpu };

        // Normalize by cores: 100% == fully using all cores? typically shown as 0..100 overall.
        // Formula gives 0..(100) overall usage.
        var cores = Environment.ProcessorCount;
        var cpuPct = (cpuDeltaMs / (elapsedMs * cores)) * 100.0;

        if (cpuPct < 0) cpuPct = 0;
        if (cpuPct > 100) cpuPct = 100;
        return cpuPct;
    }

    public static class MemoryInfo
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public static (long totalPhys, long availPhys) Get()
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!GlobalMemoryStatusEx(ref ms))
                return (0, 0);

            return ((long)ms.ullTotalPhys, (long)ms.ullAvailPhys);
        }
    }
}
