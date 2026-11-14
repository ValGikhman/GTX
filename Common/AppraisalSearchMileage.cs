using System;

namespace GTX.Common {
    public static class AppraisalSearchMileage {
        // Search ceilings start at 100K, then round up in 50K steps; actual odometers stay unchanged.
        public static int? Bucket(int? miles) {
            if (!miles.HasValue || miles < 0 || miles > 2000000) return null;
            return Math.Max(100000, ((miles.Value + 49999) / 50000) * 50000);
        }
    }
}
