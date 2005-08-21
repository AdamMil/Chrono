using System;
using System.Collections;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Xml;
using GameLib.Collections;

namespace Chrono
{

#region Types and Enums
public enum MapType { Overworld, Town, Other }

public enum Noise { Walking, Bang, Combat, Alert, NeedHelp, Item, Zap }

public enum ShopType { General, Books, Food, Armor, Weapons, ArmorWeapons, Accessories, Magic, NumTypes };

public enum TileType : byte
{ Border,

  SolidRock, Wall, ClosedDoor, OpenDoor, RoomFloor, Corridor, UpStairs, DownStairs,
  ShallowWater, DeepWater, Ice, Lava, Pit, Hole, Trap, Altar,
  
  Tree, Forest, DirtSand, Grass, Hill, Mountain, Road, Town, Portal,

  NumTypes
}

public enum Trap : byte { Dart, PoisonDart, Magic, MpDrain, Teleport, Pit }

public class Room : UniqueObject
{ public Room(Rectangle area, string name) { OuterArea=area; Name=name; }
  
  public Rectangle InnerArea { get { Rectangle r = OuterArea; r.Inflate(-1, -1); return r; } }
  
  public Rectangle OuterArea;
  public string Name;
}

public class Shop : Room
{ public Shop(Rectangle area, Shopkeeper shopkeeper, ShopType type) : base(area, null)
  { Shopkeeper=shopkeeper; Type=type;
  }

  public Point FrontOfDoor
  { get
    { Point ret = Door;
      switch(DoorSide)
      { case Direction.Up:    ret.Y++; break;
        case Direction.Down:  ret.Y--; break;
        case Direction.Left:  ret.X++; break;
        case Direction.Right: ret.X--; break;
      }
      return ret;
    }
  }

  public Rectangle ItemArea
  { get
    { Rectangle ret = InnerArea;
      switch(DoorSide)
      { case Direction.Up:    ret.Y++; ret.Height--; break;
        case Direction.Down:  ret.Height--; break;
        case Direction.Left:  ret.X++; ret.Width--; break;
        case Direction.Right: ret.Width--; break;
      }
      return ret;
    }
  }

  public bool Accepts(Item item) { return Accepts(item.Class); }
  public bool Accepts(ItemClass ic)
  { switch(Type)
    { case ShopType.Accessories: return ic==ItemClass.Amulet || ic==ItemClass.Ring;
      case ShopType.Armor: return ic==ItemClass.Armor;
      case ShopType.ArmorWeapons: return ic==ItemClass.Ammo || ic==ItemClass.Armor || ic==ItemClass.Weapon;
      case ShopType.Books: return ic==ItemClass.Scroll || ic==ItemClass.Spellbook;
      case ShopType.Food: return ic==ItemClass.Food;
      case ShopType.General: return ic!=ItemClass.Gold;
      case ShopType.Magic:
        return ic==ItemClass.Amulet || ic==ItemClass.Ring || ic==ItemClass.Scroll || ic==ItemClass.Spellbook ||
               ic==ItemClass.Wand || ic==ItemClass.Potion;
      case ShopType.Weapons: return ic==ItemClass.Ammo || ic==ItemClass.Weapon;
      default: throw new NotImplementedException("Unhandled shop type: "+Type.ToString());
    }
  }

  public Point      Door;
  public Direction  DoorSide;
  public Shopkeeper Shopkeeper;
  public ShopType   Type;
}

public struct Tile
{ [Flags] public enum Flag : byte { None=0, Hidden=1, Locked=2, Seen=4 };

  public bool GetFlag(Flag f) { return (Flags&(byte)f)!=0; }
  public void SetFlag(Flag flag, bool on) { if(on) Flags|=(byte)flag; else Flags&=(byte)~flag; }

  public ItemPile  Items;
  public Entity    Entity;   // for memory (creature on tile), or owner of trap
  [NonSerialized] public PathNode Node; // for pathfinding
  public TileType  Type;
  public byte      Subtype;  // subtype of tile (ie, type of trap/altar/etc)
  public byte      Flags;
  public byte      Sound;

  public static Tile Border { get { return new Tile(); } }
}

public struct Link
{ public Link(Point from, bool down, Dungeon.Section toSection, int toLevel)
  { ToPoint=new Point(-1, -1);
    FromPoint=from; ToSection=toSection; ToLevel=toLevel; Down=down;
  }

