using System;
using System.Threading;
using System.Threading.Tasks;

internal static class AppCacheTests
{
    private static int Main()
    {
        var key = "cache-concurrency-test:" + Guid.NewGuid();
        var expected = new object();
        int calls = 0;
        using (var ready = new CountdownEvent(16))
        using (var start = new ManualResetEventSlim())
        {
            var tasks = new Task<object>[16];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(() => {
                    ready.Signal();
                    start.Wait();
                    return AppCache.GetOrCreate(key, () => {
                        Interlocked.Increment(ref calls);
                        Thread.Sleep(200);
                        return expected;
                    });
                }, TaskCreationOptions.LongRunning);
            }
            ready.Wait();
            start.Set();
            Task.WaitAll(tasks);
            if (calls != 1) throw new Exception("Concurrent cache misses ran the factory " + calls + " times.");
            foreach (var task in tasks)
                if (!ReferenceEquals(expected, task.Result)) throw new Exception("Incorrect cached value.");
        }
        AppCache.Remove(key);
        var replacement = new object();
        if (!ReferenceEquals(replacement, AppCache.GetOrCreate(key, () => replacement)))
            throw new Exception("Invalidation did not rebuild the entry.");
        AppCache.Remove(key);
        Console.WriteLine("PASS: Concurrent cold-cache requests share one factory; invalidation rebuilds.");
        return 0;
    }
}
