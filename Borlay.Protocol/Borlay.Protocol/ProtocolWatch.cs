using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Borlay.Protocol
{
    public static class ProtocolWatch
    {
        public static ConcurrentDictionary<string, long> Stops { get; } = new ConcurrentDictionary<string, long>();

        public static TimeSpan GetTotal()
        {
            return TimeSpan.FromTicks(Stops.Sum(s => s.Value));
        }

        public static TimeSpan GetTimestamp(string name)
        {
            Stops.TryGetValue(name, out var ticks);
            return TimeSpan.FromTicks(ticks);
        }

        public static Action Start(string name)
        {
            var watch = Stopwatch.StartNew();

            return () =>
            {
                watch.Stop();
                Stops.AddOrUpdate(name, watch.Elapsed.Ticks, (n, ms) => ms + watch.Elapsed.Ticks);
            };
        }
    }
}
