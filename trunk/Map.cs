using System;
using System.Collections;
using System.Drawing;
using GameLib.Collections;

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

  public ItemPile  Items;
  public Creature  Creature; // for memory only
  public TileType  Type;
  public Point     Dest;     // destination on prev/next level
  public byte      Subtype;  // subtype of tile (ie, type of trap/altar/etc)
  public byte      Flags;
  
  public static Tile Border { get { return new Tile(); } }
}

public sealed class Map
{ 
  #region CreatureCollection
  public class CreatureCollection : ArrayList
  { public CreatureCollection(Map map) { this.map = map; }
  
    public new Creature this[int index] { get { return (Creature)base[index]; } }

    public new void Add(object o) { Add((Creature)o); }
    public void Add(Creature c)
    { base.Add(c);
      map.Added(c);
    }
    public new void AddRange(ICollection creatures)
    { base.AddRange(creatures);
      foreach(Creature c in creatures) map.Added(c);
    }
    public new void Insert(int index, object o) { Insert(index, (Creature)o); }
    public void Insert(int index, Creature c)
    { base.Insert(index, c);
      map.Added(c);
    }
    public void InsertRange(ICollection creatures, int index)
    { base.InsertRange(index, creatures);
      foreach(Creature c in creatures) map.Added(c);
    }
    public new void Remove(object o) { Remove((Creature)o); }
    public void Remove(Creature c)
    { base.Remove(c);
      map.Removed(c);
    }
    public new void RemoveAt(int index)
    { Creature c = this[index];
      base.RemoveAt(index);
      map.Removed(c);
    }
    public new void RemoveRange(int index, int count)
    { if(index<0 || index>=Count || count<0 || index+count>Count)
        throw new ArgumentOutOfRangeException();
      for(int i=0; i<count; i++) map.Removed(this[index+i]);
      base.RemoveRange(index, count);
    }

    protected Map map;
  }
  #endregion

  public Map(int width, int height)
  { this.width  = width;
    this.height = height;
    map = new Tile[height, width];
    creatures = new CreatureCollection(this);
  }

  public int Width  { get { return width; } }
  public int Height { get { return height; } }
  public Tile this[int x, int y]
  { get { return x<0 || y<0 || x>=width || y>=height ? Tile.Border : map[y, x]; }
    set { map[y, x] = value; }
  }
  public Tile this[Point pt]
  { get { return this[pt.X, pt.Y]; }
    set { map[pt.Y, pt.X] = value; }
  }

  public void AddItem(Point pt, Item item) { AddItem(pt.X, pt.Y, item); }
  public void AddItem(int x, int y, Item item)
  { ItemPile inv = map[y,x].Items;
    if(inv==null) map[y,x].Items = inv = new ItemPile();
    inv.Add(item);
  }

  public bool Contains(int x, int y) { return y>=0 && y<height && x>=0 && x<width; }

  public bool GetFlag(Point pt, Tile.Flag flag) { return this[pt.X,pt.Y].GetFlag(flag); }
  public bool GetFlag(int x, int y, Tile.Flag flag) { return this[x,y].GetFlag(flag); }
  public void SetFlag(Point pt, Tile.Flag flag, bool on) { map[pt.Y,pt.X].SetFlag(flag, on); }
  public void SetFlag(int x, int y, Tile.Flag flag, bool on) { map[y,x].SetFlag(flag, on); }
  
  public bool HasItems(Point pt) { return HasItems(pt.X, pt.Y); }
  public bool HasItems(int x, int y) { return map[y,x].Items!=null && map[y,x].Items.Count>0; }

  public void SetType(Point pt, TileType type) { map[pt.Y,pt.X].Type = type; }
  public void SetType(int x, int y, TileType type) { map[y,x].Type = type; }

  public bool IsDangerous(Point pt) { return IsDangerous(this[pt.X, pt.Y].Type); }
  public bool IsDangerous(int x, int y) { return IsDangerous(this[x,y].Type); }

  public bool IsPassable(Point pt) { return IsPassable(pt.X, pt.Y); }
  public bool IsPassable(int x, int y)
  { Tile tile = this[x,y];
    if(!IsPassable(tile.Type)) return false;
    for(int i=0; i<creatures.Count; i++) if(creatures[i].X==x && creatures[i].Y==y) return false;
    return true;
  }
  public bool IsWall(Point pt) { return IsWall(this[pt.X,pt.Y].Type); }
  public bool IsWall(int x, int y) { return IsWall(this[x,y].Type); }
  public bool IsDoor(Point pt) { return IsDoor(this[pt.X,pt.Y].Type); }
  public bool IsDoor(int x, int y) { return IsDoor(this[x,y].Type); }

