using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    public class QueueEntryOrderKeeper<T> : QueueEntryOrderKeeper, IWritableQueue<T>
    {
        private IWritableQueue _innerQueue;
        private TaskNode _innerTask;
        public QueueEntryOrderKeeper(TaskNode owner, IWritableQueue queue)
        {
            _innerQueue = queue;
            _innerTask = owner;
        }

        #region IWritableQueue<T> Members

        public void Send(T data)
        {
            SendInner(data);
        }

        #endregion

        #region IWritableQueue Members
        public void CloseEntrance() { _innerQueue.CloseEntrance(); }
        public void SendInner(object data)
        {
            lock (entrySync)
            {
                int workCount = this._innerTask.GetWorkingOn();
                if (workCount < _frontCount)
                {
                    throw new InvalidOperationException("A value has been sent after the stream was closed (KeepOrder)");
                }
                else if (workCount == _frontCount)
                {
                    _innerQueue.SendInner(data);
                }
                else if (workCount > _frontCount)
                {
                    // we cannot send this on right now. We'll store it until all of the lower numbered streas are closed
                    StoreForLater(workCount, data);
                }
            }

        }

        private void StoreForLater(int workCount, object data)
        {
            if (!queuedUpItems.ContainsKey(workCount))
            {
                queuedUpItems.Add(workCount, new Queue<T>());
            }
            queuedUpItems[workCount].Enqueue((T)data);
        }

        public string Name
        {
            get { return _innerQueue.Name; }
        }


        #endregion
        object entrySync = new object();
        int _frontCount = 0;
        List<int> streamsClosed = new List<int>();
        Dictionary<int, Queue<T>> queuedUpItems = new Dictionary<int, Queue<T>>();
        public override void CloseStream(int value)
        {
            lock (entrySync)
            {
                if (_frontCount == value)
                {
                    // We are moving on 1 stream, no action needed
                    _frontCount++;
                    // other queues might need flushing as well
                    while (FlushQueue(_frontCount))
                    {
                        _frontCount++;
                    }
                }
                else if (_frontCount > value)
                {
                    throw new InvalidOperationException(String.Format("Stream {0} cannot be closed after stream {1} has been closed. Something is going very wrong."));
                }
                else if (_frontCount < value)
                {
                    streamsClosed.Add(value);
                }
            }

        }
        /// <summary>
        /// Flushes the indicated queue to output
        /// </summary>
        /// <param name="value"></param>
        /// <returns>true if the queue was closed, false if it is still open</returns>
        private bool FlushQueue(int value)
        {
            if (queuedUpItems.ContainsKey(value))
            {
                while (queuedUpItems[value].Count > 0)
                {
                    var item = queuedUpItems[value].Dequeue();
                    _innerQueue.SendInner(item);
                }
                queuedUpItems.Remove(value);
            }
            if (streamsClosed.Contains(value))
            {
                streamsClosed.Remove(value);
                return true;
            }
            return false;
        }
    }
    public class QueueEntryOrderKeeper
    {
        public virtual void CloseStream(int value)
        {
        }
    }
}
