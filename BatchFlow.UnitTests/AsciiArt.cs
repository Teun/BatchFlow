using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class AsciiArt
    {
        [Test]
        public void ThreeStepWithAsciiArt()
        {
            List<string> results = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            TaskNode<int, string> filter = Helpers.GetFilter();
            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = Flow.FromAsciiArt(@"a-->b-->c", s, filter, n);

            flow.Start();
            flow.RunToCompletion();
            Assert.AreEqual(4, results.Count);
        }
        [Test]
        public void ThreeStepWithMultilineAsciiArt()
        {
            List<string> results = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            TaskNode<int, string> filter = Helpers.GetFilter();
            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = Flow.FromAsciiArt(@"
   a
   |
   V 
   b
   |  
   +->c",
                new Dictionary<char, TaskNode>() 
                {
                    {'a', s},
                    {'b', filter},
                    {'c', n}
                }
                );

            flow.Start();
            flow.RunToCompletion();
            Assert.AreEqual(4, results.Count);

            string outputArt = flow.GetStateSnapshot().ToStringAsciiArt();
            Assert.IsFalse(string.IsNullOrEmpty(outputArt));
            Console.WriteLine(outputArt);

        }
        [Test]
        public void ThreeStepWithMultilineAsciiArt2()
        {
            List<string> results = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            TaskNode<int, string> filter = Helpers.GetFilter();
            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = Flow.FromAsciiArt(@"
   +-----+
   |     |
   V     |
   b     a
   |         c
   |         ^
   |         |
   +---#-----+", s, filter, n);

            flow.Start();
            flow.RunToCompletion();
            Assert.AreEqual(4, results.Count);
            Console.WriteLine(flow.GetStateSnapshot().ToStringAsciiArt());
        }
        [Test]
        public void IllegalAsciiArt1()
        {
            List<string> results = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            TaskNode<int, string> filter = Helpers.GetFilter();
            EndPoint<string> n = Helpers.GetEndpoint(results);

            try
            {
                Flow flow = Flow.FromAsciiArt(@"
   a----->b->c
     <--
",
                    new Dictionary<char, TaskNode>() 
                {
                    {'a', s},
                    {'b', filter},
                    {'c', n}
                }
                    );
            }
            catch (InvalidOperationException)
            {
                return;
            }
            Assert.Fail("loose arrows should throw exception");

        }
        [Test]
        public void IllegalAsciiArt2()
        {
            List<string> results = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            TaskNode<int, string> filter = Helpers.GetFilter();
            EndPoint<string> n = Helpers.GetEndpoint(results);

            try
            {
                Flow flow = Flow.FromAsciiArt(@"
   a-+--->b->c
     |
",
                    new Dictionary<char, TaskNode>() 
                {
                    {'a', s},
                    {'b', filter},
                    {'c', n}
                }
                    );
            }
            catch (InvalidOperationException)
            {
                return;
            }
            Assert.Fail("Split connections should throw exception");

        }
        [Test]
        public void LegalAsciiArtParallel()
        {
            List<string> results1 = new List<string>();
            List<string> results2 = new List<string>();

            StartPoint<int> s1 = Helpers.GetStartpointCounter(1, 15);
            StartPoint<int> s2 = Helpers.GetStartpointCounter(5, 35);
            TaskNode<int, string> filter1 = Helpers.GetFilter();
            TaskNode<int, string> filter2 = Helpers.GetFilter();
            EndPoint<string> n1 = Helpers.GetEndpoint(results1);
            EndPoint<string> n2 = Helpers.GetEndpoint(results2);
            n2.ThreadNumber = 3;

            Flow flow = Flow.FromAsciiArt(@"
a-->b->c
d->e--->f
",
                new Dictionary<char, TaskNode>() 
            {
                {'a', s1},
                {'b', filter1},
                {'c', n1},
                {'d', s2},
                {'e', filter2},
                {'f', n2}
            }
                );
            flow.Start();
            flow.RunToCompletion();

            Assert.AreEqual(4, results1.Count);
            Assert.AreEqual(6, results2.Count);

        }
        [Test]
        public void IllegalAsciiArtList()
        {
            List<string> results = new List<string>();

            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            TaskNode<int, string> filter = Helpers.GetFilter();
            EndPoint<string> n = Helpers.GetEndpoint(results);

            try
            {
                Flow flow = Flow.FromAsciiArt(@"a-->b-->c", s, filter);
                Assert.Fail("Flow should raise an exception when the art uses more or less letters than tasks");
            }
            catch (Exception)
            {
                // noop
            }

        }
    }
}
