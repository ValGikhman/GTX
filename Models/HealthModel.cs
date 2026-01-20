using System;

namespace GTX.Models
{
    public class HealthModel: BaseModel
    {
        public DateTimeOffset ServerTime { get; set; }

        // process
        public string ProcessName { get; set; }
        public int Pid { get; set; }
        public long WorkingSetBytes { get; set; }
        public long PrivateBytes { get; set; }
        public long PagedBytes { get; set; }
        public long ManagedBytes { get; set; }

        // machine
        public long? AvailableBytes { get; set; }
        public long? CommittedBytes { get; set; } // committed virtual memory

    }
}