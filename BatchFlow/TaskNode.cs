using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

namespace BatchFlow
{
    #region TaskNode base class
    public abstract class TaskNode
    {
        protected static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(TaskNode));

        [DllImport("Kernel32.dll")]
        private static extern void QueryPerformanceCounter(ref long ticks);
        [DllImport("Kernel32.dll")]
        private static extern void QueryPerformanceFrequency(ref long ticks);

        private static double _TickLength;
        static TaskNode()
        {
            long freq = 0;
            QueryPerformanceFrequency(ref freq);
            _TickLength = 1 / (double)freq;

            // Log any exceptions inside a retry loop
            Retry.RetryDelegateFailed += (exc,loop) =>
                                             {
                                                 log.InfoFormat("Exception occurred during attempt {0}: {1}", loop, exc);
                                             };

        }
        public TaskNode()
        {
            Retries = 0;
        }
        // list of out streams
        // connect
        public string Name { get; set; }
        public int Retries { get; set; }
        internal int _threadNumber = 1;
        public virtual int ThreadNumber
        {
            get { return _threadNumber; }
            set
            {
                if (value < 1) throw new InvalidOperationException("Impossible to run less than 1 thread");
                _threadNumber = value;
            }
        }
        public IWritableQueue[] OutQueues { get; internal set; }
        internal void ConnectOutStream(IWritableQueue outQueue, int position, Type t)
        {
            IWritableQueue intercept = GetQueueEntryIntercept(outQueue, t);
            this.OutQueues[position] = intercept;
        }

