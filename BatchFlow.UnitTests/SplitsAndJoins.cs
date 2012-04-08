using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Globalization;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class SplitsAndJoins
    {
        [Test]
        public void ThreeStepWithMultilineAsciiArt()
        {
            List<string> results1 = new List<string>();
            List<string> results2 = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 50);
            TaskNode<int, string, string> splitter =
                new TaskNode<int, string, string>(
                    (int i, IWritableQueue<string> o1, IWritableQueue<string> o2) =>
                    {
                        if (i % 3 == 0 || i % 4 == 0 || i % 5 == 0)
                        {
                            // divisible by 3, 4 or 5, go to stream 1
                            o1.Send(i.ToString(CultureInfo.InvariantCulture));
                        }else{
                            // if not, send i and i+1 to stream 2
                            o2.Send(i.ToString());
                            o2.Send((i + 1).ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    );
            EndPoint<string> end1 = Helpers.GetEndpoint(results1);
            EndPoint<string> end2 = Helpers.GetEndpoint(results2);

            Flow flow = Flow.FromAsciiArt(@"
s--+
   |
   V
   t0->n
   1
   | 
   +---->m
",
                new Dictionary<char, TaskNode>() 
                {
                    {'s', s},
                    {'t', splitter},
                    {'n', end1},
                    {'m', end2}
                }
                );

            flow.Start();
            flow.RunToCompletion();
            Assert.AreEqual(29, results1.Count);
            Assert.AreEqual("50", results1[28]);
            Assert.AreEqual(42, results2.Count);

        }

        [Test]
        public void JoinedStreams()
        {
            StartPoint<int> s1 = new StartPoint<int>(
                (IWritableQueue<int> o)=>
                {
                    o.Send(1);
                    o.Send(3);
                    o.Send(5);
                    o.Send(7);
                }
                );
            StartPoint<int> s2 = new StartPoint<int>(
                (IWritableQueue<int> o) =>
                {
                    o.Send(11);
                    o.Send(13);
                    o.Send(15);
                    o.Send(17);
                }
                );
            Collector<int> end = new Collector<int>();
            Flow f = Flow.FromAsciiArt(@"
a--+
   V
b--#-->c
", new Dictionary<char, TaskNode>() { {'a', s1}, {'b', s2}, {'c', end} });
            f.Start();
            f.RunToCompletion();
            Assert.AreEqual(8, end.Items.Count);
        }
        [Test]
        public void JoinedStreams2()
        {
            StartPoint<int> r1 = StandardTasks.GetRangeEnumerator(21, 50);
            StartPoint<int> r2 = StandardTasks.GetRangeEnumerator(41, 90);
            StartPoint<int> r3 = StandardTasks.GetRangeEnumerator(-9, 10);
            Collector<int> c = new Collector<int>();
            Flow f = Flow.FromAsciiArt(@"
             a  b  c
             |  |  |
             +->#<-+
                |
                V
                d
 ", new Dictionary<char, TaskNode>() { {'a', r1},{'b', r2},{'c', r3},{'d', c}});
            f.Start();
            f.RunToCompletion();
            Assert.AreEqual(100, c.Items.Count);
        }

    }
}
