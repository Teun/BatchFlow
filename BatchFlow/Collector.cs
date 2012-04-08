using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    public class Collector<T> : EndPoint<T>
    {
        List<T> _innerList = new List<T>();
        public Collector() : base()
        {
            this.Method = (T input) =>
                {
                    lock (_innerList)
                    {
                        _innerList.Add(input);
                    }
                };
        }
        public List<T> Items { get { return _innerList; } }
    }
    public class DistinctCollector<T> : EndPoint<T>
    {
        Dictionary<T, int> _innerCounts = new Dictionary<T, int>();
        public DistinctCollector()
            : base()
        {
            this.Method = (T input) =>
                {
                    lock (_innerCounts)
                    {
                        if (!_innerCounts.ContainsKey(input))
                        {
                            _innerCounts[input] = 0;
                        }
                        _innerCounts[input] += 1;
                    }
                };
        }
        public IDictionary<T, int> Totals { get { return _innerCounts; } }
    }
}
