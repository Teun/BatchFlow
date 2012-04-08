using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    public class OutputPoint<T> : TaskNode<T>
    {
        public OutputPoint()
        {
            this.ThreadNumber = 0;
            this.OutQueues = new IWritableQueue[0];
        }
        public override int ThreadNumber
        {
            get
            {
                return base.ThreadNumber;
            }
            set
            {
                if (value != 0) throw new InvalidOperationException("OutputPoint owns no threads");
                base._threadNumber = value;
            }
        }
        public override Type InType
        {
            get
            {
                return typeof(T);
            }
        }

        public IReadableQueue<T> Output
        {
            get
            {
                return (IReadableQueue<T>)this.StreamIn;
            }
        }
        public override void Process(T inValue)
        {
            //noop
        }
    }
}
