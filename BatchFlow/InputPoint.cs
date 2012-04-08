using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    public class InputPoint<T> : TaskNode
    {
        public InputPoint()
        {
            this.ThreadNumber = 0;
            this.OutQueues = new IWritableQueue[1];
        }
        public override int ThreadNumber
        {
            get
            {
                return base.ThreadNumber;
            }
            set
            {
                if (value != 0) throw new InvalidOperationException("InputPoint owns no threads");
                base._threadNumber = value;
            }
        }
        public override Type[] OutTypes
        {
            get
            {
                return new Type[1] { typeof(T) };
            }
        }
        public override void Do()
        {
            // noop
        }
        public void Send(T value)
        {
            ((IWritableQueue<T>)this.OutQueues[0]).Send(value);
        }
        public void Send(IEnumerable<T> values)
        {
            foreach (var value in values)
            {
                Send(value);
            }
        }
        public void Send(params T[] values)
        {
            foreach (var value in values)
            {
                Send(value);
            }
        }
        public void CloseEntrance()
        {
            ((IWritableQueue<T>)this.OutQueues[0]).CloseEntrance() ;
            this.Status = RunStatus.Stopped;
        }
    }
}
