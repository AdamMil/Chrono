using System;
using System.Drawing;

namespace Chrono
{

public class EmptyEnumerator : System.Collections.IEnumerator
{ public object Current { get { throw new InvalidOperationException(); } }
  public bool MoveNext() { return false; }
  public void Reset() { }
}

public enum Direction
{ Up=0, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft,
  Above, Below, Self, Invalid
};

public sealed class Global
{ private Global() { }

  public static bool Coinflip() { return Random.Next(1)==0; }
  public static Point Move(Point pt, Direction d) { pt.Offset(DirMap[(int)d].X, DirMap[(int)d].Y); return pt; }
  public static int Rand(int max) { return Random.Next(max); }

  public static readonly Point[] DirMap = new Point[8]
  { new Point(0, -1), new Point(1, -1), new Point(1, 0),  new Point(1, 1),
    new Point(0, 1),  new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
  };
  
  static readonly Random Random = new Random();
}

} // namespace Chrono