  public Point FromPoint, ToPoint;
  public Dungeon.Section  ToSection;
  public int  ToLevel;
  public bool Down; // TODO: consider getting rid of this field
}
#endregion

#region Pathfinding
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
    int cost = Map.IsDangerous(type) ? 2000 : // dangerous tiles
               Map.IsPassable(type)  ? 1    : // freely passable tiles
               Map.IsDoor(type) && !map.GetFlag(node.Point, Tile.Flag.Locked) ? 2 : // closed, unlocked doors
               map.GetFlag(node.Point, Tile.Flag.Seen) ? 10000 : // known unpassable areas
               10; // unknown areas
    if(Map.IsPassable(type) && map.GetEntity(node.Point)!=null) cost += 10; // entity in the way
    return cost;
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
#endregion

#region Map
public class Map : UniqueObject
{ // maximum scent on a tile, maximum scent add on a single call (maximum entity smelliness), maximum sound on a tile
  public const int MaxScent=1200, MaxScentAdd=800, MaxSound=255;
  
  [Flags] public enum Space
  { None=0, Items=1, Entities=2, Links=4, All=Items|Entities|Links,
    NoItems=All&~Items, NoEntities=All&~Entities, NoLinks=All&~Links
  };

  #region EntityCollection
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

  public Map(Size size) : this(size.Width, size.Height, TileType.SolidRock, true) { }
  public Map(Size size, TileType fill, bool seen) : this(size.Width, size.Height, fill, seen) { }
  public Map(int width, int height) : this(width, height, TileType.SolidRock, true) { }
  public Map(int width, int height, TileType fill, bool seen)
  { this.width  = width;
    this.height = height;
    map = new Tile[height, width];
    scentmap = new ushort[height, width];
    entities = new EntityCollection(this);
    if(fill!=TileType.Border) Fill(fill);
    if(seen) for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Flags=(byte)Tile.Flag.Seen;
  }

  static Map()
  { Type[] types = typeof(Item).Assembly.GetTypes();
    ArrayList[] lists = new ArrayList[(int)ShopType.NumTypes];
    for(int i=0; i<lists.Length; i++) lists[i] = new ArrayList();

    foreach(ItemInfo si in Global.Items)
    { if(si.Value<=0) continue;
      if(si.Class!=ItemClass.Gold) lists[(int)ShopType.General].Add(si);
      switch(si.Class)
      { case ItemClass.Amulet: case ItemClass.Ring:
          lists[(int)ShopType.Accessories].Add(si);
          lists[(int)ShopType.Magic].Add(si);
          break;
        case ItemClass.Ammo: case ItemClass.Weapon:
          lists[(int)ShopType.ArmorWeapons].Add(si);
          lists[(int)ShopType.Weapons].Add(si);
          break;
        case ItemClass.Armor: case ItemClass.Shield:
          lists[(int)ShopType.Armor].Add(si);
          lists[(int)ShopType.ArmorWeapons].Add(si);
          break;
        case ItemClass.Food: lists[(int)ShopType.Food].Add(si); break;
        case ItemClass.Scroll: case ItemClass.Spellbook:
          lists[(int)ShopType.Books].Add(si);
          lists[(int)ShopType.Magic].Add(si);
          break;
        case ItemClass.Wand: case ItemClass.Potion: lists[(int)ShopType.Magic].Add(si); break;
      }
    }

    objSpawns = new ItemInfo[lists.Length][];
    for(int i=0; i<lists.Length; i++) objSpawns[i] = (ItemInfo[])lists[i].ToArray(typeof(ItemInfo));
  }

  public EntityCollection Entities { get { return entities; } }
  public Link[] Links { get { return links; } }
  public Room[] Rooms { get { return rooms; } }

  public string Name { get { return name!=null ? name : Section.Name; } }
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

  public bool IsOverworld { get { return mapType==MapType.Overworld; } }
  public bool IsTown { get { return mapType==MapType.Town; } }

  public Item AddItem(Point pt, Item item) { return AddItem(pt.X, pt.Y, item); }
  public Item AddItem(int x, int y, Item item)
  { ItemPile inv = map[y,x].Items;
    if(inv==null) map[y,x].Items = inv = new ItemPile();
    Item ret = inv.Add(item);
    item.OnMap();
    return ret;
  }
  
  public void AddLink(Link link)
  { Link[] narr = new Link[links.Length+1];
    Array.Copy(links, narr, links.Length);
    narr[links.Length] = link;
    links = narr;
  }

