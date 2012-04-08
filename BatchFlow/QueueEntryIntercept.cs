using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    internal class QueueEntryIntercept<T> : IWritableQueue<T>
    {
        private static Dictionary<IWritableQueue, List<TaskNode>> _writersByQueue = new Dictionary<IWritableQueue, List<TaskNode>>();
        private IWritableQueue _innerQueue;
        private TaskNode _innerTask;
        public QueueEntryIntercept(TaskNode sender, IWritableQueue queue)
        {
            _innerQueue = queue;
            _innerTask = sender;
            lock (_writersByQueue)
            {
                if (!_writersByQueue.ContainsKey(queue))
                {
                    _writersByQueue[queue] = new List<TaskNode>();
                }
                _writersByQueue[queue].Add(sender);
            }
        }

        #region IWritableQueue Members

        public void CloseEntrance() 
        {
            // pas sluiten als alle schrijvers klaar zijn
            lock (_writersByQueue)
            {
                if (_writersByQueue[_innerQueue].Contains(_innerTask))
                {
                    _writersByQueue[_innerQueue].Remove(_innerTask);
                }
                if (_writersByQueue[_innerQueue].Count == 0)
                {
                    _innerQueue.CloseEntrance();
                }
            }
        }
        public void Send(T data)
        {
            SendInner(data);
        }
        public void SendInner(object data)
        {
            _innerTask.TrackStartWaiting();
            _innerQueue.SendInner(data);
            _innerTask.TrackEndWaiting();
        }
        public string Name
        {
            get { return _innerQueue.Name; }
        }

        #endregion
    }
}
