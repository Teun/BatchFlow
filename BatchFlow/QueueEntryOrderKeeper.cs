using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BatchFlow
{
    public class QueueEntryOrderKeeper<T> : QueueEntryOrderKeeper, IWritableQueue<T>
    {
        protected static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(QueueEntryOrderKeeper));
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
                    log.DebugFormat("Sending item '{0}' to ordered output stream '{1}'", data, _innerQueue.Name);
                    _innerQueue.SendInner(data);
                }
                else if (workCount > _frontCount)
                {
                    // we cannot send this on right now. We'll store it until all of the lower numbered streas are closed
                    log.DebugFormat("Temporary storing item '{0}' (nr. {2}) because we are waiting for item nr. {1} first", data, workCount, _frontCount);
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

            // If the queued up items list get large, there is probably one task taking relatively long.
            // This is not necessarily a problem, but the collection of queuedUp items can become very 
            // large and use up memory. We don't want to block the entry to this collection, but we will
            // make the fast threads wait a little bit when the size of the collection grows too large.
            // How long we wait and what is too large are arbitrary, but configurable.
            if (queuedUpItems.Count > Properties.Settings.Default.QueuedItemsThreshold)
            {
                Thread.Sleep(Properties.Settings.Default.QueuedItemsOverflowSleepTime);
            }
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
                    log.DebugFormat("Sending item {0} from temporary storage into the stream '{1}'", item, _innerQueue.Name);
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
