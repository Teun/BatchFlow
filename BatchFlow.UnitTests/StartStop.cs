using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class StartStop
    {
        [SetUp]
        public void Setup()
        {
            log4net.Config.XmlConfigurator.Configure();
        }
        [Test]
        public void StopRightInTheMiddle()
        {
            InputPoint<int> inp = new InputPoint<int>();
            TaskNode<int,int> process = new TaskNode<int, int>(
                    (input, output) => output.Send(input)
                        );
            process.ThreadNumber = 2;
            process.KeepOrder = true;
            Collector<int> coll = new Collector<int>();
            
            Flow flow = Flow.FromAsciiArt("a->b->c",
                inp,
                process,
                coll
                );
            coll.ItemProcessed += (o,a) =>
                {
                    if ((int)a.Item == 0)
                    {
                        flow.Stop();
                    }
                    var state = flow.GetStateSnapshot();
                    Console.WriteLine(state.ToStringAsciiArt());
                };

            flow.Start();
            for (int i = 1; i < 100; i++)
            {
                inp.Send(i);
            }
            inp.Send(0);
            inp.Send(1);
            inp.Send(1);
            inp.Send(1);
            inp.Send(1);
            inp.Send(1);
            inp.Send(1);
            //for (int i = 1; i < 1000; i++)
            //{
            //    inp.Send(i);
            //}
            flow.RunToCompletion();
            Assert.AreEqual(coll.Items.Count,100);
        }
    }
}
