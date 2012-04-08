using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.Globalization;

namespace BatchFlow.UnitTests
{
    static class Helpers
    {
        internal static Flow ConnectStartFilterEnd(TaskNode s, TaskNode filter, TaskNode n)
        {
            Flow flow = new Flow();
            flow.AddNode(s);
            flow.AddNode(filter);
            flow.AddNode(n);
            flow.ConnectNodes(s, filter, 0);
            flow.ConnectNodes(filter, n, 0);
            flow.Error += new EventHandler<Flow.ErrorEventArgs>(flow_Error);
            return flow;
        }

        internal static StartPoint<int> GetStartpointCounter(int from, int to)
        {
            StartPoint<int> s = new StartPoint<int>(
                (IWritableQueue<int> o) =>
                {
                    for (int i = from; i <= to; i++)
                    {
                        o.Send(i);
                    }
                }
                );
            return s;
        }
        internal static EndPoint<string> GetEndpoint(List<string> output)
        {
            EndPoint<string> n = new EndPoint<string>(
                (string i) =>
                {
                    output.Add(i);
                    Thread.Sleep(10);
                }
                );
            return n;
        }

        internal static TaskNode<int, string> GetFilter()
        {
            TaskNode<int, string> filter = new TaskNode<int, string>(
                (int i, IWritableQueue<string> word) =>
                {
                    if (i % 3 != 0) return;
                    string formattedNumber = i.ToString("###.00", CultureInfo.InvariantCulture);
                    if (formattedNumber.Contains("2")) return;
                    if (i == 3) Thread.Sleep(100);
                    word.Send(formattedNumber);
                }

                );

            return filter;
        }

        internal static void flow_Error(object sender, Flow.ErrorEventArgs e)
        {
            Assert.Fail("Error was thrown from node (after retries): {0}", e.Error.Message);
        }
    }
}
