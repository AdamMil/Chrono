using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using GameLib.Collections;

namespace Chrono
{

#region Types and Enums
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

[Serializable]
public class Shop
{ public Shop(Rectangle area, Shopkeeper shopkeeper, ShopType type) { Area=area; Shopkeeper=shopkeeper; Type=type; }
  public Rectangle Area;
  public Shopkeeper Shopkeeper;
  public ShopType Type;
}

[Serializable]
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

[Serializable]
public struct Link
{ public Link(Point from, bool down)
  { FromPoint=from; ToPoint=new Point(-1, -1); ToDungeon=null; ToLevel=-1; Down=down;
  }
  public Link(Point from, MapCollection to, bool down)
  { FromPoint=from; ToPoint=new Point(-1, -1); ToDungeon=to; ToLevel=0; Down=down;
  }
  public Link(Point from, MapCollection to, int level, bool down)
  { FromPoint=from; ToPoint=new Point(-1, -1); ToDungeon=to; ToLevel=level; Down=down;
  }
  public Point FromPoint, ToPoint;
  public MapCollection ToDungeon;
  public int  ToLevel;
  public bool Down;
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
#endregion

#region Map
[Serializable]
public class Map : UniqueObject
{ // maximum scent on a tile, maximum scent add on a single call (maximum entity smelliness), maximum sound on a tile
  public const int MaxScent=1200, MaxScentAdd=800, MaxSound=255;
  
  [Flags] public enum Space
  { None=0, Items=1, Entities=2, Links=4, All=Items|Entities|Links,
    NoItems=All&~Items, NoEntities=All&~Entities, NoLinks=All&~Links
  };

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
  public Map(SerializationInfo info, StreamingContext context) : base(info, context) { }

  static Map()
  { Type[] types = typeof(Item).Assembly.GetTypes();
    ArrayList[] lists = new ArrayList[(int)ShopType.NumTypes];
    for(int i=0; i<lists.Length; i++) lists[i] = new ArrayList();
    foreach(Type t in types)
    { if(!t.IsAbstract && t.IsSerializable && t.IsSubclassOf(typeof(Item)))
      { FieldInfo f = t.GetField("ShopValue", BindingFlags.Public|BindingFlags.Static);
        if(f==null || (int)f.GetValue(null)<=0) continue;
        SpawnInfo si = new SpawnInfo(t);

        Item i = (Item)t.GetConstructor(Type.EmptyTypes).Invoke(null);
        lists[(int)ShopType.General].Add(si);

        switch(i.Class)
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
    }
    objSpawns = new SpawnInfo[lists.Length][];
    for(int i=0; i<lists.Length; i++) objSpawns[i] = (SpawnInfo[])lists[i].ToArray(typeof(SpawnInfo));
  }

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

  public void AddShop(Rectangle rect, ShopType type) { AddShop(rect, type, true); }
  public void AddShop(Rectangle rect, ShopType type, bool stock)
  { AddShop(rect, type, (Shopkeeper)Entity.Generate(typeof(Shopkeeper), Global.Rand(3), EntityClass.Fighter), stock);
  }
  public void AddShop(Rectangle rect, ShopType type, Shopkeeper shopkeeper, bool stock)
  { Shop[] narr = new Shop[shops==null ? 1 : shops.Length+1];
    if(narr.Length!=1) Array.Copy(shops, narr, narr.Length-1);
    shops = narr;
    Shop shop = narr[narr.Length-1] = new Shop(rect, shopkeeper, type);
    if(stock) while(RestockShop(shop));
    shopkeeper.SetShop(shop);
  }

  public void ClearLinks() { links = new Link[0]; }

  public void AddScent(int x, int y, int amount)
  { if(Index==(int)Overworld.Place.Overworld && Dungeon is Overworld) return;
    scentmap[y,x] = (ushort)Math.Min(scentmap[y,x]+Math.Min(amount, MaxScentAdd), MaxScent);
  }

  public bool Contains(int x, int y) { return y>=0 && y<height && x>=0 && x<width; }

  public void Fill(TileType type) { for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Type=type; }

  public Entity GetEntity(Point pt)
  { for(int i=0; i<entities.Count; i++) if(entities[i].Position==pt) return entities[i];
    return null;
  }
  public Entity GetEntity(int x, int y) { return GetEntity(new Point(x, y)); }

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
    { Map nm = links[index].ToDungeon[links[index].ToLevel];
      for(int ml=0,ol=0; ml<links.Length; ml++)    // initialize all links going to the same level
      { if(links[ml].ToLevel!=nm.Index || links[ml].ToDungeon!=nm.Dungeon) continue; // skip ones going elsewhere
        while(ol<nm.links.Length && nm.links[ol].ToLevel!=Index) ol++;
        if(ol==nm.Links.Length) { links[ml].ToPoint = new Point(); continue; }
        links[ml].ToPoint = nm.links[ol].FromPoint;
        nm.links[ol].ToPoint = links[ml].FromPoint;
      }
    }
    return links[index];
  }
  