  public void AddRoom(Rectangle rect) { AddRoom(new Room(rect, null)); }
  public void AddRoom(Rectangle rect, string name) { AddRoom(new Room(rect, name)); }
  public void AddRoom(Room room)
  { Room[] narr = new Room[rooms.Length+1];
    if(narr.Length!=1) Array.Copy(rooms, narr, narr.Length-1);
    narr[rooms.Length] = room;
    rooms = narr;
  }

  public void AddShop(Rectangle rect, ShopType type) { AddShop(rect, type, true); }
  public void AddShop(Rectangle rect, ShopType type, bool stock)
  { Shopkeeper sk = new Shopkeeper();
    AI.Make(sk, 2, Race.Human, EntityClass.Fighter);
    AddShop(rect, type, sk, stock);
  }
  public void AddShop(Rectangle rect, ShopType type, Shopkeeper shopkeeper, bool stock)
  { Shop shop = new Shop(rect, shopkeeper, type);
    for(int y=shop.OuterArea.Y; y<shop.OuterArea.Bottom; y++)
      for(int x=shop.OuterArea.X; x<shop.OuterArea.Right; x++)
        if(IsDoor(x, y))
        { shop.Door = new Point(x, y);
          if(x==shop.OuterArea.X) shop.DoorSide=Direction.Left;
          else if(y==shop.OuterArea.Y) shop.DoorSide=Direction.Up;
          else if(x==shop.OuterArea.Right-1) shop.DoorSide=Direction.Right;
          else if(y==shop.OuterArea.Bottom-1) shop.DoorSide=Direction.Down;
          else throw new ArgumentException("This shop has an oddly-placed door!");
          goto foundDoor;
        }
    throw new ArgumentException("This shop has no door!");

    foundDoor:
    Entities.Add(shop.Shopkeeper);
    shop.Shopkeeper.Position    = shop.FrontOfDoor;
    shop.Shopkeeper.SocialGroup = GroupID;
    if(stock) while(RestockShop(shop));
    
    AddRoom(shop);
  }
  
  public void AddScent(int x, int y, int amount)
  { if(IsOverworld) return;
    scentmap[y,x] = (ushort)Math.Min(scentmap[y,x]+Math.Min(amount, MaxScentAdd), MaxScent);
  }

  public void ClearLinks() { links = new Link[0]; }

  public bool Contains(int x, int y) { return y>=0 && y<height && x>=0 && x<width; }

  public void Fill(TileType type) { for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Type=type; }

  public Entity GetEntity(int x, int y) { return GetEntity(new Point(x, y)); }
  public Entity GetEntity(Point pt)
  { Tile t = this[pt];
    if(t.Entity!=null) return t.Entity;
    for(int i=0; i<entities.Count; i++) if(entities[i].Position==pt) return entities[i];
    return null;
  }
  public Entity GetEntity(string id)
  { for(int i=0; i<entities.Count; i++) if(entities[i].EntityID==id) return entities[i];
    return null;
  }

  public bool GetFlag(Point pt, Tile.Flag flag) { return this[pt.X,pt.Y].GetFlag(flag); }
  public bool GetFlag(int x, int y, Tile.Flag flag) { return this[x,y].GetFlag(flag); }
  public void SetFlag(Point pt, Tile.Flag flag, bool on) { map[pt.Y,pt.X].SetFlag(flag, on); }
  public void SetFlag(int x, int y, Tile.Flag flag, bool on) { map[y,x].SetFlag(flag, on); }
  
  public Link GetLink(Point pt) { return GetLink(pt, true); }
  public Link GetLink(Point pt, bool autoGenerate)
  { for(int i=0; i<links.Length; i++) if(links[i].FromPoint==pt) return GetLink(i, autoGenerate);
    throw new ApplicationException("No such link");
  }
  public Link GetLink(int index) { return GetLink(index, true); }
  public Link GetLink(int index, bool autoGenerate)
  { if(autoGenerate && links[index].ToPoint.X==-1) // if the link hasn't been initialized yet
    { Map nm = links[index].ToSection[links[index].ToLevel];
      for(int i=0; i<nm.links.Length; i++)
      { Link link = nm.links[i];
        if((link.ToLevel==Index && link.ToSection==Section || link.ToLevel==-1) &&
           (link.ToPoint.X==-1 || link.ToPoint==links[index].FromPoint))
        { links[index].ToPoint  = link.FromPoint;
          nm.links[i].ToPoint   = links[index].FromPoint;
          nm.links[i].ToSection = Section;
          nm.links[i].ToLevel   = Index;
          break;
        }
      }
    }
    return links[index];
  }

