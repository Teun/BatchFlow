using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BatchFlow
{
    public class Flow
    {
        protected static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Flow));
        public static int DefaultStreamSize = 10;
        public static ThreadPriority DefaultPriority = ThreadPriority.BelowNormal;
        public Flow()
        {
            this.Status = RunStatus.NotStarted;
        }
        private List<TaskNode> _nodes = new List<TaskNode>();
        internal IEnumerable<TaskNode> Nodes{get{return _nodes;}}

        private List<BoundedBlockingQueue> _streams = new List<BoundedBlockingQueue>();
        internal IEnumerable<BoundedBlockingQueue> Streams { get { return _streams; } }
        public RunStatus Status { get; protected set; }
        public void Start()
        {
            CheckValidity();
            _ready = new Semaphore(1, 1);
            log.Info("Starting Flow");
            foreach (var node in Nodes)
            {
                node.Start();
            }
            this.Status = RunStatus.Stopped;
            log.Info("Flow started");
        }
        public void Stop()
        {
            this.Status = RunStatus.Stopping;
            foreach (var node in Nodes)
            {
                node.Stop();

            }
            lock (_inProcessLock)
            {
                if (_inProcessCounter > 0)
                {
                    _ready.Release();// release for the possible runtocompletion
                    Thread.Sleep(10);
                    _ready.WaitOne();
                }
            }
            this.Status = RunStatus.Stopped;
        }
        public void AddNode(TaskNode newNode)
        {
            AddNode(newNode, Position.Origin);
        }
        /// <summary>
        /// Add a node to the flow. Nodes may only be included once per flow
        /// </summary>
        /// <param name="newNode"></param>
        public void AddNode(TaskNode newNode, Position position)
        {
            _nodes.Add(newNode);
            if(String.IsNullOrEmpty(newNode.Name )) newNode.Name = "node " + _nodes.Count.ToString();
            newNode.OwningFlow = this;
            newNode.Position = position;
        }
        public BoundedBlockingQueue ConnectNodes(TaskNode publisher, TaskNode reader, int streamNumber)
        {
            return ConnectNodes(publisher, reader, streamNumber, DefaultStreamSize);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="publisher"></param>
        /// <param name="reader"></param>
        /// <param name="streamNumber"></param>
        public BoundedBlockingQueue ConnectNodes(TaskNode publisher, TaskNode reader, int streamNumber, int queueSize)
        {
            if(! reader.InType.IsAssignableFrom(publisher.OutTypes[streamNumber]))
            {
                throw new InvalidOperationException(String.Format("The in-stream of the reader ({1}) cannot be assigned from the publisher of type ({0})", publisher.OutTypes[streamNumber].Name, reader.InType.Name));
            }
            Type bbOfT = typeof(BoundedBlockingQueue<>).MakeGenericType(new Type[] { reader.InType });
            BoundedBlockingQueue stream = (BoundedBlockingQueue)Activator.CreateInstance(bbOfT, queueSize);
            publisher.ConnectOutStream(stream, streamNumber, reader.InType);
            reader.ConnectInStream(stream);
            if (String.IsNullOrEmpty(stream.Name)) stream.Name = String.Format("from '{0}' to '{1}'", publisher.Name, reader.Name);
            _streams.Add(stream);
            stream.OwningFlow = this;
            return stream;
        }
        public void ConnectNodeByJoin(TaskNode start, TaskNode end, int outNr)
        {
            if (end.StreamIn == null)
            {
                throw new InvalidOperationException("Use ConnectNodeByJoin only after connecting the main connection");
            }
            start.ConnectOutStream((IWritableQueue)end.StreamIn, outNr, end.InType);
        }
        public TaskNode GetTask(string name)
        {
            return this._nodes.FirstOrDefault(t => t.Name == name);
        }
        public BoundedBlockingQueue GetStream(string toTask)
        {
            return (BoundedBlockingQueue)this.GetTask(toTask).StreamIn;
        }
        internal char[,] Art { get; set; }

        #region ready monitoring
        private Semaphore _ready;
        private int _inProcessCounter = 0;
        private object _inProcessLock = new object();
        internal void IncrementInProcess()
        {
            lock (_inProcessLock)
            {
                _inProcessCounter++;
                if (_inProcessCounter == 1)
                {
                    //we were free, but not anymore
                    _ready.WaitOne();
                }
            }
        }
        internal void DecrementInProcess()
        {
            lock (_inProcessLock)
            {
                _inProcessCounter--;
                if (_inProcessCounter == 0)
                {
                    //we were in process, but not anymore
                    _ready.Release();
                }
            }
        }
        #endregion

        private void CheckValidity()
        {
            if (this.Status != RunStatus.NotStarted)
            {
                throw new InvalidOperationException("You cannot restart a stopped flow");
            }
            //Zijn er stream die niet aan beide kanten vast zitten?
            foreach (BoundedBlockingQueue stream in this.Streams)
            {
                int n = this.Nodes.Where(node => node.StreamIn == stream).Count();
                if (n == 0)
                {
                    throw new InvalidOperationException(String.Format("The stream '{0}' is not connected to any input generator", stream.Name));
                }
                QueueEqualityComparer comp = new QueueEqualityComparer();
                n = (from node in this.Nodes where node.OutQueues.Contains(stream, comp) select node).Count();
                if (n == 0)
                {
                    throw new InvalidOperationException(String.Format("The stream '{0}' is not connected to any output consumer", stream.Name));
                }
            }
            foreach (var node in Nodes)
            {
                Type[] genericParams = node.GetType().GetGenericArguments();
                if (genericParams.Length > 0)
                {
                    if (node.OutQueues.Length == genericParams.Length - 1)
                    {
                        // We use the in stream
                        if (node.StreamIn == null)
                        {
                            throw new InvalidOperationException(String.Format("The node '{0}' needs an in-connection", node.Name));
                        }
                    }
                }
            }
        }

        public void RunToCompletion()
        {
            _ready.WaitOne();
            _ready.Release();
            if (_stoppingException != null)
            {
               
                throw _stoppingException;
            }
        }

        public event EventHandler<ErrorEventArgs> Error;
        public class ErrorEventArgs : EventArgs 
        {
            /// <summary>
            /// This is the task node that threw the exception
            /// </summary>
            public TaskNode Node { get; internal set; }
            /// <summary>
            /// The actual exception thrown, includes stacktrace into the tasks code
            /// </summary>
            public Exception Error { get; internal set; }
            /// <summary>
            /// The data item that the task node was processing when it threw the exception
            /// </summary>
            public object ProcessedItem { get; internal set; }
            /// <summary>
            /// Set this property to true if you want the exception to cause stopping the entire flow
            /// </summary>
            public bool StopProcessing { get; set; }
        }
        private Exception _stoppingException;
        internal void OnError(TaskNode node, Exception exc, object item, ref bool stopProcessing)
        {
            if (Error != null)
            {
                ErrorEventArgs args = new ErrorEventArgs() { Error = exc, ProcessedItem=item, StopProcessing = stopProcessing, Node = node };
                Error(this, args);
                stopProcessing = args.StopProcessing;
            }
            else
            {
                stopProcessing = true;
            }
            if (stopProcessing) _stoppingException = exc;
        }

        public FlowState GetStateSnapshot()
        {
            return new FlowState(this);
        }


        public static Flow FromAsciiArt(string art, IDictionary<char, TaskNode> nodes)
        {
            Flow f = new Flow();
            AsciiArt.ExtractNodesAndConnections(art, nodes, f);

            return f;
        }

        public static Flow FromAsciiArt(string art, params TaskNode[] nodes)
        {
            Dictionary<char, TaskNode> dict = new Dictionary<char, TaskNode>();
            var chars = art.ToCharArray().Where((char c) => c>='a' && c<='z').OrderBy(c => c).ToList<char>();
            if (chars.Count() != nodes.Length)
            {
                throw new ArgumentException("The list of nodes contains the wrong number of items. Ir should match the number of different letters in the ASCII art representation");
            }
            for (int i = 0; i < chars.Count(); i++)
            {
                dict.Add(chars[i], nodes[i]);
            }
            return FromAsciiArt(art, dict);
        }

    }
    public enum RunStatus
    {
        NotStarted,
        Stopped,
        Running,
        Stopping,
        Error
    }

}
