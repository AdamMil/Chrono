using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;
using GameLib.Collections;

namespace Chrono
{

public enum TileType : byte
{ Border,

  SolidRock, Wall, ClosedDoor, OpenDoor, RoomFloor, Corridor, UpStairs, DownStairs,
  ShallowWater, DeepWater, Ice, Lava, Pit, Hole,

  Trap, Altar,
  
  NumTypes
}

public enum Trap : byte { Dart, PoisonDart, Magic, MpDrain, Teleport, Pit }
public enum God : byte { God1, God2, God3 }

public enum Noise { Walking, Bang, Combat, Alert, NeedHelp, Item, Zap }

[Serializable]
public struct Tile
{ [Flags] public enum Flag : byte { None=0, Hidden=1, Locked=2, Seen=4 };

  public bool GetFlag(Flag f) { return (Flags&(byte)f)!=0; }
  public void SetFlag(Flag flag, bool on) { if(on) Flags|=(byte)flag; else Flags&=(byte)~flag; }

  public ItemPile  Items;
  public Entity    Entity;   // for memory (creature on tile), or owner of trap
  [NonSerialized] public PathNode Node; // for pathfinding
  public ushort    Scent;    // strength of player smell
  public TileType  Type;
  public byte      Subtype;  // subtype of tile (ie, type of trap/altar/etc)
  public byte      Flags;
  public byte      Sound;

  public static Tile Border { get { return new Tile(); } }
}

[Serializable]
public struct Link
{ public Link(Point from, bool down) { From=from; To=new Point(-1, -1); ToLevel=-1; Down=down; }
  public Point From, To;
  public int  ToLevel;
  public bool Down;
}

public class PathNode
{ public PathNode(int x, int y) { Point=new Point(x, y); }
  public enum State : byte { New, Open, Closed };

  public Point    Point;
  public PathNode Parent;
  public int      Base, Cost;
  public State    Type;
  public byte     Length;
}

public sealed class PathFinder // FIXME: having this latch onto the .Node bits of a map is not clean
{ public PathFinder() { queue = new BinaryTree(NodeComparer.Default); }

  public PathNode GetPathFrom(Point pt) { return map[pt].Node; }

  public bool Plan(Map map, Point start, Point goal)
  { queue.Clear();
    if(this.map!=map) Clear(this.map);

    PathNode n;
    byte maxlen = (byte)Math.Min(Math.Max(map.Width, map.Height)*3/2, 255);
    for(int y=0; y<map.Height; y++)
      for(int x=0; x<map.Width; x++)
      { n = map[x, y].Node;
        if(n==null) map.SetNode(x, y, new PathNode(x, y));
        else n.Type=PathNode.State.New;
      }

    this.map = map;
    if(map[start].Node==null || map[goal].Node==null) return false;
    n = map[goal].Node;
    n.Base = n.Cost = n.Length = 0;
    Insert(n); // work backwards

    while(queue.Count>0)
    { n = (PathNode)queue.RemoveMinimum();
      if(n.Point==start) { queue.Clear(); return true; } // work backwards
      if(n.Length==maxlen) break;
      n.Type = PathNode.State.Closed;
      for(int yi=-1; yi<=1; yi++)
        for(int xi=-1; xi<=1; xi++)
        { if(xi==0 && yi==0) continue;
          PathNode nei = map[n.Point.X+xi, n.Point.Y+yi].Node;
          if(nei==null) continue;
          int move=MoveCost(nei), bcost=n.Base+move, heur=Math.Max(Math.Abs(nei.Point.X-goal.X), Math.Abs(nei.Point.Y-goal.Y))/8,
              cost=bcost + (move>2 ? heur : heur/4);
          if(nei.Type!=PathNode.State.New && cost>=nei.Cost) continue;
          nei.Parent = n;
          nei.Base   = bcost;
          nei.Cost   = cost;
          nei.Length = (byte)(n.Length+1);
          Insert(nei);
        }
    }
    queue.Clear();
    return false;
  }

  void Clear(Map map)
  { if(map==null) return;
    for(int y=0; y<map.Height; y++) for(int x=0; x<map.Width; x++) map.SetNode(x, y, null);
  }
  
  void Insert(PathNode node) { queue.Add(node); node.Type=PathNode.State.Open; }

  int MoveCost(PathNode node)
  { TileType type = map[node.Point].Type;
    return Map.IsDangerous(type) ? 2000 : // dangerous tiles
           Map.IsPassable(type) ? 1 :     // freely passable tiles
           Map.IsDoor(type) && !map.GetFlag(node.Point, Tile.Flag.Locked) ? 2 : // doors
           map.GetFlag(node.Point, Tile.Flag.Seen) ? 10000 : // known unpassable areas
           10; // unknown areas
  }

