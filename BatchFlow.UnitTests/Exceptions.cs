using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class Exceptions
    {
        [Test]
        public void ConnectingWrongTypesThrows()
        {
            List<string> results = new List<string>();
            StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
            EndPoint<string> n = Helpers.GetEndpoint(results);

            Flow flow = new Flow();
            flow.AddNode(s);
            flow.AddNode(n);
            try
            {
                flow.ConnectNodes(s, n, 0);
                Assert.Fail("Connecting nodes of different types should throw invalid operation exception");
            }
            catch (InvalidOperationException)
            { }

        }

        class A { }
        class B : A { }
        [Test]
        public void ConnectingPolymorphicTypes()
        {
            StartPoint<B> s = new StartPoint<B>((IWritableQueue<B> q) => q.Send(new B()));
            EndPoint<A> e = new EndPoint<A>((A q) => { });
            Flow flow = new Flow();
            flow.AddNode(s);
            flow.AddNode(e);
            flow.ConnectNodes(s, e, 0);
        }
        
        [Test]
        public void MultithreadedStartpointThrows()
        {
            List<string> results = new List<string>();
            try
            {
                StartPoint<int> s = Helpers.GetStartpointCounter(1, 15);
                s.ThreadNumber = 2;
                Assert.Fail("Library should throw invalid operstion exception when StartPoint is run multithreaded");
            }
            catch (InvalidOperationException)
            {
                //OK
            }
        }

        [Test]
        public void ExceptionsFromTasks()
        {
            StartPoint<int> start = StandardTasks.GetRangeEnumerator(1, 10);
            int i = 0; 
            EndPoint<int> end = new EndPoint<int>(
                (int input) =>
                {
                    i++;
                    throw new InvalidTimeZoneException();
                }
                );
            end.Retries = 3;
            Flow f = Flow.FromAsciiArt("b<--a", start, end);
            f.Start();
            try
            {
                f.RunToCompletion();
                Assert.Fail("RunToCompletion should throw any stopping exceptions from tasks");
            }
            catch (InvalidTimeZoneException)
            { }
            Console.WriteLine("Exceptions thown : {0}", i);
            Console.WriteLine("Status of flow after error: \n{0}", f.GetStateSnapshot());
            Assert.AreEqual(RunStatus.Error, end.Status);
            Assert.AreEqual(0, end.ItemsProcessed);
        }
        [Test]
        public void HandledExceptionsFromTasks()
        {
            StartPoint<int> start = StandardTasks.GetRangeEnumerator(1, 2);
            int i = 0;
            EndPoint<int> end = new EndPoint<int>(
                (int input) =>
                {
                    i++;
                    throw new InvalidTimeZoneException();
                }
                );
            end.Retries = 3;
            Flow f = Flow.FromAsciiArt("b<--a", start, end);
            f.Error += new EventHandler<Flow.ErrorEventArgs>(f_Error);
            f.Start();
            f.RunToCompletion();
            //Assert.AreEqual(RunStatus.Stopped, end.Status); //check doesn't hold, as the separate tasks may still be shutting down when the last data item leaves the system
            Assert.AreEqual(0, end.ItemsProcessed); // nothing successfully
            Console.WriteLine("Status of flow: \n{0}", f.GetStateSnapshot());
        }

        void f_Error(object sender, Flow.ErrorEventArgs e)
        {
            if (e.Error is InvalidTimeZoneException)
            {
                e.StopProcessing = false; // carry on
            }
        }
    }
}
