using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class Threading
    {
        [Test]
        public void ThreeStepWithFilterMultithreaded()
        {
            List<string> results = new List<string>();
            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            // pass only numbers divisible by three with no 2 in them
            // Output in 
            TaskNode<int, string> filter = Helpers.GetFilter();
            filter.ThreadNumber = 3;

            EndPoint<string> n = Helpers.GetEndpoint(results);
            n.ThreadNumber = 4;

            Flow flow = Helpers.ConnectStartFilterEnd(s, filter, n);

            flow.Start();
            flow.RunToCompletion();
            Assert.AreEqual(4, results.Count);
            Assert.AreNotEqual("3.00", results[0]);

        }
        [Test]
        public void StartingAndStopping()
        {
            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            // pass only numbers divisible by three with no 2 in them
            // Output in 
            TaskNode<int, string> filter = Helpers.GetFilter();
            filter.ThreadNumber = 2;

            Collector<string> collect = new Collector<string>();

            filter.ItemProcessed += new EventHandler<TaskNode.ItemEventArgs>(StopFlowItemProcessed);
            Flow flow = Flow.FromAsciiArt("c->d--->e", s, filter, collect);

            flow.Start();
            // Flow will be stopped from evnt handler
            // Give it time to do it wrong:
            Thread.Sleep(100);
            Assert.Greater(collect.Items.Count, 0);
            Assert.Less(collect.Items.Count, 4);
            flow.RunToCompletion();
            try
            {
                Console.WriteLine(  flow.Status);
                //flow.Start();
                //Assert.Fail("An exception should be thrown when starting a stopped flow");
            }
            catch (InvalidOperationException)
            {
                //noop
            }
        }
        int i = 0;
        void StopFlowItemProcessed(object sender, TaskNode.ItemEventArgs e)
        {
            i++;
            if (i == 3) ((TaskNode)sender).OwningFlow.Stop();
        }
        [Test]
        public void KeepOrder()
        {
            List<string> results = new List<string>();
            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            // pass only numbers divisible by three with no 2 in them
            // Output in 
            TaskNode<int, string> filter = Helpers.GetFilter();
            filter.ThreadNumber = 5;
            filter.KeepOrder = true;

            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = Helpers.ConnectStartFilterEnd(s, filter, n);

            flow.Start();
            flow.RunToCompletion();
            Assert.AreEqual(4, results.Count);
            Assert.AreEqual("3.00", results[0]);
        }
        [Test]
        public void KeepOrder2()
        {
            List<int> results = new List<int>();
            Random r = new Random();
            StartPoint<int> s = Helpers.GetStartpointCounter(1, 50);
            TaskNode<int, int> process = new TaskNode<int, int>(
                (int input, IWritableQueue<int> output) =>
                {
                    int sleeptime = r.Next(50);
                    Thread.Sleep(sleeptime);
                    output.Send(input);
                }
                ) { ThreadNumber = 5, KeepOrder = true };
            Collector<int> n = new Collector<int>();
            Flow f = Flow.FromAsciiArt("c<----b<---0a", new Dictionary<char, TaskNode>() { { 'a', s }, { 'b', process }, { 'c', n } });
            f.Start();
            f.RunToCompletion();
            int vorig = 0;
            foreach (var item in n.Items)
            {
                Assert.AreEqual(vorig + 1, item);
                vorig = item;
            }
        }
		[Test]
		public void KeepOrder3()
		{
			List<int> results = new List<int>();
			Random r = new Random();
			StartPoint<int> s = Helpers.GetStartpointCounter(1, 50);
			TaskNode<int, int, int> process = new TaskNode<int, int, int>(
				(input, output1, output2) =>
				{
					int sleeptime = r.Next(50);
					Thread.Sleep(sleeptime);
					if (input % 2 == 0)
					{
						output1.Send(input);
					}
					else
					{
						output2.Send(input);
					}
				}
				) { ThreadNumber = 2, KeepOrder = true };
			Collector<int> n = new Collector<int>();
			Collector<int> n2 = new Collector<int>();
			Flow f = Flow.FromAsciiArt(@"
a-->b0-->c
    1
    |
    V
    d
", new Dictionary<char, TaskNode>() { { 'a', s }, { 'b', process }, { 'c', n }, {'d', n2} });
			f.Start();
			f.RunToCompletion();
			int vorig = 0;
			foreach (var item in n.Items)
			{
				Assert.AreEqual(vorig + 2, item);
				vorig = item;
			}
			vorig = -1;
			foreach (var item in n2.Items)
			{
				Assert.AreEqual(vorig + 2, item);
				vorig = item;
			}
		}
	}
}