  class NodeComparer : IComparer
  { public int Compare(object x, object y)
    { PathNode a=(PathNode)x, b=(PathNode)y;
      int n = a.Cost - b.Cost;
      if(n==0)
      { n = a.Point.X-b.Point.X;
        if(n==0) n = a.Point.Y-b.Point.Y;
      }
      return n;
    }

    public static NodeComparer Default = new NodeComparer();
  }

  BinaryTree queue;
  Map map;
}

[Serializable]
public sealed class Map : UniqueObject
{ // maximum scent on a tile, maximum scent add on a single call (maximum entity smelliness), maximum sound on a tile
  public const int MaxScent=1200, MaxScentAdd=800, MaxSound=255;
  
  #region EntityCollection
  [Serializable]
  public class EntityCollection : ArrayList
  { public EntityCollection(Map map) { this.map = map; }

    public new Entity this[int index] { get { return (Entity)base[index]; } }

    public new int Add(object o) { return Add((Entity)o); }
    public int Add(Entity c)
    { int i = base.Add(c);
      map.Added(c);
      return i;
    }
    public new void AddRange(ICollection entities)
    { base.AddRange(entities);
      foreach(Entity c in entities) map.Added(c);
    }
    public new void Insert(int index, object o) { Insert(index, (Entity)o); }
    public void Insert(int index, Entity c)
    { base.Insert(index, c);
      map.Added(c);
    }
    public void InsertRange(ICollection entities, int index)
    { base.InsertRange(index, entities);
      foreach(Entity c in entities) map.Added(c);
    }
    public new void Remove(object o) { Remove((Entity)o); }
    public void Remove(Entity c)
    { base.Remove(c);
      map.Removed(c);
    }
    public new void RemoveAt(int index)
    { Entity c = this[index];
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

  public Map(int width, int height) : this(width, height, TileType.SolidRock, true) { }
  public Map(int width, int height, TileType fill, bool seen)
  { this.width  = width;
    this.height = height;
    map = new Tile[height, width];
    entities = new EntityCollection(this);
    if(fill!=TileType.Border)
      for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Type=fill;
    if(seen) for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Flags=(byte)Tile.Flag.Seen;
  }
  public Map(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public EntityCollection Entities { get { return entities; } }
  public Link[] Links { get { return links; } }
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
    item.OnMap();
  }
  
  public void AddLink(Link link)
  { Link[] narr = new Link[links.Length+1];
    Array.Copy(links, narr, links.Length);
    narr[links.Length] = link;
    links = narr;
  }
  
  public void ClearLinks() { links = new Link[0]; }

  public void AddScent(int x, int y, int amount)
  { map[y,x].Scent = (ushort)Math.Min(map[y,x].Scent+Math.Min(amount, MaxScentAdd), MaxScent);
  }

  public bool Contains(int x, int y) { return y>=0 && y<height && x>=0 && x<width; }

  public Entity GetEntity(Point pt)
  { for(int i=0; i<entities.Count; i++) if(entities[i].Position==pt) return entities[i];
    return null;
  }
  public Entity GetEntity(int x, int y) { return GetEntity(new Point(x, y)); }

  public bool GetFlag(Point pt, Tile.Flag flag) { return this[pt.X,pt.Y].GetFlag(flag); }
  public bool GetFlag(int x, int y, Tile.Flag flag) { return this[x,y].GetFlag(flag); }
  public void SetFlag(Point pt, Tile.Flag flag, bool on) { map[pt.Y,pt.X].SetFlag(flag, on); }
  public void SetFlag(int x, int y, Tile.Flag flag, bool on) { map[y,x].SetFlag(flag, on); }
  
  public Link GetLink(Point pt)
  { for(int i=0; i<links.Length; i++)
    { if(links[i].From==pt)
      { if(links[i].To.X==-1)
        { Map nm = App.Dungeon[links[i].ToLevel];
          for(int ml=0,ol=0; ml<links.Length; ml++)
          { if(links[ml].ToLevel!=nm.Index) continue;
            while(ol<nm.links.Length && nm.links[ol].ToLevel!=Index) ol++;
            links[ml].To = nm.links[ol].From;
            nm.links[ol].To = links[ml].From;
          }
        }
        return links[i];
      }
    }
    throw new ApplicationException("No such link");
  }

  public bool HasItems(Point pt) { return HasItems(pt.X, pt.Y); }
  public bool HasItems(int x, int y) { return map[y,x].Items!=null && map[y,x].Items.Count>0; }

  public void SetNode(Point pt, PathNode node) { map[pt.Y,pt.X].Node = node; }
  public void SetNode(int x, int y, PathNode node) { map[y,x].Node = node; }

  public void SetType(Point pt, TileType type) { map[pt.Y,pt.X].Type = type; }
  public void SetType(int x, int y, TileType type) { map[y,x].Type = type; }

  public bool IsDangerous(Point pt) { return IsDangerous(this[pt.X, pt.Y].Type); }
  public bool IsDangerous(int x, int y) { return IsDangerous(this[x,y].Type); }

  public bool IsPassable(Point pt) { return IsPassable(pt.X, pt.Y); }
  public bool IsPassable(int x, int y)
  { Tile tile = this[x,y];
    if(!IsPassable(tile.Type)) return false;
    for(int i=0; i<entities.Count; i++) if(entities[i].X==x && entities[i].Y==y) return false;
    return true;
  }
  public bool IsWall(Point pt) { return IsWall(this[pt.X,pt.Y].Type); }
  public bool IsWall(int x, int y) { return IsWall(this[x,y].Type); }
  public bool IsDoor(Point pt) { return IsDoor(this[pt.X,pt.Y].Type); }
  public bool IsDoor(int x, int y) { return IsDoor(this[x,y].Type); }

  public Point FreeSpace() { return FreeSpace(true, false); }
  public Point FreeSpace(bool allowItems) { return FreeSpace(allowItems, false); }
  public Point FreeSpace(bool allowItems, bool allowEntities)
  { int tries = width*height;
    while(tries-->0)
    { Point pt = new Point(Global.Rand(width), Global.Rand(height));
      if(!IsFreeSpace(pt, allowItems, allowEntities)) continue;
      return pt;
    }
    for(int y=0; y<height; y++)
      for(int x=0; x<width; x++)
        if(IsFreeSpace(x, y, allowItems, allowEntities)) return new Point(x, y);
    throw new ArgumentException("No free space found on this map!");
  }

  public bool IsFreeSpace(Point pt, bool allowItems, bool allowEntities)
  { return IsFreeSpace(pt.X, pt.Y, allowItems, allowEntities);
  }
  public bool IsFreeSpace(int x, int y, bool allowItems, bool allowEntities)
  { Tile tile = this[x, y];
    if(!IsPassable(tile.Type) || !allowItems && tile.Items!=null && tile.Items.Count>0) return false;
    if(!allowEntities) for(int i=0; i<entities.Count; i++) if(entities[i].X==x && entities[i].Y==y) return false;
    return true;
  }

  public void MakeNoise(Point pt, Entity source, Noise type, byte volume)
  { for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Sound=0;
    if(soundStack==null) soundStack=new Point[256];
    int slen, nslen=1;
    bool changed;

    map[pt.Y, pt.X].Sound = volume;
    
    if(volume>10)
    { soundStack[0] = pt;
      do
      { slen=nslen; changed=false;
        for(int i=0; i<slen; i++)
        { Point cp=soundStack[i];
          int val = map[cp.Y,cp.X].Sound - 10; // yeah, yeah, a poor decay method...

          for(int d=0; d<8; d++)
          { Point np = Global.Move(cp, d);
            Tile   t = this[np];
            int tval = t.Type==TileType.ClosedDoor ? val-60 : val;
            if((IsPassable(t.Type) || t.Type==TileType.ClosedDoor) && tval>t.Sound)
            { if(t.Sound==0)
              { if(nslen==soundStack.Length)
                { Point[] narr = new Point[nslen+128];
                  Array.Copy(soundStack, narr, nslen);
                  soundStack = narr;
                }
                soundStack[nslen++] = np;
              }
              map[np.Y,np.X].Sound = (byte)tval;
              changed = true;
            }
          }
        }
      } while(changed);
    }
    if(!IsPassable(map[pt.Y,pt.X].Type)) map[pt.Y,pt.X].Sound=0;
    
    foreach(Entity e in entities)
      if(e!=source)
      { int vol = map[e.Position.Y, e.Position.X].Sound;
        if(vol>0) e.OnNoise(source, type, vol);
      }
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
  
  public Map RestoreMemory()
  { Map ret = Memory;
    Memory = null;
    return ret;
  }

  public void SaveMemory(Map memory) { Memory = memory; }

  public static bool IsDangerous(TileType type) { return (tileFlag[(int)type]&TileFlag.Dangerous) != TileFlag.None; }
  public static bool IsPassable(TileType type) { return (tileFlag[(int)type]&TileFlag.Passable) != TileFlag.None; }
  public static bool IsWall(TileType type) { return (tileFlag[(int)type]&TileFlag.Wall) != TileFlag.None; }
  public static bool IsDoor(TileType type) { return (tileFlag[(int)type]&TileFlag.Door) != TileFlag.None; }

  public void Simulate() { Simulate(null); }
  public void Simulate(Player player)
  { bool addedPlayer = player==null;

    while(thinkQueue.Count==0)
    { while(thinkQueue.Count==0)
      { timer += 10;
        for(int i=0; i<entities.Count; i++)
        { Entity c = entities[i];
          c.Timer += 10;
          if(c.Timer>=c.Speed)
          { if(c==player) addedPlayer=true;
            thinkQueue.Enqueue(c);
          }
        }
        if(timer>=100)
        { timer -= 100;
          for(int i=0; i<entities.Count; i++) entities[i].ItemThink();
          for(int y=0; y<height; y++)
            for(int x=0; x<width; x++)
            { ItemPile items = map[y,x].Items;
              if(items!=null) for(int i=0; i<items.Count; i++) if(items[i].Think(null)) items.RemoveAt(i--);
            }
          age++;
          int level = App.Player.Map.Index;
          if(numCreatures<50 && (Index==level && age%75==0 || Index!=level && age%150==0)) SpawnMonster();
        }
        if(entities.Count==0) return;
      }
      if(addedPlayer) break;
      Simulate(null);
    }

    thinking++;
    while(!App.Quit && thinkQueue.Count!=0)
    { Entity c = (Entity)thinkQueue.Dequeue();
      if(removedEntities.Contains(c)) continue;
      if(c==player) { thinking--; return; }
      c.Think();
    }
    if(--thinking==0) removedEntities.Clear();
  }
  
  public Item SpawnItem()
  { SpawnInfo s = Global.NextSpawn();
    Item item = (Item)s.ItemType.GetConstructor(Type.EmptyTypes).Invoke(null);
    item.Count = Global.Rand(s.SpawnMax-s.SpawnMin)+s.SpawnMin;

    if(Global.Rand(100)<15) item.Curse(); // TODO: make these improve weapon/armor
    else if(Global.Rand(100)<10) item.Bless();

    AddItem(FreeSpace(true, true), item);
    return item;
  }

  public void SpawnMonster()
  { int idx = entities.Add(Entity.Generate(typeof(Orc), Index+1, EntityClass.Fighter));
    for(int i=0; i<10; i++)
    { entities[idx].Position = FreeSpace();
      if(App.Player==null || !App.Player.CanSee(entities[idx])) break;
    }
  }

  public void SpreadScent()
  { if(scentbuf==null || scentbuf.Length<width*height) scentbuf=new ushort[width*height];
    for(int y=0,i=0; y<height; y++)
      for(int x=0; x<width; i++,x++)
      { if(!IsPassable(map[y,x].Type)) continue;
        int val=0, n=0;
        for(int yi=-1; yi<=1; yi++)
          for(int xi=-1; xi<=1; xi++)
            if(xi!=0 || yi!=0)
            { Tile t = this[x+xi, y+yi];
              if(IsPassable(t.Type)) { val += t.Scent; n++; }
            }
        if(n>0) scentbuf[i] = (ushort)Math.Max(val/n-3, 0);
      }
    for(int y=0,i=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Scent=scentbuf[i++];
  }

  public Map Memory;
  public int Index;

  [Serializable]
  class EntityComparer : IComparer
  { public int Compare(object x, object y) { return ((Entity)x).Timer - ((Entity)y).Timer; }
  }

  void Added(Entity c)
  { c.Map=this;
    c.OnMapChanged();
    if(c.Class!=EntityClass.Other) numCreatures++;
  }
  void Removed(Entity c)
  { c.Map=null;
    c.OnMapChanged();
    if(thinking>0) removedEntities[c]=true;
    if(c.Class!=EntityClass.Other) numCreatures--;
  }

  Tile[,] map;
  Link[]  links = new Link[0];
  EntityCollection entities;
  PriorityQueue thinkQueue = new PriorityQueue(new EntityComparer());
  Hashtable removedEntities = new Hashtable();
  int width, height, thinking, timer, age, numCreatures;

  static ushort[] scentbuf;
  static Point[] soundStack;

  [Flags]
  enum TileFlag : byte { None=0, Passable=1, Wall=2, Door=4, Dangerous=8 }
  static readonly TileFlag[] tileFlag = new TileFlag[(int)TileType.NumTypes]
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