        protected virtual IWritableQueue GetQueueEntryIntercept(IWritableQueue outQueue, Type t)
        {
            Type ofT = typeof(QueueEntryIntercept<>).MakeGenericType(new Type[] { t });
            IWritableQueue finalWritableQueue = (IWritableQueue)Activator.CreateInstance(ofT, this, outQueue);
            return finalWritableQueue;
        }
        public void ConnectOutStream<T>(IWritableQueue outQueue, int position)
        {
            ConnectOutStream(outQueue, position, typeof(T));
        }
        internal List<Thread> _threads = new List<Thread>();
        public void Start()
        {
            log.InfoFormat("Starting task '{0}'", this.Name);
            for (int i = 0; i < _threadNumber; i++)
            {
                Thread newThread = new Thread(Worker);
                newThread.Priority = Flow.DefaultPriority;
                newThread.IsBackground = true;
                newThread.Name = this.Name + " - " + i.ToString();
                _threads.Add(newThread);
            }
            Status = RunStatus.Running;
            lock (_threads)
            {
                foreach (var thread in _threads)
                {
                    thread.Start();
                    // prevent all threads from attacking at the same time
                    Thread.Sleep(50);
                }
            }
        }
        public void Stop()
        {
            if(Status == RunStatus.Running) Status = RunStatus.Stopping;
            Thread.Sleep(200);
            foreach (var t in _threads)
            {
                if (t.ThreadState != ThreadState.WaitSleepJoin && t.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
                {
                    t.Abort();
                }
            }
            // Clear instream
            BoundedBlockingQueue queueIn = this.StreamIn as BoundedBlockingQueue;
            if (queueIn != null)
            {
                queueIn.CloseEntrance();
                while (queueIn.Count > 0) queueIn.ReceiveInner();
            }
            // this.StreamIn. close entrance
            // clear all items in it
            if (Status == RunStatus.Stopping) this.Status = RunStatus.Stopped;
            this._threads.Clear();
            //Thread.CurrentThread.Abort();
            log.InfoFormat("Task '{0}' forcibly stopped", this.Name);
        }
        public virtual void Worker()
        {
            if (AfterComplete != null)
            {
                // We increase this for eacht thread, indicating that work needs to be 
                // done still after processing the items
                OwningFlow.IncrementInProcess();
                // Remove this after LastDo
            }

            while (Status == RunStatus.Running)
            {
                try
                {
                    Do();
                }
                catch (ThreadAbortException)
                { /*noop, happens when stopping a flow*/ }
                catch (Exception e)
                {
                    bool stop = true;
                    log.WarnFormat("Exception caught in '{0}'", this.Name);
                    this.OwningFlow.OnError(this, e, e.Data["processedItem"], ref stop);
                    if (stop)
                    {
                        this.Status = RunStatus.Error;
                        this.OwningFlow.Stop();
                        log.Error("Exception is fatal to flow", e);
                    }
                }

            }
            log.DebugFormat("Leaving loop ({0})", this.Name);
            lock (_threads)
            {
                if (_threads.Contains(Thread.CurrentThread))
                {
                    _threads.Remove(Thread.CurrentThread);
                }
                // last one turn off the light, please
                if (_threads.Count == 0)
                {
                    // Last chance for the task to do anything
                    LastDo();
                    if (this.Status == RunStatus.Running || this.Status == RunStatus.Stopping)
                    {
                        this.Status = RunStatus.Stopped;
                    }
                    foreach (var stream in this.OutQueues)
                    {
                        stream.CloseEntrance(); 
                    }
                }
            }
            if (AfterComplete != null)
            {
                OwningFlow.DecrementInProcess();
            }
            log.InfoFormat("Task '{0}' done", this.Name);
        }

        public abstract void Do();

        public event Action<IList<IWritableQueue>> AfterComplete;

        protected virtual void LastDo()
        {
            if (AfterComplete != null)
            {
                AfterComplete(OutQueues);
            }
        }

        public IReadableQueue StreamIn { get; internal set; }
        public void ConnectInStream(IReadableQueue queue)
        {
            StreamIn = queue;
        }

        public Flow OwningFlow { get; internal set; }
        public virtual Type InType { get { return null; } }
        public virtual Type[] OutTypes { get { return new Type[0]; } }
        public Position Position { get; internal set; }
        public RunStatus Status { get; internal set; }

        #region stat tracking

        private long _ticksProcessing;
        private long _ticksWaiting;
        private long _itemsProcessed;
        public long ItemsProcessed
        {
            get { return _itemsProcessed; }
        }
        public double TotalSecondsProcessing
        {
            get
            {
                long extraTime = 0;
                // multiple threads may be at work. We count all their time together
                // first we copy the values into a local list to prevent the enumeration 
                // to be broken by new modifications
                List<long> allStarts = new List<long>(_processingStarts.Values);
                if (allStarts.Any((v) => v > 0))
                {
                    long now =0; QueryPerformanceCounter(ref now);
                    foreach (long start in allStarts.Where((v)=>v>0))
                    {
                        extraTime += now - start;
                    }
                }
                return (_ticksProcessing + extraTime - _ticksWaiting)* _TickLength;
            }
        }
        public double TotalSecondsBlocked
        {
            get
            {
                return (_ticksWaiting * _TickLength);
            }
        }


        private Dictionary<int, long> _processingStarts = new Dictionary<int, long>();
        private long CurrentProcessing
        {
            get 
            {
                int t = Thread.CurrentThread.ManagedThreadId;
				lock (_processingStarts)
				{
					if (!_processingStarts.ContainsKey(t)) return 0;
					return _processingStarts[t];
				}
            }
            set 
            {
                int t = Thread.CurrentThread.ManagedThreadId;
				lock (_processingStarts)
				{
					_processingStarts[t] = value;
				}
            }
        }
        private Dictionary<int, long> _waitingStarts = new Dictionary<int, long>();
        private long CurrentWaiting
        {
            get
            {
                int t = Thread.CurrentThread.ManagedThreadId;
				lock (_waitingStarts)
				{
					if (!_waitingStarts.ContainsKey(t)) return 0;
					return _waitingStarts[t];
				}
            }
            set
            {
                int t = Thread.CurrentThread.ManagedThreadId;
				lock (_waitingStarts)
				{
					_waitingStarts[t] = value;
				}
            }
        }
        protected internal void TrackStartProcessing()
        {
            long now = 0;
            QueryPerformanceCounter(ref now);
            CurrentProcessing = now;
        }
        protected internal void TrackEndProcessing()
        {
            long endTime = 0;
            QueryPerformanceCounter(ref endTime);
            _ticksProcessing += endTime - CurrentProcessing;
            _itemsProcessed++;
            CurrentProcessing = 0;
        }
        protected internal void TrackStartWaiting()
        {
            long now = 0;
            QueryPerformanceCounter(ref now);
            CurrentWaiting = now;
        }
        protected internal void TrackEndWaiting()
        {
            long endTime = 0;
            QueryPerformanceCounter(ref endTime);
            _ticksWaiting += endTime - CurrentWaiting;
        }

        #endregion

        #region thread info
        private Dictionary<int, int> _workingOn = new Dictionary<int, int>();
        public int GetWorkingOn()
        {
            int value;
            lock(_workingOn)
            {
                if (!_workingOn.ContainsKey(Thread.CurrentThread.ManagedThreadId)) return 0;
                value = _workingOn[Thread.CurrentThread.ManagedThreadId];
            }
            log.DebugFormat("Getting WonkingOn value: thread ID {0}, value {1}", Thread.CurrentThread.ManagedThreadId, value);
            return value;
        }
        public void SetWorkingOn(int itemNr)
        {
            log.DebugFormat("Setting WonkingOn value: thread ID {0}, value {1}", Thread.CurrentThread.ManagedThreadId, itemNr);
            lock (_workingOn)
            {
                _workingOn[Thread.CurrentThread.ManagedThreadId] = itemNr;
            }
        }
        #endregion

        public class ItemEventArgs : EventArgs { public object Item { get; set; } }
        public event EventHandler<ItemEventArgs> ItemProcessed;
        private static object _eventSerializer = new object();
        protected void RaiseItemProcessed(object inValue)
        {
            if (ItemProcessed != null)
            {
                lock (_eventSerializer)
                {
                    ItemProcessed(this, new ItemEventArgs() { Item = inValue });
                }
            }
            log.DebugFormat("Item processed by node '{0}': {1}", this.Name, inValue);
        }

    }
    public abstract class TaskNode<Tin> : TaskNode
    {
        public bool KeepOrder { get; set; }
        int _orderCounter = 0;
        object _orderLock = new object();
        public override void Do()
        {
            bool haveValue = false;
            Tin inValue = default(Tin);
            try
            {
                int order = 0;
                try
                {
                    lock (_orderLock)
                    {
                        if (KeepOrder)
                        {
                            order = _orderCounter;
                            _orderCounter++;
                            this.SetWorkingOn(order);
                            log.DebugFormat("Using KeepOrder: assigned order number {0} to value {1}", order, inValue);
                        }
                        inValue = ((IReadableQueue<Tin>)StreamIn).Receive();
                        haveValue = true;
                    }
                }
                catch (BoundedBlockingQueue.ClosedQueueException)
                {
                    inValue = default(Tin);
                    haveValue = false;
                }
                if (!haveValue)
                {
                    this.Status = RunStatus.Stopping;
                    return;
                }
                log.DebugFormat("Received value {0} from inqueue", inValue);
                TrackStartProcessing();

                // this is the actual work
                Retry.Times(delegate { Process(inValue); }, this.Retries, Properties.Settings.Default.RetryWaitMillis, true);

                if (KeepOrder)
                {
					foreach (var keeper in _orderKeepers)
					{
						keeper.CloseStream(order);
					}
                }
                TrackEndProcessing();
                RaiseItemProcessed(inValue);
            }
            catch (Exception e)
            {
                if (haveValue)
                {
                    e.Data["processedItem"] = inValue;
                }
                throw;
            }
            finally
            {
                if (haveValue)
                {
                    // We add an item every time we put something in a queue
                    // Here we subtract one, as we have taken the item from a queue and processed it
                    // If it was written to an output queue, it will have been counted now, so we can 
                    // safely subtract.
                    OwningFlow.DecrementInProcess();
                }
            }
        }

