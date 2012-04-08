using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    public static class StandardTasks
    {
        public static StartPoint<int> GetRangeEnumerator(int from, int to)
        {
            return new StartPoint<int>((IWritableQueue<int> output) => { for (int i = from; i <= to; i++) { output.Send(i); } });
        }
        /// <summary>
        /// Returns a task that passes on everything it receives, but only once. If the same value is
        /// passed twice, only the first one is passed on.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static TaskNode<T, T> GetUniqueFilter<T>()
        {
            Dictionary<T, bool> alreadySeen = new Dictionary<T, bool>();
            TaskNode<T, T> result = new TaskNode<T, T>(
                (T input, IWritableQueue<T> output)=>
                    {
                        lock (alreadySeen)
                        {
                            if (!alreadySeen.ContainsKey(input))
                            {
                                alreadySeen.Add(input, true);
                                output.Send(input);
                            }
                        }
                    }
                );
            return result;
        }
        public static TaskNode<T, T> GetSortingFilter<T>()
        {
            List<T> collect = new List<T>();
            TaskNode<T, T> result = new TaskNode<T, T>(
                (T input, IWritableQueue<T> output) =>
                {
                    collect.Add(input);
                }
                );
            result.AfterComplete +=
                (IList<IWritableQueue> outputs) =>
                {
                    collect.Sort();
                    foreach (var item in collect)
                    {
                        outputs[0].SendInner(item);
                    }
                };
            return result;
        }
    }
}