  public Point FreeSpace() { return FreeSpace(false, false); }
  public Point FreeSpace(bool allowItems) { return FreeSpace(allowItems, false); }
  public Point FreeSpace(bool allowItems, bool allowCreatures)
  { int tries = width*height;
    while(tries-->0)
    { Point pt = new Point(Global.Rand(width), Global.Rand(height));
      if(!IsFreeSpace(pt, allowItems, allowCreatures)) continue;
      return pt;
    }
    for(int y=0; y<height; y++)
      for(int x=0; x<width; x++)
        if(IsFreeSpace(x, y, allowItems, allowCreatures)) return new Point(x, y);
    throw new ArgumentException("No free space found on this map!");
  }
  
  public bool IsFreeSpace(Point pt, bool allowItems, bool allowCreatures)
  { return IsFreeSpace(pt.X, pt.Y, allowItems, allowCreatures);
  }
  public bool IsFreeSpace(int x, int y, bool allowItems, bool allowCreatures)
  { Tile tile = this[x, y];
    if(!IsPassable(tile.Type) || !allowItems && tile.Items!=null && tile.Items.Count>0) return false;
    if(!allowCreatures) for(int i=0; i<creatures.Count; i++) if(creatures[i].X==x && creatures[i].Y==y) return false;
    return true;
  }

  public Point RandomTile(TileType type)
  { int tries = width*height;
    while(tries-->0)
    { int x = Global.Rand(width), y = Global.Rand(height);
      if(map[y, x].Type==type) return new Point(x, y);
    }
    for(int y=0; y<height; y++) for(int x=0; x<width; x++) if(map[y, x].Type==type) return new Point(x, y);
    throw new ArgumentException("No such tile on this map!");
  }

  public static bool IsDangerous(TileType type) { return (tileFlag[(int)type]&TileFlag.Dangerous) != TileFlag.None; }
  public static bool IsPassable(TileType type) { return (tileFlag[(int)type]&TileFlag.Passable) != TileFlag.None; }
  public static bool IsWall(TileType type) { return (tileFlag[(int)type]&TileFlag.Wall) != TileFlag.None; }
  public static bool IsDoor(TileType type) { return (tileFlag[(int)type]&TileFlag.Door) != TileFlag.None; }

  public CreatureCollection Creatures { get { return creatures; } }

  public void Simulate() { Simulate(null); }
  public void Simulate(Player player)
  { bool addedPlayer = player==null;

    while(thinkQueue.Count==0)
    { while(thinkQueue.Count==0)
      { timer += 10;
        for(int i=0; i<creatures.Count; i++)
        { Creature c = creatures[i];
          c.Timer += 10;
          if(c.Timer>=c.Speed)
          { if(c==player) addedPlayer=true;
            thinkQueue.Enqueue(c);
          }
        }
        if(timer>=100)
        { timer -= 100;
          for(int i=0; i<creatures.Count; i++) creatures[i].ItemThink();
          for(int y=0; y<height; y++)
            for(int x=0; x<width; x++)
            { ItemPile items = map[y,x].Items;
              if(items!=null) for(int i=0; i<items.Count; i++) items[i].Think(null);
            }
        }
      }
      if(addedPlayer) break;
      Simulate(null);
    }

    thinking++;
    while(thinkQueue.Count!=0)
    { Creature c = (Creature)thinkQueue.Dequeue();
      if(removedCreatures.Contains(c)) continue;
      if(c==player) { thinking--; return; }
      c.Think();
    }
    if(--thinking==0) removedCreatures.Clear();
  }

  class CreatureComparer : IComparer
  { public int Compare(object x, object y) { return ((Creature)x).Timer - ((Creature)y).Timer; }
  }

  void Added(Creature c)
  { c.Map=this;
    c.OnMapChanged();
  }
  void Removed(Creature c)
  { c.Map=null;
    c.OnMapChanged();
    if(thinking>0) removedCreatures[c]=true;
  }

  Tile[,] map;
  CreatureCollection creatures;
  PriorityQueue thinkQueue = new PriorityQueue(new CreatureComparer());
  GameLib.Collections.Map removedCreatures = new GameLib.Collections.Map();
  int width, height, thinking, timer;

  [Flags]
  enum TileFlag : byte { None=0, Passable=1, Wall=2, Door=4, Dangerous=8 }
  static readonly TileFlag[] tileFlag = new TileFlag[]
  { TileFlag.None,   // Border
    TileFlag.None,   // SolidRock
    TileFlag.Wall,   // Wall
    TileFlag.Door,   // ClosedDoor
    TileFlag.Door|TileFlag.Passable, // OpenDoor
    TileFlag.Passable, TileFlag.Passable, TileFlag.Passable, TileFlag.Passable, // RoomFloor, Corridor, stairs
    TileFlag.Passable, // ShallowWater
    TileFlag.Passable|TileFlag.Dangerous, // DeepWater
    TileFlag.Passable, // Ice
    TileFlag.Passable|TileFlag.Dangerous, // Lava
    TileFlag.Passable|TileFlag.Dangerous, // Pit
    TileFlag.Passable|TileFlag.Dangerous, // Hole
    TileFlag.Passable|TileFlag.Dangerous, // Trap
    TileFlag.Passable // Altar
  };
}

} // namespace Chrono
