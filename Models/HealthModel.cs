using System;

public class ProcMetrics
{
    public string Name { get; set; }
    public int Pid { get; set; }
    public double CpuPercent { get; set; }      // 0..100 (all cores combined -> 0..100)
    public long WorkingSetBytes { get; set; }
    public double MemoryPercent { get; set; }   // 0..100 of total physical RAM
}

public class HealthTotals
{
    public double CpuPercentTotal { get; set; }     // overall CPU %
    public long TotalPhysBytes { get; set; }
    public long AvailPhysBytes { get; set; }
    public double MemoryPercentTotal { get; set; }  // overall used physical %
}

public class HealthModel
{
    public string ServerTime { get; set; }
    public ProcMetrics IIS { get; set; }
    public ProcMetrics SqlServer { get; set; }
    public HealthTotals Totals { get; set; }
}
