using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;

namespace BatchFlow.UnitTests
{
    [TestFixture]
    public class Ordering
    {
        [Test]
        public void SortingValues()
        {
            InputPoint<int> entry = new InputPoint<int>();
            TaskNode<int, int> sorter = StandardTasks.GetSortingFilter<int>();
            Collector<int> collect = new Collector<int>();
            sorter.ItemProcessed += new EventHandler<TaskNode.ItemEventArgs>(sorter_ItemProcessed);
            
            Flow f = Flow.FromAsciiArt("a-->b-->c", entry, sorter, collect);
            f.Start();

            entry.Send(3, 7, 1, 9, 123, 2, 5, 3);
            entry.CloseEntrance();
            f.RunToCompletion();
            //Thread.Sleep(1000);
            Console.WriteLine("Last:" + f.GetStateSnapshot());
            Assert.AreEqual(8, collect.Items.Count);
            Assert.AreEqual(1, collect.Items[0]);
            Assert.AreEqual(3, collect.Items[3]);
        }

        void sorter_ItemProcessed(object sender, TaskNode.ItemEventArgs e)
        {
            Console.WriteLine(((TaskNode)sender).OwningFlow.GetStateSnapshot());
        }
    }
}
