using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchFlow
{
    public struct Position
    {
        public int x; public int y;
        public static Position operator +(Position p, Position.Direction dir)
        {
            Position result = p;
            if (dir == Position.Direction.North) result.y--;
            if (dir == Position.Direction.South) result.y++;
            if (dir == Position.Direction.West) result.x--;
            if (dir == Position.Direction.East) result.x++;
            return result;
        }
        public bool IsWithin(char[,] matrix)
        {
            if (this.x < 0 || this.y < 0 || this.x >= matrix.GetLength(0) || this.y >= matrix.GetLength(1))
            {
                return false;
            }
            return true;
        }
        public override bool Equals(object obj)
        {
            Position other = (Position)obj;
            return (other.x == this.x && other.y == this.y);
        }
        public override int GetHashCode()
        {
            return this.x.GetHashCode() + this.y.GetHashCode();
        }
        public static Position Origin
        {
            get { return new Position() { x = 0, y = 0 }; }
        }
        [Flags]
        public enum Direction
        {
            None = 0, North = 1, East = 2, South = 4, West = 8
        }
            public static IEnumerable<Position.Direction> AllDirections
            {
                get
                {
                    return new Direction[] { Direction.North, Direction.East, Direction.South, Direction.West };
                }
            }
    }
}