  public int GetScent(Point pt) { return GetScent(pt.X, pt.Y); }
  public int GetScent(int x, int y)
  { if(x<0 || x>=width || y<0 || y>=height) return 0;
    return scentmap[y,x];
  }

  public Shop GetShop(int x, int y) { return GetShop(new Point(x, y)); }
  public Shop GetShop(Point pt)
  { if(shops==null) return null;
    for(int i=0; i<shops.Length; i++) if(shops[i].Area.Contains(pt)) return shops[i];
    return null;
  }

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

  public Point FreeSpace() { return FreeSpace(Space.Items); }
  public Point FreeSpace(Space allow)
  { int tries = width*height;
    while(tries-->0)
    { Point pt = new Point(Global.Rand(width), Global.Rand(height));
      if(!IsFreeSpace(pt, allow)) continue;
      return pt;
    }
    for(int y=0; y<height; y++)
      for(int x=0; x<width; x++)
        if(IsFreeSpace(x, y, allow)) return new Point(x, y);
    throw new ArgumentException("No free space found on this map!");
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
  { if(Index==(int)Overworld.Place.Overworld && Dungeon is Overworld) return;
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
  { if(shop.Shopkeeper==null) return false;
    if(shop.Shopkeeper.HP<=0) { shop.Shopkeeper=null; return false; }

    for(int y=shop.Area.Y; y<shop.Area.Bottom; y++)
      for(int x=shop.Area.X; x<shop.Area.Right; x++)
        if(!HasItems(x, y))
        { SpawnInfo[] arr = objSpawns[(int)shop.Type];
          AddItem(x, y, Global.SpawnItem(arr[Global.Rand(arr.Length)]));
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
  { if(Index==(int)Overworld.Place.Overworld && Dungeon is Overworld || width==1 || height==1) return;
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
  
  public static Map Load(Stream stream)
  { StreamReader sr = new StreamReader(stream, System.Text.Encoding.ASCII);
    ArrayList lines = new ArrayList();
    string line;
    while((line=sr.ReadLine())!=null && line!="--BEGIN MAP--");
    while((line=sr.ReadLine())!=null && line!="--END MAP--") lines.Add(line);

    Map map = new Map(((string)lines[0]).Length, lines.Count);
    for(int y=0; y<map.Height; y++)
    { line = (string)lines[y];
      for(int x=0; x<map.Width; x++)
      { TileType type;
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
        map.SetType(x, y, type);
      }
    }
    return map;
  }

  public Map Memory;
  public MapCollection Dungeon;
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
  Shop[]  shops;
  ushort[,] scentmap;
  EntityCollection entities;
  PriorityQueue thinkQueue = new PriorityQueue(new EntityComparer());
  Hashtable removedEntities = new Hashtable();
  int width, height, thinking, timer, age, numCreatures;

  static ushort[] scentbuf;
  static Point[] soundStack;
  static SpawnInfo[][] objSpawns;

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

[Serializable]
public class TestMap : Map
{ public TestMap(int width, int height) : base(width, height) { }
  public TestMap(Size size) : base(size) { }
  public TestMap(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override void OnInit()
  { int max = Width*Height/250, min = Width*Height/500;
    for(int i=0,num=Global.Rand(max-min)+min+2; i<num; i++) SpawnItem();
    for(int i=0,num=Global.Rand(max-min)+min; i<num; i++) SpawnMonster();
  }

  protected override void Think()
  { base.Think();
    int level = App.Player.Map.Index;
    if(NumCreatures<50 && (Index==level && Age%75==0 || Index!=level && Age%150==0)) SpawnMonster();
  }

  Item SpawnItem()
  { Item item = Global.SpawnItem(Global.NextSpawn());
    AddItem(FreeSpace(Map.Space.All), item);
    return item;
  }

  void SpawnMonster()
  { int idx = Entities.Add(Entity.Generate(typeof(Orc), Index+1, EntityClass.Fighter));
    for(int i=0; i<10; i++)
    { Entities[idx].Position = FreeSpace();
      if(App.Player==null || !App.Player.CanSee(Entities[idx])) break;
    }
  }
}

} // namespace Chrono
