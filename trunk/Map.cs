using System;
using System.Drawing;

namespace Chrono
{

public enum TileType : byte
{ Border,

  SolidRock, Wall, ClosedDoor, OpenDoor, RoomFloor, Corridor, UpStairs, DownStairs,
  ShallowWater, DeepWater, Ice, Lava, Pit, Hole,
  
  Trap, Altar
}

public enum Trap : byte { Dart, PoisonDart, Magic, MpDrain, Teleport, Pit }
public enum God : byte { God1, God2, God3 }

public struct Tile
{ [Flags()]
  public enum Flag : byte { None=0, PermaLit=1, Lit=2, Hidden=4, Locked=8 };

  public bool Lit
  { get { return GetFlag(Flag.Lit|Flag.PermaLit); }
    set { SetFlag(Flag.Lit, value); }
  }
  public bool GetFlag(Flag f) { return (Flags&(byte)f)!=0; }
  public void SetFlag(Flag flag, bool on) { if(on) Flags|=(byte)flag; else Flags&=(byte)~flag; }

  public Inventory Items;
  public TileType  Type;
  public Point     Dest;    // destination on prev/next level
  public byte      Subtype; // subtype of tile (ie, type of trap/altar/etc)
  public byte      Flags;
  
  public static Tile Border { get { return new Tile(); } }
}

public sealed class Map
{ public Map(int width, int height)
  { this.width  = width;
    this.height = height;
    map = new Tile[height, width];
  }

  public int Width  { get { return width; } }
  public int Height { get { return height; } }
  public Tile this[int x, int y] { get { return x<0 || y<0 || x>=width || y>=height ? Tile.Border : map[y, x]; } }
  public Tile this[Point pt] { get { return this[pt.X, pt.Y]; } }

  public bool Contains(int x, int y) { return y>=0 && y<height && x>=0 && x<width; }

  public void SetTile(Point pt, TileType type) { map[pt.Y,pt.X].Type = type; }
  public void SetTile(int x, int y, TileType type) { map[y,x].Type = type; }
  public void SetTile(Point pt, Tile tile) { map[pt.Y,pt.X] = tile; }
  public void SetTile(int x, int y, Tile tile) { map[y,x] = tile; }

  public bool GetFlag(Point pt, Tile.Flag flag) { return this[pt.X,pt.Y].GetFlag(flag); }
  public bool GetFlag(int x, int y, Tile.Flag flag) { return this[x,y].GetFlag(flag); }
  public void SetFlag(Point pt, Tile.Flag flag, bool on) { map[pt.Y,pt.X].SetFlag(flag, on); }
  public void SetFlag(int x, int y, Tile.Flag flag, bool on) { map[y,x].SetFlag(flag, on); }

  public bool IsPassable(Point pt) { return IsPassable(this[pt.X,pt.Y].Type); }
  public bool IsPassable(int x, int y) { return IsPassable(this[x,y].Type); }
  public bool IsWall(Point pt) { return IsWall(this[pt.X,pt.Y].Type); }
  public bool IsWall(int x, int y) { return IsWall(this[x,y].Type); }
  public bool IsDoor(Point pt) { return IsDoor(this[pt.X,pt.Y].Type); }
  public bool IsDoor(int x, int y) { return IsDoor(this[x,y].Type); }

  public Point RandomTile(TileType type)
  { int tries = width*height;
    while(tries-->0)
    { int x = Global.Rand.Next(width), y = Global.Rand.Next(height);
      if(map[y, x].Type==type) return new Point(x, y);
    }
    for(int y=0; y<height; y++) for(int x=0; x<width; x++) if(map[y, x].Type==type) return new Point(x, y);
    throw new ArgumentException("No such tile on this map!");
  }

  public static bool IsPassable(TileType type) { return (tileFlag[(int)type]&TileFlag.Passable) != TileFlag.None; }
  public static bool IsWall(TileType type) { return (tileFlag[(int)type]&TileFlag.IsWall) != TileFlag.None; }
  public static bool IsDoor(TileType type) { return (tileFlag[(int)type]&TileFlag.IsDoor) != TileFlag.None; }

  [Flags]
  enum TileFlag : byte { None=0, Passable=1, IsWall=2, IsDoor=4 }
  static readonly TileFlag[] tileFlag = new TileFlag[]
  { TileFlag.None,   // Border
    TileFlag.None,   // SolidRock
    TileFlag.IsWall, // Wall
    TileFlag.IsDoor, // ClosedDoor
    TileFlag.IsDoor|TileFlag.Passable, // OpenDoor
    TileFlag.Passable, TileFlag.Passable, TileFlag.Passable, TileFlag.Passable, // others
    TileFlag.Passable, TileFlag.Passable, TileFlag.Passable, TileFlag.Passable,
    TileFlag.Passable, TileFlag.Passable, TileFlag.Passable, TileFlag.Passable
  };
  
  Tile[,] map;
  int width, height;
}

} // namespace Chrono
