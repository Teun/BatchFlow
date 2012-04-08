using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    internal static class AsciiArt
    {


        class PositionedTask
        {
            public TaskNode task;
            public Position pos;
        }
        class PositionedConnection
        {
            public PositionedTask endPoint;
            public PositionedTask startPoint;
            public List<Position> track = new List<Position>();
            public Position.Direction tailDirection;
            public static IEnumerable<Position.Direction> GetContainedDirections(Position.Direction dir)
            {
                foreach (var possibleDir in Position.AllDirections)
                {
                    if (possibleDir == Position.Direction.None) continue;
                    if((dir & possibleDir) == possibleDir) yield return possibleDir;
                }
            }
        }

        internal static void ExtractNodesAndConnections(string art, IDictionary<char, TaskNode> nodes, Flow f)
        {
            char[,] artChars = ExtractMatrix(art);
            f.Art = artChars;
            List<PositionedTask> tasks = GetTasks(artChars, nodes);
            List<PositionedConnection> connections = new List<PositionedConnection>();

            foreach (var task  in tasks)
            {
                f.AddNode(task.task, task.pos);
            }
            int totalUsed = 0;
            foreach (var task in tasks)
            {
                PositionedConnection conn = FindConnectionEnd(task, artChars);
                if (conn != null)
                {
                    while (ExpandConnection(conn, artChars)) { };
                    Position startPos = conn.track[conn.track.Count-1];
                    char start = artChars[startPos.x, startPos.y];
                    conn.startPoint = new PositionedTask() { task = nodes[start], pos = startPos };
                    int outNr = GetStreamNumber(conn, artChars);
                    BoundedBlockingQueue stream = f.ConnectNodes(conn.startPoint.task, conn.endPoint.task, outNr);
                    stream.InPoint = conn.track[1];
                    connections.Add(conn);
                    totalUsed += conn.track.Count - 1;
                }
            }
            foreach (Position p in GetJoinPosition(artChars))
            {
                PositionedConnection joinedWith = connections.Where((PositionedConnection c)=> c.track.Contains(p)).Single();
                List<PositionedConnection> conns = FindConnectionJoin(p, artChars);
                foreach (var conn in conns)
                {
                    conn.endPoint = joinedWith.endPoint;
                    while (ExpandConnection(conn, artChars)) { };
                    Position startPos = conn.track[conn.track.Count - 1];
                    char start = artChars[startPos.x, startPos.y];
                    conn.startPoint = new PositionedTask() { task = nodes[start], pos = startPos };
                    int outNr = GetStreamNumber(conn, artChars);
                    f.ConnectNodeByJoin(conn.startPoint.task, conn.endPoint.task, outNr);
                    connections.Add(conn);
                    totalUsed += conn.track.Count - 1; // both the end and begin point are already counted
                }
            }



            // after finding all connections, the total number of +, |, -, digits and letters should sum to the 
            // total tracks + nr of tasks
            int usedSpots = art.ToCharArray().Count(c => { return "1234567890abcdefghijklmnopqrstuvwxyz-+#|<>^V".Contains(c); });
            if (usedSpots != totalUsed + tasks.Count)
            {
                throw new InvalidOperationException("loose ends!");
            }
        }

        private static List<PositionedConnection> FindConnectionJoin(Position p, char[,] artChars)
        {
            List<PositionedConnection> result = new List<PositionedConnection>();
            ExtractConnectionEndFor(p, artChars, Position.Direction.North, 'V', result);
            ExtractConnectionEndFor(p, artChars, Position.Direction.South, '^', result);
            ExtractConnectionEndFor(p, artChars, Position.Direction.West, '>', result);
            ExtractConnectionEndFor(p, artChars, Position.Direction.East, '<', result);
            return result;
        }

        private static IEnumerable<Position> GetJoinPosition(char[,] artChars)
        {
            for (int x = 0; x < artChars.GetLength(0); x++)
            {
                for (int y = 0; y < artChars.GetLength(1); y++)
                {
                    if(artChars[x,y] == '#')
                        yield return new Position(){x = x, y = y};
                }
            }
        }

        private static int GetStreamNumber(PositionedConnection conn, char[,] artChars)
        {
            Position startPoint = conn.track[conn.track.Count-2];
            char c = artChars[startPoint.x, startPoint.y];
            if (c >= '0' && c <= '9')
            {
                return int.Parse(c.ToString());
            }
            return 0;
        }

        private static bool ExpandConnection(PositionedConnection conn, char[,] artChars)
        {
            Position currentPos = conn.track[conn.track.Count - 1];
            List<Position.Direction> dirs = new List<Position.Direction>();
            foreach (var direction in PositionedConnection.GetContainedDirections(conn.tailDirection))
            {
                Position newPos = currentPos + direction;
                if (newPos.IsWithin(artChars))
                {
                    Position.Direction thisDir = CharFits(artChars[newPos.x, newPos.y], direction);
                    if (thisDir != Position.Direction.None)
                    {
                        dirs.Add(direction);
                    }
                }
            }
            if (dirs.Count == 0)
            {
                throw new InvalidOperationException("Connection reaches dead end");
            }
            if (dirs.Count > 1)
            {
                throw new InvalidOperationException("Connection reaches splitting point: not allowed");
            }
            Position newPoint = currentPos + dirs[0];
            conn.track.Add(newPoint);
            conn.tailDirection = CharFits(artChars[newPoint.x, newPoint.y], dirs[0]);
            return !TailAtEnd(artChars[newPoint.x, newPoint.y]);
        }

        private static bool TailAtEnd(char p)
        {
            return (p >= 'a' && p <= 'z');
        }
        private static Position.Direction CharFits(char found, Position.Direction direction)
        {
            if (found == '+' || found == '#')
            {
                switch (direction)
                {
                    case Position.Direction.North:
                        return Position.Direction.East | Position.Direction.West | Position.Direction.North;
                    case Position.Direction.East:
                        return Position.Direction.North | Position.Direction.East | Position.Direction.South;
                    case Position.Direction.South:
                        return Position.Direction.East | Position.Direction.West | Position.Direction.South; 
                    case Position.Direction.West:
                        return Position.Direction.West | Position.Direction.North | Position.Direction.South; 
                    default: return Position.Direction.None;
                }
            }
            if (found == '|')
            {
                switch (direction)
                {
                    case Position.Direction.North:
                        return Position.Direction.North;
                    case Position.Direction.South:
                        return Position.Direction.South;
                    default:
                        return Position.Direction.None;
                }
            }
            if (found == '-')
            {
                switch (direction)
                {
                    case Position.Direction.East:
                        return Position.Direction.East;
                    case Position.Direction.West:
                        return Position.Direction.West;
                    default:
                        return Position.Direction.None;
                }
            }
            if (found >= 'a' && found <= 'z')
            {
                return direction;
            }
            if (found >= '0' && found <= '9')
            {
                return direction;
            }
            return Position.Direction.None;
        }

        

        private static PositionedConnection FindConnectionEnd(PositionedTask task, char[,] artChars)
        {
            List<PositionedConnection> pointers = new List<PositionedConnection>();
            ExtractConnectionEndFor(task.pos, artChars, Position.Direction.North, 'V', pointers);
            ExtractConnectionEndFor(task.pos, artChars, Position.Direction.South, '^', pointers);
            ExtractConnectionEndFor(task.pos, artChars, Position.Direction.West, '>', pointers);
            ExtractConnectionEndFor(task.pos, artChars, Position.Direction.East, '<', pointers);
            if (pointers.Count == 0)
            {
                return null;
            }
            else if (pointers.Count == 1)
            {
                pointers[0].endPoint = task;
                return pointers[0];
            }
            throw new InvalidOperationException(String.Format("Multiple pointers seem to end at task '{0}'.(position {1}) This is illegal. Use the Join (#) to merge two streams.", task.task.Name, artChars[task.pos.x, task.pos.y]));
        }

        private static void ExtractConnectionEndFor(Position pos, char[,] artChars, Position.Direction dir, char pointerSymbol, List<PositionedConnection> pointers)
        {
            int difX = (dir == Position.Direction.East ? 1 : (dir == Position.Direction.West ? -1 : 0));
            int difY = (dir == Position.Direction.South ? 1 : (dir == Position.Direction.North ? -1 : 0));
            pos.x += difX; pos.y += difY;
            if (pos.x < 0 || pos.y < 0 || pos.x >= artChars.GetLength(0) || pos.y >= artChars.GetLength(1))
            {
                return;
            }
            if (artChars[pos.x, pos.y] == pointerSymbol)
            {
                PositionedConnection newConn = new PositionedConnection() { tailDirection = dir };
                newConn.track.Add(pos);
                pointers.Add(newConn);
            }
        }

        private static List<PositionedTask> GetTasks(char[,] artChars, IDictionary<char, TaskNode> nodes)
        {
            List<PositionedTask> result = new List<PositionedTask>();
            for (int x = 0; x < artChars.GetLength(0); x++)
            {
                for (int y = 0; y < artChars.GetLength(1); y++)
                {
                    if (artChars[x, y] >= 'a' && artChars[x, y] <= 'z')
                    {
                        if(nodes.ContainsKey(artChars[x,y]))
                        {
                            result.Add(new PositionedTask(){pos = new Position(){x = x, y=y}, task = nodes[artChars[x,y]]});
                        }else{
                            throw new InvalidOperationException(String.Format("The flow definition contains the letter '{0}'. No task is known for this letter.", artChars[x,y]));
                        }
                    }
                }
            }
            return result;
        }

        private static char[,] ExtractMatrix(string art)
        {
            string[] lines = art.Split(new char[]{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
            int maxLength = lines.Max(line => line.Length);
            char[,] chars = new char[maxLength, lines.Length];
            for (int x = 0; x < maxLength; x++)
            {
                for (int y = 0; y < lines.Length; y++)
                {
                    if (lines[y].Length <= x)
                    {
                        chars[x, y] = ' ';
                    }
                    else
                    {
                        chars[x, y] = lines[y][x];
                    }
                }
            }
            return chars;
        }
    }
}
