using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class BlockingQueues
    {
        [Test]
        public void OrderedReading()
        {
            BoundedBlockingQueue<int> q = new BoundedBlockingQueue<int>(10);
            q.Send(1);
            q.Send(2);
            q.Send(3);
            int read = q.Receive();
            Assert.AreEqual(1, read);
            read = q.Receive();
            Assert.AreEqual(2, read);
            read = q.Receive();
            Assert.AreEqual(3, read);
        }

        [Test]
        public void ClosedQueue()
        {
            BoundedBlockingQueue<int> q = new BoundedBlockingQueue<int>(10);
            q.Send(1);
            q.CloseEntrance();
            q.Receive();
            try
            {
                q.Receive();
                Assert.Fail("Expected exception");
            }
            catch (BoundedBlockingQueue.ClosedQueueException)
            {
                // noop 
            }

        }
        [Test]
        public void ClosedQueueWithBlockedReader()
        {
            bool test = false;
            BoundedBlockingQueue<int> q = new BoundedBlockingQueue<int>(10);
            Thread t = new Thread(new ParameterizedThreadStart( (object o) => 
            {
                try
                {
                    ((BoundedBlockingQueue)o).ReceiveInner();
                    test = false;
                }
                catch (BoundedBlockingQueue.ClosedQueueException)
                {
                    test = true;
                }
            }
            ));
            t.Start(q);
            Thread.Sleep(100);

            q.CloseEntrance();
            t.Join();
            Assert.IsTrue(test);
        }
        [Test]
        public void Blocking()
        {
            List<int> result = new List<int>();
            BoundedBlockingQueue<int> q = new BoundedBlockingQueue<int>(10);
            Thread t = new Thread(new ThreadStart(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    q.Send(i);
                }
            }
            ));
            Thread t2 = new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    try
                    {
                        result.Add(q.Receive());
                    }
                    catch (BoundedBlockingQueue.ClosedQueueException)
                    {
                        return;
                    }
                }
            }
            ));
            t.Start();
            t2.Start();

            t.Join();
            q.CloseEntrance();

            t2.Join();
            Assert.AreEqual(10000, result.Count);
            Assert.AreEqual(9999, result[9999]);
        }
    }
}