  public int GetScent(Point pt) { return GetScent(pt.X, pt.Y); }
  public int GetScent(int x, int y)
  { if(x<0 || x>=width || y<0 || y>=height) return 0;
    return scentmap[y,x];
  }

  public Room GetRoom(int x, int y) { return GetRoom(new Point(x, y)); }
  public Room GetRoom(Point pt)
  { for(int i=0; i<rooms.Length; i++) if(rooms[i].OuterArea.Contains(pt)) return rooms[i];
    return null;
  }
  public Room GetRoom(string id)
  { for(int i=0; i<rooms.Length; i++) if(rooms[i].Name==id) return rooms[i];
    return null;
  }

  public Shop GetShop(int x, int y) { return GetRoom(new Point(x, y)) as Shop; }
  public Shop GetShop(Point pt) { return GetRoom(pt) as Shop; }

  public bool HasItems(Point pt) { return HasItems(pt.X, pt.Y); }
  public bool HasItems(int x, int y) { return this[x,y].Items!=null && map[y,x].Items.Count>0; }

  public void SetNode(Point pt, PathNode node) { map[pt.Y,pt.X].Node = node; }
  public void SetNode(int x, int y, PathNode node) { map[y,x].Node = node; }

  public void SetType(Point pt, TileType type) { map[pt.Y,pt.X].Type = type; }
  public void SetType(int x, int y, TileType type) { map[y,x].Type = type; }

  public bool IsDangerous(Point pt) { return IsDangerous(this[pt.X, pt.Y].Type); }
  public bool IsDangerous(int x, int y) { return IsDangerous(this[x,y].Type); }

  public bool IsLink(Point pt) { return IsLink(this[pt.X, pt.Y].Type); }
  public bool IsLink(int x, int y) { return IsLink(this[x,y].Type); }

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

  // FIXME: make changes so this doesn't crash the game
  public Point FreeSpace() { return FreeSpace(Space.Items, new Rectangle(0, 0, Width, Height)); }
  public Point FreeSpace(Rectangle area) { return FreeSpace(Space.Items, area); }
  public Point FreeSpace(Space allow) { return FreeSpace(allow, new Rectangle(0, 0, Width, Height)); }
  public Point FreeSpace(Space allow, Rectangle area)
  { int tries = width*height;
    while(tries-->0)
    { Point pt = new Point(Global.Rand(area.Width)+area.X, Global.Rand(area.Height)+area.Y);
      if(IsFreeSpace(pt, allow)) return pt;
    }
    for(int y=area.Y; y<area.Bottom; y++)
      for(int x=area.X; x<area.Right; x++)
        if(IsFreeSpace(x, y, allow)) return new Point(x, y);
    throw new ApplicationException("No free space found!");
  }
  
  public Point FreeSpaceNear(Point pt)
  { Point test;
    for(int tri=0; tri<50; tri++)
    { int dist = tri/10+1;
      do test = new Point(pt.X+Global.Rand(-dist, dist), pt.Y+Global.Rand(-dist, dist));
      while(test.X==pt.X && test.Y==pt.Y);
      if(IsFreeSpace(test, Space.NoEntities)) return test;
    }
    
    for(int dist=1,max=Math.Max(Math.Max(pt.X, Width-pt.X-1), Math.Max(pt.Y, Height-pt.Y-1)); dist<=max; dist++)
    { for(int x=-dist, y=-dist; x<=dist; x++)
      { test = new Point(pt.X+x, pt.Y+y);
        if(IsFreeSpace(test, Space.NoEntities)) return test;
      }
      for(int x=dist, y=-dist; y<=dist; y++)
      { test = new Point(pt.X+x, pt.Y+y);
        if(IsFreeSpace(test, Space.NoEntities)) return test;
      }
      for(int x=dist, y=dist; x>=-dist; x--)
      { test = new Point(pt.X+x, pt.Y+y);
        if(IsFreeSpace(test, Space.NoEntities)) return test;
      }
      for(int x=-dist, y=dist; y>=dist; y--)
      { test = new Point(pt.X+x, pt.Y+y);
        if(IsFreeSpace(test, Space.NoEntities)) return test;
      }
    }
    throw new ApplicationException("No free space found!");
  }

