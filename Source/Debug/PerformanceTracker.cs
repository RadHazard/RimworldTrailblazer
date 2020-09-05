using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Trailblazer.Debug
{
    /// <summary>
    /// Performance tracker class that's capable of tracking both integer and time-based metrics.
    /// </summary>
    public class PerformanceTracker
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly Dictionary<string, Stopwatch> stopwatches = new Dictionary<string, Stopwatch>();

        /// <summary>
        /// Tracks a maximum value metric.
        /// </summary>
        /// <param name="key">The metric key.</param>
        /// <param name="value">The value to potentially record as maximum.</param>
        public void Max(string key, int value)
        {
            if (!counts.ContainsKey(key) || counts[key] < value)
            {
                counts[key] = value;
            }
        }

        /// <summary>
        /// Tracks a non-time based metric.
        /// </summary>
        /// <param name="key">The metric key.</param>
        /// <param name="count">The amount to add to the metric.</param>
        public void Count(string key, int count = 1)
        {
            if (!counts.ContainsKey(key))
            {
                counts[key] = count;
            }
            else
            {
                counts[key] += count;
            }
        }

        /// <summary>
        /// Starts tracking a new invocation of a time-based metric.
        /// Does not handle recursive invocations.  Make sure to call StopInvocation() at the end.
        /// </summary>
        /// <param name="key">The metric key.</param>
        public void StartInvocation(string key)
        {
            Count(key);
            if (!stopwatches.ContainsKey(key))
            {
                stopwatches[key] = new Stopwatch();
            }
            stopwatches[key].Start();
        }

        /// <summary>
        /// Finishes tracking the current invocation of a time-based metric.
        /// </summary>
        /// <param name="key">Key.</param>
        public void EndInvocation(string key)
        {
            stopwatches[key].Stop();
        }

        public string GetSummary()
        {
            foreach (Stopwatch s in stopwatches.Values)
            {
                s.Stop();
            }

            StringBuilder sb = new StringBuilder("Performance tracker results:").AppendLine();
            sb.AppendLine(string.Format("    Key               Count      Total Time     Instance Time"));
                                      //"  key456789ABCDEF   12345678  123456789ABC ms  123456789ABC ms"
                                      //"  key456789ABCDEF   12345678         n/a              n/a"
            foreach (string key in counts.Keys.OrderBy(k => k))
            {
                if (stopwatches.ContainsKey(key))
                {
                    Stopwatch sw = stopwatches[key];
                    float instanceTime = ((float)sw.ElapsedTicks / counts[key]) * 1000f / Stopwatch.Frequency;
                    sb.AppendLine(string.Format("  {0,-16}  {1,8}  {2,12} ms  {3,12:f5} ms", key, counts[key], sw.ElapsedMilliseconds, instanceTime));
                }
                else
                {
                    sb.AppendLine(string.Format("  {0,-16}  {1,8}         n/a              n/a", key, counts[key]));
                }
            }

            return sb.ToString();
        }
    }
}
