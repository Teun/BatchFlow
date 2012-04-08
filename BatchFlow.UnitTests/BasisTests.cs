using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class BasisTests
    {
        [Test]
        public void ThreeStepWithFilter()
        {
            List<string> results = new List<string>();
            StartPoint<int> s = Helpers.GetStartpointCounter(1, 100);
            // pass only numbers divisible by three with no 2 in them
            // Output in 
            TaskNode<int, string> filter = Helpers.GetFilter();
            filter.ItemProcessed += new EventHandler<TaskNode.ItemEventArgs>(EndItemProcessed);

            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = Helpers.ConnectStartFilterEnd(s, filter, n);
            
            flow.Start();
            flow.RunToCompletion();

            Assert.Contains(flow.Status , new List<RunStatus>() { RunStatus.Stopped, RunStatus.Stopping});

            Assert.AreEqual(27, results.Count);
            Assert.AreEqual(results.Count, n.ItemsProcessed);
            Assert.AreEqual(1, s.ItemsProcessed);
            Console.WriteLine(flow.GetStateSnapshot());
            Assert.Greater(s.TotalSecondsProcessing, 0);
            Assert.Greater(filter.TotalSecondsProcessing, 0);
            Assert.Greater(filter.TotalSecondsBlocked, 0);

            // Each node works with one thread, so order is maintained, so:
            Assert.AreEqual('3', results[0][0]);

        }

        [Test]
        public void ThreeStepWithInputPoint()
        {
            List<string> results = new List<string>();
            InputPoint<int> s = new InputPoint<int>();
            TaskNode<int, string> filter = Helpers.GetFilter();

            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = Helpers.ConnectStartFilterEnd(s, filter, n);
             
            flow.Start();
            s.Send(1,2,3,4,5,6,7,8);
            s.Send(new int[]{9,10,11,12,13,14,15});
            flow.RunToCompletion();
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(15, filter.ItemsProcessed);
            Assert.AreEqual(RunStatus.Running, n.Status);
            
            s.CloseEntrance();
            flow.RunToCompletion();
            // at this point, the flow and some nodes may still be running or stopping,
            // the data items have left the flow, but the nodes can still be in the process of stopping
            Assert.Contains(n.Status, new List<RunStatus>(){RunStatus.Running , RunStatus.Stopping, RunStatus.Stopped});

            // after a small wait, everything should be in the Stopped status
            Thread.Sleep(100);
            Assert.AreEqual(RunStatus.Stopped, n.Status);
        }
        [Test]
        public void ThreeStepWithInputAndOutputPoints()
        {
            List<string> results = new List<string>();
            InputPoint<int> s = new InputPoint<int>();
            TaskNode<int, string> filter = Helpers.GetFilter();

            OutputPoint<string> outpoint = new OutputPoint<string>();

            Flow flow = Helpers.ConnectStartFilterEnd(s, filter, outpoint);

            flow.Start();
            s.Send(1, 2, 3, 4, 5, 6, 7, 8);
            s.Send(new int[] { 9, 10, 11, 12, 13, 14, 15 });
            string firstResult = outpoint.Output.Receive();
            Assert.AreEqual("3.00", firstResult);

        }
        [Test]
        public void ConsoleLogging()
        {
            StartPoint<int> s = new StartPoint<int>((IWritableQueue<int> outQ) => 
            {
                for (int i = 0; i < 100; i++)
                {
                    Console.WriteLine("Sending {0}", i);
                    outQ.Send(i);

                }
            });

            EndPoint<int> n = new EndPoint<int>((int i)=>Console.WriteLine("Received {0}", i));

            n.ThreadNumber = 4;

            Flow flow = Flow.FromAsciiArt("s-->e", new Dictionary<char, TaskNode>() { 
                {'s', s},
                {'e', n}
            });

            flow.Start();
            flow.RunToCompletion();
        }
        [Test]
        public void EventsAndStatePresentation()
        {
            StartPoint<int> start = StandardTasks.GetRangeEnumerator(1, 100);
            TaskNode<int, string> filter = Helpers.GetFilter();
            filter.ThreadNumber = 2;
            Collector<string> end = new Collector<string>();

            filter.ItemProcessed += new EventHandler<TaskNode.ItemEventArgs>(EndItemProcessed);
            Flow f = Flow.FromAsciiArt("a-->b->z", start, filter, end);
            f.Start();
            f.RunToCompletion();
            // all items have left the flow, but some threads may still be running. The status of the tasks
            // could still be Stopping, or even Running
            Thread.Sleep(10);
            // Now everything should have status Stopped
            Assert.AreEqual(RunStatus.Stopped, f.Status);
            Console.WriteLine("last: {0}\n\n", f.GetStateSnapshot());

        }
        StringBuilder collect = new StringBuilder();
        void EndItemProcessed(object sender, TaskNode.ItemEventArgs e)
        {
        }
    }
}