  public bool IsFreeSpace(Point pt, Space allow) { return IsFreeSpace(pt.X, pt.Y, allow); }
  public bool IsFreeSpace(int x, int y, Space allow)
  { Tile tile = this[x, y];
    if(!IsPassable(tile.Type) || (allow&Space.Items)==0 && tile.Items!=null && tile.Items.Count>0) return false;
    if((allow&Space.Entities)==0)
      for(int i=0; i<entities.Count; i++) if(entities[i].X==x && entities[i].Y==y) return false;
    if((allow&Space.Links)==0)
      for(int i=0; i<links.Length; i++) if(links[i].FromPoint.X==x && links[i].FromPoint.Y==y) return false;
    return true;
  }

  public void MakeNoise(Point pt, Entity source, Noise type, byte volume)
  { if(IsOverworld) return;
    for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Sound=0;
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

  // called when the map is first created (can be overridden for the initial item spawn, etc)
  public virtual void OnInit() { }

  public Point RandomTile(TileType type)
  { int tries = width*height;
    while(tries-->0)
    { int x = Global.Rand(width), y = Global.Rand(height);
      if(map[y, x].Type==type) return new Point(x, y);
    }
    for(int y=0; y<height; y++) for(int x=0; x<width; x++) if(map[y, x].Type==type) return new Point(x, y);
    throw new ArgumentException("No such tile on this map!");
  }
  
  public bool RestockShop(Shop shop)
  { Rectangle area = shop.ItemArea;
    for(int y=area.Y; y<area.Bottom; y++)
      for(int x=area.X; x<area.Right; x++)
        if(!HasItems(x, y))
        { ItemInfo[] arr = objSpawns[(int)shop.Type];
          AddItem(x, y, arr[Global.Rand(arr.Length)].MakeItem()).Shop=shop;
          return true;
        }
    return false;
  }

  public Map RestoreMemory()
  { Map ret = Memory;
    Memory = null;
    return ret;
  }

  public void SaveMemory(Map memory) { Memory = memory; }

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
          Think();
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

  #region SpreadScent
  public unsafe void SpreadScent()
  { if(IsOverworld || width==1 || height==1) return;
    if(scentbuf==null || scentbuf.Length<width*height) scentbuf=new ushort[width*height];
    int wid=width-1, hei=height-1, ye=hei*width, val, n, to;

    fixed(ushort* smap=scentmap) fixed(ushort* sbuf=scentbuf)
    { for(int y=0,yo=0; y<height; yo+=width,y++)
        for(int x=0; x<width; x++)
          if(!IsPassable(map[y,x].Type)) smap[yo+x]=ushort.MaxValue;

      for(int x=1,yo=hei*width; x<wid; x++) // top and bottom edges (excluding corners)
      { to=x;
        if(smap[to]==ushort.MaxValue) { sbuf[to]=0; continue; }
        val=0; n=0;
        if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
        if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
        to += width;
        if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
        if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
        if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
        sbuf[x] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;

        to=x+yo;
        if(smap[to]==ushort.MaxValue) { sbuf[to]=0; continue; }
        val=0; n=0;
        if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
        if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
        to -= width;
        if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
        if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
        if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
        sbuf[x+yo] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
      }

      for(int y=width; y<ye; y+=width) // left and right edges (excluding corners)
      { to=y;
        if(smap[to]==ushort.MaxValue) { sbuf[to]=0; continue; }
        val=0; n=0;
        if(smap[to-width]!=ushort.MaxValue) { val += smap[to-width]; n++; }
        if(smap[to+width]!=ushort.MaxValue) { val += smap[to+width]; n++; }
        to++;
        if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
        if(smap[to-width]!=ushort.MaxValue) { val += smap[to-width]; n++; }
        if(smap[to+width]!=ushort.MaxValue) { val += smap[to+width]; n++; }
        sbuf[y] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;

        to=y+wid;
        if(smap[to]==ushort.MaxValue) { sbuf[to]=0; continue; }
        val=0; n=0;
        if(smap[to-width]!=ushort.MaxValue) { val += smap[to-width]; n++; }
        if(smap[to+width]!=ushort.MaxValue) { val += smap[to+width]; n++; }
        to--;
        if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
        if(smap[to-width]!=ushort.MaxValue) { val += smap[to-width]; n++; }
        if(smap[to+width]!=ushort.MaxValue) { val += smap[to+width]; n++; }
        sbuf[y+wid] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
      }

      if(smap[0]==ushort.MaxValue) sbuf[0]=0; // top-left corner
      else
      { val=0; n=0;
        if(smap[1]!=ushort.MaxValue) { val += smap[1]; n++; }
        if(smap[width]!=ushort.MaxValue) { val += smap[width]; n++; }
        if(smap[width+1]!=ushort.MaxValue) { val += smap[width+1]; n++; }
        sbuf[0] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
      }

      if(smap[wid]==ushort.MaxValue) sbuf[wid]=0; // top-right corner
      else
      { val=0; n=0;
        if(smap[wid-1]!=ushort.MaxValue) { val += smap[wid-1]; n++; }
        if(smap[wid+width]!=ushort.MaxValue) { val += smap[wid+width]; n++; }
        if(smap[wid+wid]!=ushort.MaxValue) { val += smap[wid+wid]; n++; }
        sbuf[wid] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
      }

      if(smap[ye]==ushort.MaxValue) sbuf[ye]=0; // bottom-left corner
      else
      { val=0; n=0;
        if(smap[ye+1]!=ushort.MaxValue) { val += smap[ye+1]; n++; }
        if(smap[ye-width]!=ushort.MaxValue) { val += smap[ye-width]; n++; }
        if(smap[ye-wid]!=ushort.MaxValue) { val += smap[ye-wid]; n++; }
        sbuf[ye] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
      }

      to = ye+wid;
      if(smap[to]==ushort.MaxValue) sbuf[to]=0; // bottom-right corner
      else
      { val=0; n=0;
        if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
        to -= width;
        if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
        if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
        sbuf[ye+wid] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
      }

      for(int yo=width; yo<ye; yo+=width) // the center
        for(int x=1; x<wid; x++)
        { to=yo+x;
          if(smap[to]==ushort.MaxValue) { sbuf[to]=0; continue; }
          val=0; n=0;
          if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
          if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
          to -= width;
          if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
          if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
          if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
          to += width+width;
          if(smap[to]!=ushort.MaxValue) { val += smap[to]; n++; }
          if(smap[to-1]!=ushort.MaxValue) { val += smap[to-1]; n++; }
          if(smap[to+1]!=ushort.MaxValue) { val += smap[to+1]; n++; }
          sbuf[yo+x] = val>3 ? (ushort)Math.Max(val/n-3, 0) : (ushort)0;
        }

      GameLib.Interop.Unsafe.Copy(sbuf, smap, width*height*sizeof(ushort));
    }
  }
  #endregion

  public static bool IsDangerous(TileType type) { return (tileFlag[(int)type]&TileFlag.Dangerous) != TileFlag.None; }
  public static bool IsLink(TileType type) { return (tileFlag[(int)type]&TileFlag.Link) != TileFlag.None; }
  public static bool IsPassable(TileType type) { return (tileFlag[(int)type]&TileFlag.Passable) != TileFlag.None; }
  public static bool IsWall(TileType type) { return (tileFlag[(int)type]&TileFlag.Wall) != TileFlag.None; }
  public static bool IsDoor(TileType type) { return (tileFlag[(int)type]&TileFlag.Door) != TileFlag.None; }
  
  public static Map Load(string name, Dungeon.Section section, int index)
  { if(name.IndexOf('/')==-1) name = "map/"+name;
    if(name.IndexOf('.')==-1) name += ".xml";
    return Load(Global.LoadXml(name), section, index);
  }

  public static Map Load(XmlDocument doc, Dungeon.Section section, int index)
  { XmlElement root = doc.DocumentElement;
    Map map;

    XmlNode node = root.SelectSingleNode("rawMap");
    if(node!=null) // if we have raw map data
    { string[] lines = Xml.BlockToArray(node.InnerText);
      map = new Map(((string)lines[0]).Length, lines.Length);
      for(int y=0; y<map.Height; y++)
      { string line = (string)lines[y];
        for(int x=0; x<map.Width; x++)
        { TileType type;
          #region Select tile type
          switch(line[x])
          { case '*': type=TileType.Border; break;
            case ' ': type=TileType.SolidRock; break;
            case '#': type=TileType.Wall; break;
            case '+': type=TileType.ClosedDoor; break;
            case '-': type=TileType.OpenDoor; break;
            case '.': type=TileType.RoomFloor; break;
            case 'c': type=TileType.Corridor; break;
            case '<': type=TileType.UpStairs; break;
            case '>': type=TileType.DownStairs; break;
            case 'w': type=TileType.ShallowWater; break;
            case 'W': type=TileType.DeepWater; break;
            case 'I': type=TileType.Ice; break;
            case 'L': type=TileType.Lava; break;
            case 'P': type=TileType.Pit; break;
            case 'H': type=TileType.Hole; break;
            case '^': type=TileType.Trap; break;
            case '_': type=TileType.Altar; break;
            case 'T': type=TileType.Tree; break;
            case 'F': type=TileType.Forest; break;
            case 'S': type=TileType.DirtSand; break;
            case 'G': type=TileType.Grass; break;
            case 'm': type=TileType.Hill; break;
            case 'M': type=TileType.Mountain; break;
            case 'R': type=TileType.Road; break;
            case 'o': type=TileType.Town; break;
            case '\\': type=TileType.Portal; break;
            default: throw new ArgumentException(string.Format("Unknown tile type {0} at {1},{2}", line[x], x, y));
          }
          #endregion
          map.SetType(x, y, type);
        }
      }

      map.Section = section;
      map.Index   = index;
    }
    else map = MapGenerator.Generate(root, section, index);

    map.mapID   = root.Attributes["id"].Value;
    map.name    = Xml.Attr(root, "name");
    map.mapType = (MapType)Enum.Parse(typeof(MapType), Xml.Attr(root, "type", "Other"));

    #region Add links
    ListDictionary links = new ListDictionary();
    foreach(XmlNode link in root.SelectNodes("link"))
    { string av=link.Attributes["to"].Value, toSection=null, toDungeon=null;
      int toLevel;

      if(av=="PREV")
      { if(index==0) continue;
        toLevel = index-1;
      }
      else if(av=="NEXT")
      { if(index==section.Depth-1) continue;
        toLevel = index+1;
      }
      else if(char.IsDigit(av[0])) toLevel = int.Parse(av);
      else
      { int pos = av.IndexOf(':');
        if(pos!=-1)
        { toLevel = int.Parse(av.Substring(pos+1));
          av = av.Substring(0, pos);
        }
        else toLevel = 0;
        pos = av.IndexOf('/');
        if(pos==-1) toSection=av;
        else { toSection=av.Substring(pos+1); toDungeon=av.Substring(0, pos); }
      }

      av = link.Attributes["type"].Value;
      TileType type = av=="None" ? TileType.Border : (TileType)Enum.Parse(typeof(TileType), av);
      Point pt = map.FindXmlLocation(link, links);
      Link  li = new Link(pt, type!=TileType.UpStairs,
                          toSection==null ? section : (toDungeon==null ? section.Dungeon :
                                                                       Dungeon.GetDungeon(toDungeon))[toSection],
                          toLevel);
      if(type!=TileType.Border) map.SetType(pt, type);
      map.AddLink(li);
      
      av = Xml.Attr(link, "id");
      if(av!=null) links[av] = li;
    }
    #endregion
    
    foreach(XmlNode npc in root.SelectNodes("npc"))
    { AI ai = AI.MakeNpc(npc);
      ai.Position = map.FindXmlLocation(npc, links);
      map.Entities.Add(ai);
    }
    
    map.OnInit();
    return map;
  }

  public Map Memory;
  public Dungeon.Section Section;
  public int Index, GroupID=-1;

  protected int Age { get { return age; } }
  protected int NumCreatures { get { return numCreatures; } }

  protected virtual void Think()
  { for(int i=0; i<entities.Count; i++) entities[i].ItemThink();
    for(int y=0; y<height; y++)
      for(int x=0; x<width; x++)
      { ItemPile items = map[y,x].Items;
        if(items!=null) for(int i=0; i<items.Count; i++) if(items[i].Think(null)) items.RemoveAt(i--);
      }
    age++;
  }

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
  
  Point FindLink(TileType type)
  { for(int i=0; i<links.Length; i++)
    { Point pt = links[i].FromPoint;
      if(this[pt].Type==type) return pt;
    }
    throw new ApplicationException("link not found: "+type);
  }

  Point FindXmlLocation(XmlNode node, ListDictionary links) { return FindXmlLocation(node, links, false); }
  Point FindXmlLocation(XmlNode node, ListDictionary links, bool optional)
  { string av = Xml.Attr(node, "location");
    if(av!=null)
    { if(char.IsDigit(av[0])) // X,Y format
      { string[] bits = av.Split(',');
        return new Point(int.Parse(bits[0]), int.Parse(bits[1]));
      }
      
      // assume it's the name of a tile type
      return RandomTile((TileType)Enum.Parse(typeof(TileType), av));
    }
    else
    { node = node.SelectSingleNode("location");
      if(node==null)
      { if(optional) return new Point(-1, -1);
        throw new ArgumentException("This node contains no location data");
      }

      Point pt;
      while(true) // TODO: this is not optimal if it has to loop
      { if((av=Xml.Attr(node, "room")) != null)
          pt = FreeSpace(GetRoom(node.Attributes["room"].Value).InnerArea); // room id
        else if((av=Xml.Attr(node, "tile")) != null)
          pt = RandomTile((TileType)Enum.Parse(typeof(TileType), av)); // tile type
        else if((av=Xml.Attr(node, "link")) != null)
          pt = ((Link)links[av]).FromPoint;
        else
        { XmlNode relTo = node.SelectSingleNode("relTo");
          Point rp = FindXmlLocation(relTo, links);

          Range range = new Range(relTo.Attributes["distance"]);
          int amin, amax, tri;
          av = Xml.Attr(relTo, "direction", "Random");
          if(av=="Random") { amin=0; amax=360; }
          else
          { amin = 45 * (int)(Direction)Enum.Parse(typeof(Direction), av) - 22;
            amax = amin + 45;
          }
          
          tryagain:
          for(tri=0; tri<25; tri++)
          { GameLib.Mathematics.TwoD.Vector v = new GameLib.Mathematics.TwoD.Vector(0, -range.RandValue());
            v.Rotate(Global.Rand(amin, amax) * GameLib.Mathematics.MathConst.DegreesToRadians);
            pt = v.ToPoint().ToPoint();
            pt.X += rp.X;
            pt.Y += rp.Y;
            if(IsPassable(pt) && !IsDangerous(pt)) goto found;
          }
          if(amax-amin < 360) { amin=0; amax=360; goto tryagain; }
          continue;
        }

        found:
        av = Xml.Attr(node, "pathTo");
        if(av!=null)
        { PathFinder pf = new PathFinder();
          Point goalPt;
          switch(av)
          { case "UpStairs": goalPt=FindLink(TileType.UpStairs); break;
            case "DownStairs": goalPt=FindLink(TileType.DownStairs); break;
            case "Portal": goalPt=FindLink(TileType.Portal); break;
            default: throw new NotSupportedException("pathTo attribute not supported: "+av);
          }
          pf.Plan(this, pt, goalPt);
        }
        return pt;
      }
    }
  }

  Tile[,] map;
  Link[]  links = new Link[0];
  Room[]  rooms = new Room[0];
  ushort[,] scentmap;
  EntityCollection entities;
  PriorityQueue thinkQueue = new PriorityQueue(new EntityComparer());
  Hashtable removedEntities = new Hashtable();
  string mapID, name;
  int width, height, thinking, timer, age, numCreatures;
  MapType mapType;

  static ushort[] scentbuf;
  static Point[] soundStack;
  static ItemInfo[][] objSpawns;

  [Flags]
  enum TileFlag : byte { None=0, Passable=1, Wall=2, Door=4, Dangerous=8, Link=16 }
  static readonly TileFlag[] tileFlag = new TileFlag[(int)TileType.NumTypes]
  { TileFlag.None,   // Border
    TileFlag.None,   // SolidRock
    TileFlag.Wall,   // Wall
    TileFlag.Door,   // ClosedDoor
    TileFlag.Door|TileFlag.Passable, // OpenDoor
    TileFlag.Passable, TileFlag.Passable, // RoomFloor, Corridor
    TileFlag.Passable|TileFlag.Link, TileFlag.Passable|TileFlag.Link, // stairs (up and down)
    TileFlag.Passable, // ShallowWater
    TileFlag.Passable|TileFlag.Dangerous, // DeepWater
    TileFlag.Passable, // Ice
    TileFlag.Passable|TileFlag.Dangerous, // Lava
    TileFlag.Passable|TileFlag.Dangerous, // Pit
    TileFlag.Passable|TileFlag.Dangerous, // Hole
    TileFlag.Passable|TileFlag.Dangerous, // Trap
    TileFlag.Passable, // Altar
    TileFlag.Passable, // Tree
    TileFlag.Passable, // Forest
    TileFlag.Passable, // DirtSand
    TileFlag.Passable, // Grass
    TileFlag.Passable, // Hill
    TileFlag.Passable, // Mountain
    TileFlag.Passable, // Road
    TileFlag.Passable|TileFlag.Link, // Town
    TileFlag.Passable|TileFlag.Link, // Portal
  };
}
#endregion

} // namespace Chrono
