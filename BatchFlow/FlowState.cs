using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace BatchFlow
{
    public class FlowState
    {
        internal FlowState(Flow flow)
        {
            this.Tasks = new List<TaskState>();
            this.Art = flow.Art;

            foreach (var task in flow.Nodes)
            {
                TaskState state = new TaskState()
                {
                    ItemsProcessed = task.ItemsProcessed,
                    Name = task.Name,
                    Status = task.Status,
                    TotalSecondsBlocked = task.TotalSecondsBlocked,
                    TotalSecondsProcessing = task.TotalSecondsProcessing,
                    Position = task.Position
                };

                this.Tasks.Add(state);
            }
            this.Streams = new List<StreamState>();
            foreach (var stream in flow.Streams)
        	{
                StreamState state = new StreamState()
                {
                    Name = stream.Name,
                    Closed = stream.IsClosed,
                    Count = stream.Count,
                    InPoint = stream.InPoint
                };
                this.Streams.Add(state);
            }

        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Tasks: status - name, items processed/seconds processing/seconds blocked\n");
            foreach (var task in this.Tasks)
            {
                sb.AppendFormat("{4} - {0}, {1}/{2:0.00}/{3:0.00}\n", task.Name, task.ItemsProcessed, task.TotalSecondsProcessing, task.TotalSecondsBlocked, task.Status);
            }
            sb.AppendFormat("\nStreams: name, items in stream\n");
            foreach (var stream in this.Streams)
            {
                sb.AppendFormat("Stream: {0}, {1} items {2}\n", stream.Name, stream.Count, stream.Closed?"(closed)": "");
            }
            return sb.ToString();
        }
        public string ToStringAsciiArt()
        {
            int cellSize = 5;
            if (this.Art == null || this.Art.Length == 0)
            {
                return "";
            }
            char[,] outputArt = new char[this.Art.GetLength(0)  * cellSize + 6, this.Art.GetLength(1) * cellSize + 6];
            FillExpandedArt(outputArt, this.Art);
            FillTasks(outputArt);
            FillConnectionInfo(outputArt);

            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < outputArt.GetLength(1); y++) 
            {
                for (int x = 0; x < outputArt.GetLength(0); x++)
                {
                    if (outputArt[x, y] == '\0')
                    {
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append(outputArt[x, y]);
                    }
                }
                sb.Append("\r\n");
            }
            
            return sb.ToString();
        }

        private void FillConnectionInfo(char[,] outputArt)
        {
            foreach (var conn in this.Streams)
            {
                int x = conn.InPoint.x * 5 + 1;
                int y = conn.InPoint.y * 5 + 6;
                PasteString(conn.Count.ToString(), outputArt, x, y, 5);
                if(conn.Closed)
                {
                    PasteString("(closed)", outputArt, x, y + 1, 8);
                }
            }
        }

        private void FillTasks(char[,] outputArt)
        {
            foreach (var task in Tasks)
            {
                FillTasks(outputArt, task);
            }
        }

        private void FillTasks(char[,] outputArt, TaskState task)
        {
            int baseX = task.Position.x * 5;
            int baseY = task.Position.y * 5;
            for (int x = 0; x < 11; x++)
            {
                for (int y = 0; y < 11; y++)
                {
                    if (x == 0 || x == 10)
                    {
                        outputArt[baseX + x, baseY + y] = '|';
                    }
                    else if (y == 0 || y == 10)
                    {
                        outputArt[baseX + x, baseY + y] = '-';
                    }
                    else
                    {
                        outputArt[baseX + x, baseY + y] = ' ';
                    }
                }
            }
            outputArt[baseX, baseY] = '+';
            outputArt[baseX + 10, baseY] = '+';
            outputArt[baseX, baseY + 10] = '+';
            outputArt[baseX + 10, baseY + 10] = '+';
            PasteString(task.Name, outputArt, baseX + 1, baseY + 2, 9);
            PasteString(task.ItemsProcessed.ToString(), outputArt, baseX + 1, baseY + 3, 9);
            PasteString(task.TotalSecondsProcessing.ToString("0.00 s"), outputArt, baseX + 1, baseY + 4, 9);
            PasteString(task.TotalSecondsBlocked   .ToString("0.00 s"), outputArt, baseX + 1, baseY + 5, 9);
            PasteString(task.Status.ToString(), outputArt, baseX + 1, baseY + 7, 9);
        }

        private void PasteString(string text, char[,] outputArt, int x, int y, int maxLen)
        {
            if (text.Length > maxLen)
            {
                text = text.Substring(0, maxLen);
            }
            if (maxLen > text.Length) x++;
            for (int i = 0; i < text.Length; i++)
            {
                outputArt[x + i, y] = text[i];
            }
        }

        private void FillExpandedArt(char[,] outputArt, char[,] smallArt)
        {
            for (int x = 0; x < smallArt.GetLength(0); x++)
            {
                for (int y = 0; y < smallArt.GetLength(1); y++)
                {
                    PlotEnlargedChar(outputArt, smallArt[x, y], x, y);
                }
            }
        }

        private void PlotEnlargedChar(char[,] outputArt, char c, int px, int py)
        {
            int startPointX = 3 + px * 5;
            int startPointY = 3 + py * 5;
            char[,] largeChar = GetLargeChar(c);
            for (int x = 0; x < largeChar.GetLength(0); x++)
            {
                for (int y = 0; y < largeChar.GetLength(1); y++)
                {
                    outputArt[startPointX + x, startPointY + y] = largeChar[x, y];
                }
            }
        }

        private char[,] GetLargeChar(char c)
        {
            char[,] result = new char[5, 5];
            string s = "";
            switch (c)
            {
                case '+':
                case '#':
                    s = @"`  |  :
                          `  |  :
                          `--+--:
                          `  |  :
                          `  |  :";
                    break;
                case 'V':
                    s = @"` \|/ :
                          `  |  :
                          `  |  :
                          `  |  :
                          `  |  :";
                    break;
                case '^':
                    s = @"`  |  :
                          `  |  :
                          `  |  :
                          `  |  :
                          ` /|\ :";
                    break;
                case '-':
                    s = @"`     :
                          `     :
                          `-----:
                          `     :
                          `     :";
                    break;
                case '<':
                    s = @"`     :
                          `    / :
                          `-----:
                          `    \:
                          `     :";
                    break;
                case '>':
                    s = @"`     :
                          `\    :
                          `-----:
                          `/    :
                          `     :";
                    break;
                case '|':
                    s = @"`  |  :
                          `  |  :
                          `  |  :
                          `  |  :
                          `  |  :";
                    break;
                default:
                    break;
            }
            if (c == '-')
            {
            }
            int marker = s.IndexOf('`');
            int line = 0;
            while (marker > -1)
            {
                for (int i = 0; i < 5; i++)
                {
                    result[i, line] = s[marker + i + 1];
                }
                marker = s.IndexOf('`', marker+1);
                line++;
            }
            return result;
        }
        
        public IList<TaskState> Tasks { get; private set; }
        public IList<StreamState> Streams { get; private set; }
        public char[,] Art { get; private set; }

        public class TaskState
        {
            public string Name { get; internal set; }
            public RunStatus Status { get; internal set; }
            public long ItemsProcessed { get; internal set; }
            public double TotalSecondsBlocked { get; internal set; }
            public double TotalSecondsProcessing { get; internal set; }
            public Position Position { get; internal set; }
        }
        public class StreamState
        {
            public string Name;
            public int Count;
            public bool Closed;
            public Position InPoint;
        }
    }
}