        public abstract void Process(Tin inValue);
        public override Type InType
        {
            get
            {
                return typeof(Tin);
            }
        }
        IList<QueueEntryOrderKeeper> _orderKeepers = new List<QueueEntryOrderKeeper>();
        protected override IWritableQueue GetQueueEntryIntercept(IWritableQueue outQueue, Type t)
        {
            IWritableQueue result = base.GetQueueEntryIntercept(outQueue, t);
            if (this.KeepOrder)
            {
                Type KeepOrderOfT = typeof(QueueEntryOrderKeeper<>).MakeGenericType(new Type[] { t });
                result = (IWritableQueue)Activator.CreateInstance(KeepOrderOfT, (TaskNode)this, result);
                _orderKeepers.Add((QueueEntryOrderKeeper)result);
            }
            return result;
        }

    }
    #endregion

    #region start & end points
    public class EndPoint<Tin> : TaskNode<Tin>
    {
        protected EndPoint()
        {
            OutQueues = new IWritableQueue[0];
        }
        public EndPoint(Action<Tin> dlg):this()
        {
            Method = dlg;
        }
        internal Action<Tin> Method { get; set; }
        public override void Process(Tin inValue)
        {
            Method(inValue);
        }
    }
    public class StartPoint<Tout> : TaskNode
    {
        public StartPoint(Action<IWritableQueue<Tout>> dlg)
        {
            Method = dlg;
            OutQueues = new IWritableQueue[1];
        }
        internal Action<IWritableQueue<Tout>> Method { get; set; }
        public override void Do()
        {
            OwningFlow.IncrementInProcess();
            TrackStartProcessing();
            Retry.Times(() => Method((IWritableQueue<Tout>)OutQueues[0]), this.Retries, Properties.Settings.Default.RetryWaitMillis, true);
            TrackEndProcessing();
            OwningFlow.DecrementInProcess();
            this.Status = RunStatus.Stopping;
        }
        public override int ThreadNumber
        {
            get
            {
                return base.ThreadNumber;
            }
            set
            {
                if (value != 1)
                {
                    throw new InvalidOperationException("StartPoint<> does not support multiple threads");
                }
                base.ThreadNumber = value;
            }
        }
        public override Type[] OutTypes
        {
            get
            {
                return new Type[1] { typeof(Tout) };
            }
        }
    }

