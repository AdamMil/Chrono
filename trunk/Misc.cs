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

  public static string AorAn(string s)
  { char fc = char.ToLower(s[0]);
    if(fc=='a' || fc=='e' || fc=='i' || fc=='o' || fc=='u') return "an";
    else return "a";
  }

  public static bool Coinflip() { return Random.Next(100)<50; }
  public static Point Move(Point pt, Direction d) { return Move(pt, (int)d); }
  public static Point Move(Point pt, int d)
  { if(d<0) d = d%8+8;
    else if(d>7) d = d%8;
    pt.Offset(DirMap[d].X, DirMap[d].Y);
    return pt;
  }
  public static int NdN(int ndice, int nsides) // dice range from 1 to nsides, not 0 to nsides-1
  { int val=0;
    nsides++;
    while(ndice-->0) { val += Random.Next(nsides); }
    return val;
  }
  public static Direction PointToDir(Point off)
  { for(int i=0; i<8; i++) if(DirMap[i]==off) return (Direction)i;
    return Direction.Invalid;
  }
  public static int Rand(int min, int max) { return Random.Next(min, max); }
  public static int Rand(int max) { return Random.Next(max); }

  public static readonly Point[] DirMap = new Point[8]
  { new Point(0, -1), new Point(1, -1), new Point(1, 0),  new Point(1, 1),
    new Point(0, 1),  new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
  };

  static readonly Random Random = new Random();
}

} // namespace Chrono
