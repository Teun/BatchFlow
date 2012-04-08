using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;

namespace BatchFlow
{
    public class BoundedBlockingQueue<T> : BoundedBlockingQueue, IWritableQueue<T>, IReadableQueue<T>
    {
        public BoundedBlockingQueue(int size):base(size)
        {
        }

        public void Send(T data)
        {
            if (data == null) throw new ArgumentNullException("data");
            base.SendInner(data);
        }

        public T Receive()
        {
            return (T)base.ReceiveInner();
        }

    }
    public class BoundedBlockingQueue : IWritableQueue, IReadableQueue, IDisposable
    {
        private Semaphore _itemsAvailable, _spaceAvailable;
        private Queue _queue;
        private object _queueLockObject = new object();
        private bool _isClosed = false;
        public string Name { get; set; }
        public Flow OwningFlow { get; internal set; }
        internal Position InPoint { get; set; }

        public BoundedBlockingQueue(int size)
        {
            _queue = new Queue(size);
            SetSize(size);
        }
        public int Count
        {
            get { return _queue.Count; }
        }

        /// <summary>
        /// To resize the queue. Can only be done on an empty queue, preferably before use.
        /// </summary>
        /// <param name="size">new size</param>
        public void SetSize(int size)
        {
            lock(_queueLockObject)
            {
                if(_queue.Count > 0)
                {
                    throw new InvalidOperationException("Do not set the size of the queue after using it");
                }
                if (size <= 0) throw new ArgumentOutOfRangeException("size");
                _itemsAvailable = new Semaphore(0, size);
                _spaceAvailable = new Semaphore(size, size);
            }
        }

        public void SendInner(object data)
        {
            if (data == null) throw new ArgumentNullException("data");
            _spaceAvailable.WaitOne();
            lock (_queueLockObject)
            {
                if (_isClosed) throw new ClosedQueueException();
                _queue.Enqueue(data);
                // wordt weer losgelaten door de task die deze queue leest
                if(OwningFlow!=null) OwningFlow.IncrementInProcess();
            }
            _itemsAvailable.Release();
        }

        public object ReceiveInner()
        {
            object item;
            if (_queue.Count == 0 && IsClosed)
            {
                throw new ClosedQueueException();
            }
            _itemsAvailable.WaitOne();
            lock (_queueLockObject)
            {
                if (_queue.Count == 0 && IsClosed)
                {
                    _itemsAvailable.Release();
                    throw new ClosedQueueException();
                }
                item = _queue.Dequeue();
            }
            _spaceAvailable.Release();
            return item;
        }
        /// <summary>
        /// Once this has been called, it will be impossible to Send more items. Receiving is still possible, but 
        /// when the queue isempty, receive will not block, but throw ClosedQueueException
        /// </summary>
        public void CloseEntrance()
        {
            if (_isClosed) return;
            lock (_queueLockObject)
            {
                _isClosed = true;
                // Any waiting blocked threads need to get access to find out the queue is closed
                try
                {
                    _itemsAvailable.Release();
                }
                catch (SemaphoreFullException)
                {
                    // apparently not necessary
                }
            }
        }
        public bool IsClosed { get { return _isClosed; } }

        void IDisposable.Dispose()
        {
            if (_itemsAvailable != null)
            {
                _itemsAvailable.Close();
                _spaceAvailable.Close();
                _itemsAvailable = null;
            }
        }
        [Serializable]
        public class ClosedQueueException : Exception { }
    }
    internal class QueueEqualityComparer : IEqualityComparer<IWritableQueue>
    {
        #region IEqualityComparer<IWritableQueue> Members

        public bool Equals(IWritableQueue x, IWritableQueue y)
        {
            return (x.Name == y.Name);
        }

        public int GetHashCode(IWritableQueue obj)
        {
            return obj.Name.GetHashCode();
        }

        #endregion
    }
    public interface IWritableQueue<T> : IWritableQueue
    { void Send(T data);}
    public interface IReadableQueue<T> : IReadableQueue 
    {  T Receive();}
    public interface IWritableQueue
    { void SendInner(object data); string Name{get;} void CloseEntrance();}
    public interface IReadableQueue
    { object ReceiveInner(); string Name{get;}}
}