    #endregion

    #region processing task nodes
    public class TaskNode<Tin, Tout, Tout2> : TaskNode<Tin>
    {
        public TaskNode(Action<Tin, IWritableQueue<Tout>, IWritableQueue<Tout2>> dlg)
        {
            Method = dlg;
            OutQueues = new IWritableQueue[2];
        }
        internal Action<Tin, IWritableQueue<Tout>, IWritableQueue<Tout2>> Method { get; set; }
        public override void Process(Tin inValue)
        {
            Method(inValue, (IWritableQueue<Tout>)OutQueues[0], (IWritableQueue<Tout2>)OutQueues[1]);
        }
        public override Type[] OutTypes
        {
            get
            {
                return new Type[] { typeof(Tout), typeof(Tout2) };
            }
        }

    }
    public class TaskNode<Tin, Tout> : TaskNode<Tin>
    {
        public TaskNode(Action<Tin, IWritableQueue<Tout>> dlg)
        {
            Method = dlg;
            OutQueues = new IWritableQueue[1];
        }
        internal Action<Tin, IWritableQueue<Tout>> Method { get; set; }
        public override void Process(Tin inValue)
        {
            Method(inValue, (IWritableQueue<Tout>)OutQueues[0]);
        }
        public override Type[] OutTypes
        {
            get
            {
                return new Type[] { typeof(Tout) };
            }
        }

    }
    #endregion

}
