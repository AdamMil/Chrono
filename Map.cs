using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml;
using AdamMil.Collections;
using Point=System.Drawing.Point;
using Rectangle=System.Drawing.Rectangle;
using Size=System.Drawing.Size;

namespace Chrono
{

#region Enums
public enum MapType { Overworld, Town, Other }

[Flags] public enum TileFlag : byte { None=0, Hidden=1, Locked=2, Seen=4 };

public enum TileType : byte
{ Border,

  SolidRock, Wall, ClosedDoor, OpenDoor, RoomFloor, Corridor, UpStairs, DownStairs,
  ShallowWater, DeepWater, Ice, DeepIce, Lava, Pit, Hole, Altar, HardRock, HardWall,
  
  Tree, Forest, DirtSand, Grass, Hill, Mountain, Road, Town, Portal,

  NumTypes
}
#endregion

#region Link
public struct Link
{ public Link(Point from, Dungeon.Section toSection, int toLevel)
  { ToPoint=new Point(-1, -1);
    FromPoint=from; ToSection=toSection; ToLevel=toLevel;
  }

  public Point FromPoint, ToPoint;
  public Dungeon.Section  ToSection;
  public int  ToLevel;
}
#endregion

#region Map
public sealed class Map
{ // maximum sent on a tile; maximum scent taht can be added at once (maximum entity stench); maximum sound on a tile
  public const int MaxScent=1200, MaxScentAdd=800, MaxSound=255;
  
  [Flags]
  public enum Space
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
      map.OnAdd(c);
      return i;
    }

    public new void AddRange(ICollection entities)
    { base.AddRange(entities);
      foreach(Entity c in entities) map.OnAdd(c);
    }

    public new void Insert(int index, object o) { Insert(index, (Entity)o); }
    public void Insert(int index, Entity c)
    { base.Insert(index, c);
      map.OnAdd(c);
    }

    public void InsertRange(ICollection entities, int index)
    { base.InsertRange(index, entities);
      foreach(Entity c in entities) map.OnAdd(c);
    }

    public new void Remove(object o) { Remove((Entity)o); }
    public void Remove(Entity c)
    { base.Remove(c);
      map.OnRemove(c);
    }

    public new void RemoveAt(int index)
    { Entity c = this[index];
      base.RemoveAt(index);
      map.OnRemove(c);
    }

    public new void RemoveRange(int index, int count)
    { if(index<0 || index>=Count || count<0 || index+count>Count)
        throw new ArgumentOutOfRangeException();
      for(int i=0; i<count; i++) map.OnRemove(this[index+i]);
      base.RemoveRange(index, count);
    }

    protected Map map;
  }
  #endregion

  public Map(Size size) : this(size.Width, size.Height) { }
  public Map(int width, int height) : this(width, height, TileType.SolidRock, false) { }
  public Map(int width, int height, TileType fill, bool seen)
  { Width=width; Height=height;
    map      = new Tile[height, width];
    scentmap = new ushort[height, width];
    Entities = new EntityCollection(this);
    if(fill!=TileType.Border) Fill(fill);
    if(seen) for(int y=0; y<height; y++) for(int x=0; x<width; x++) map[y,x].Flags = TileFlag.Seen;
  }
  
  public Tile this[int x, int y]
  { get { return x<0 || y<0 || x>=Width || y>=Height ? Tile.Border : map[y, x]; }
    set { map[y, x] = value; }
  }

  public Tile this[Point pt]
  { get { return this[pt.X, pt.Y]; }
    set { map[pt.Y, pt.X] = value; }
  }

  public Link[] Links { get { return links; } }
  public Room[] Rooms { get { return rooms; } }

  public Item AddItem(Point pt, Item item) { return AddItem(pt.X, pt.Y, item); }
  public Item AddItem(int x, int y, Item item)
  { ItemPile inv = map[y,x].Items;
    if(inv==null) map[y,x].Items = inv = new ItemPile();
    Item ret = inv.Add(item);
    item.OnPlace(this, new Point(x, y));
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

  public void AddScent(int x, int y, int amount)
  { scentmap[y,x] = (ushort)Math.Min(scentmap[y,x]+Math.Min(amount, MaxScentAdd), MaxScent);
  }

  public void AddShop(Rectangle rect, ShopType type) { AddShop(rect, type, true); }
  public void AddShop(Rectangle rect, ShopType type, bool stock)
  { AddShop(rect, type, new Shopkeeper(), stock);
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
    shop.Shopkeeper.Pos = shop.FrontOfDoor;
    if(stock) RestockShop(shop, true);

    AddRoom(shop);
  }

  public void ClearLinks() { links = new Link[0]; }

  public bool Contains(Point pt) { return pt.X>=0 && pt.X<Width && pt.Y>=0 && pt.Y<Height; }
  public bool Contains(int x, int y) { return x>=0 && x<Width && y>=0 && y<Height; }

  public void Fill(TileType type) { for(int y=0; y<Height; y++) for(int x=0; x<Width; x++) map[y,x].Type=type; }

  public Point FreeSpace() { return FreeSpace(Space.Items, new Rectangle(0, 0, Width, Height)); }
  public Point FreeSpace(Rectangle area) { return FreeSpace(Space.Items, area); }
  public Point FreeSpace(Space allow) { return FreeSpace(allow, new Rectangle(0, 0, Width, Height)); }
  public Point FreeSpace(Space allow, Rectangle area)
  { int tries = Width*Height;
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

  public bool IsFreeSpace(int x, int y, Space allow) { return IsFreeSpace(new Point(x, y), allow); }
  public bool IsFreeSpace(Point pt, Space allow)
  { Tile tile = this[pt.X, pt.Y];
    if(!IsUsuallyPassable(tile.Type) || (allow&Space.Items)==0 && tile.Items!=null && tile.Items.Count>0) return false;
    if((allow&Space.Entities)==0) for(int i=0; i<Entities.Count; i++) if(Entities[i].Pos==pt) return false;
    if((allow&Space.Links)==0) for(int i=0; i<links.Length; i++) if(links[i].FromPoint==pt) return false;
    return true;
  }

  public bool IsDoor(int x, int y) { return IsDoor(this[x, y].Type); }

  public bool IsUsuallyDangerous(Point pt) { return IsUsuallyDangerous(pt.X, pt.Y); }
  public bool IsUsuallyDangerous(int x, int y)
  { return IsUsuallyDangerous(this[x, y].Type); // TODO: check for known traps in the future
  }

  public bool IsUsuallyPassable(Point pt) { return IsUsuallyPassable(this[pt.X, pt.Y].Type); }
  public bool IsUsuallyPassable(int x, int y) { return IsUsuallyPassable(this[x, y].Type); }

  public bool IsWall(int x, int y) { return IsWall(this[x, y].Type); }

  public Entity GetEntity(int x, int y) { return GetEntity(new Point(x, y)); }

  public Entity GetEntity(Point pt)
  { foreach(Entity e in Entities) if(e.Pos==pt) return e;
    return this[pt].Entity;
  }

  public Entity GetEntity(string name)
  { foreach(Entity e in Entities) if(e.Class.Name==name) return e;
    return null;
  }

  public bool GetFlag(Point pt, TileFlag flag) { return this[pt.X, pt.Y].Is(flag); }
  public bool GetFlag(int x, int y, TileFlag flag) { return this[x, y].Is(flag); }
  public void SetFlag(Point pt, TileFlag flag, bool on) { map[pt.Y, pt.X].Set(flag, on); }
  public void SetFlag(int x, int y, TileFlag flag, bool on) { map[y, x].Set(flag, on); }

  public Link GetLink(Point pt) { return GetLink(pt, true); }
  public Link GetLink(Point pt, bool autoGenerate)
  { for(int i=0; i<links.Length; i++) if(links[i].FromPoint==pt) return GetLink(i, autoGenerate);
    throw new ArgumentException("No such link");
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

  public Room GetRoom(Point pt)
  { for(int i=0; i<rooms.Length; i++) if(rooms[i].OuterArea.Contains(pt)) return rooms[i];
    return null;
  }
  public Room GetRoom(string id)
  { for(int i=0; i<rooms.Length; i++) if(rooms[i].Name==id) return rooms[i];
    return null;
  }

  public Shop GetShop(Point pt) { return GetRoom(pt) as Shop; }

  public int GetScent(Point pt) { return GetScent(pt.X, pt.Y); }
  public int GetScent(int x, int y)
  { if(x<0 || x>=Width || y<0 || y>=Height) return 0;
    return scentmap[y, x];
  }

  public bool HasItems(Point pt) { return HasItems(pt.X, pt.Y); }
  public bool HasItems(int x, int y)
  { ItemPile ip = this[x, y].Items;
    return ip!=null && ip.Count!=0;
  }

  public void MakeNoise(Point center, Entity source, Noise type, byte volume)
  { if(Type==MapType.Overworld || volume<=10) return;

    for(int y=0; y<Height; y++) for(int x=0; x<Width; x++) map[y,x].Sound = 0; // clear existing sound values
    if(soundStack==null) soundStack = new Point[256];
    int slen, nslen=1;
    bool changed;

    map[center.Y, center.X].Sound = volume;
    soundStack[0] = center;
    do
    { slen=nslen; changed=false;
      for(int i=0; i<slen; i++) // for each tile under consideration, propogate the sound further
      { Point cp = soundStack[i];
        int  val = map[cp.Y, cp.X].Sound - 10; // decay due to distance. TODO: make the decay exponential
        if(val<=0) continue; // if it has attenuated to nothing, skip it

        for(int d=0; d<8; d++) // if we still have sound left over, propogate it to the surrounding tiles
        { Point np = Global.Move(cp, d);
          Tile   t = this[np];
          int tval = t.Type==TileType.ClosedDoor ? val-60 : val; // sound passes through closed doors, but is muffled

          // if the adjacent tile transmits sound and has a sound value lower than the current one, we need to propogate
          if((IsUsuallyPassable(t.Type) || t.Type==TileType.ClosedDoor) && tval>t.Sound)
          { // if we haven't considered this tile yet and the noise wouldn't be immediately attenutated to nothing
            // next time, add it to the list of tiles under consideration
            if(t.Sound==0 && tval>10) // TODO: change this >10 check when we make distance attenuation exponential
            { if(nslen==soundStack.Length)
              { Point[] narr = new Point[nslen+128];
                Array.Copy(soundStack, narr, nslen);
                soundStack = narr;
              }
              soundStack[nslen++] = np;
            }
            map[np.Y, np.X].Sound = (byte)tval;
            changed = true; // mark that some sound was propogated this iteration
          }
        }
      }
    } while(changed); // while sound continues to propogate

    foreach(Entity e in Entities) // now alert all the entities that are in range of the sound
      if(e!=source) // except for the one that generated it...
      { int vol = map[e.Y, e.X].Sound;
        if(vol>0) e.OnNoise(source, type, vol);
      }
  }

  public Point RandomTile(TileType type)
  { int tries = Width*Height;
    while(tries-->0)
    { int x = Global.Rand(Width), y = Global.Rand(Height);
      if(map[y, x].Type==type) return new Point(x, y);
    }
    for(int y=0; y<Height; y++) for(int x=0; x<Width; x++) if(map[y, x].Type==type) return new Point(x, y);
    throw new ArgumentException("No such tile on this map!");
  }

  public bool RestockShop(Shop shop, bool fullyStock)
  { Rectangle area = shop.ItemArea;
    bool stocked = false;
    for(int y=area.Y; y<area.Bottom; y++)
      for(int x=area.X; x<area.Right; x++)
        if(!HasItems(x, y))
        { AddItem(x, y, new Item(shop.Type.RandItem()));
          if(!fullyStock) return true;
          stocked = true;
        }
    return stocked;
  }

  public void SetType(Point pt, TileType type) { map[pt.Y, pt.X].Type = type; }
  public void SetType(int x, int y, TileType type) { map[y, x].Type = type; }

  /*
    map time is measured in real time, with each tick being equal to 15 seconds.
    a creature with maximum speed (100) would be able to a turn each 1/10 tick (or 1.5 seconds)
    a creature with a speed of 33 would be able to take a turn each 100/33 * 1/10 tick (= 1/3 tick = 5 seconds) 
    
    if an entity is passed in returnAfter, the procedure will return when it becomes that entity's turn.
    this procedure is not generally reentrant, and only one level of reentrancy is allowed when returnAfter is used.
    specifically, the call stack Simulate(foo) -> Simulate(bar) is not guaranteed to work properly,
    and when Simulate(foo) -> Simulate(foo) ultimately returns, two of foo's turns will have elapsed.
  */
  public void Simulate() { Simulate(null); }
  public void Simulate(Entity returnAfter)
  { bool addedReturn = returnAfter==null; // if no entity is passed, the condition is always met (normal operation)

    while(thinkQueue.Count==0 || !addedReturn) // if the think queue is empty, or we need to wait for an entity...
    { timer += 10;
      if(timer>=100)
      { timer -= 100;
        Tick();
      }

      if(Entities.Count==0) return;

      foreach(Entity e in Entities) // add all the entities that will think in a single pass
      { e.Timer += e.Speed;
        if(e.Timer>=100)
        { e.Timer -= 100;
          if(e==returnAfter) addedReturn = true;
          thinkQueue.Enqueue(e);
        }
      }
    }

    thinking++;
    while(!App.IsQuitting && thinkQueue.Count!=0) // while there's stuff left to do
    { Entity e = (Entity)thinkQueue.Dequeue();
      if(removedEntities.Contains(e)) continue; // if the entity is dead, etc, skip it
      if(e==returnAfter) return; // if it's the one we're waiting for, return immediately (before thinking)
      e.Think();
    }
    if(--thinking==0) removedEntities.Clear(); // when we're finally done with a batch, clear this list
  }

  public void Spawn(int index)
  { int count = Global.GetEntityClass(index).GroupSize.RandValue();
    Point pos = FreeSpace();

    for(int i=0; i<count; i++)
    { Entity e = new Entity(index);
      e.Pos = i==0 ? pos : FreeSpaceNear(pos);
      Entities.Add(e);
    }
  }

  public void Tick()
  { foreach(Entity e in Entities) e.Tick();

    ArrayList remove = null;
    for(int y=0; y<Height; y++)
      for(int x=0; x<Width; x++)
      { ItemPile pile = map[y, x].Items;
        if(pile!=null)
        { foreach(Item i in pile)
            if(i.Tick(null, pile))
            { if(remove==null) remove = new ArrayList();
              remove.Add(i);
            }
          if(remove!=null && remove.Count!=0)
          { foreach(Item i in remove) pile.Remove(i);
            remove.Clear();
          }
        }
      }
  }

  public readonly EntityCollection Entities;
  public readonly int Width, Height;
  public Dungeon.Section Section;
  public int Index, SpawnMax, SpawnRate;
  public EntityGroup Spawns;
  public MapType Type;

  public static bool IsDoor(TileType tt) { return tt==TileType.ClosedDoor || tt==TileType.OpenDoor; }

  public static bool IsDownLink(TileType tt)
  { return tt==TileType.DownStairs || tt==TileType.Hole || tt==TileType.Portal;
  }

  public static bool IsLink(TileType tt)
  { return tt==TileType.DownStairs || tt==TileType.Hole || tt==TileType.Portal || tt==TileType.UpStairs;
  }

  public static bool IsWall(TileType tt) { return tt==TileType.Wall || tt==TileType.HardWall; }

  public static bool IsUsuallyDangerous(TileType tt) { return tt==TileType.DeepWater || tt==TileType.Lava; }

  public static bool IsUsuallyPassable(TileType tt)
  { switch(tt)
    { case TileType.Border: case TileType.ClosedDoor: case TileType.DeepWater:
      case TileType.HardRock: case TileType.HardWall: case TileType.Hole:
      case TileType.Lava: case TileType.Mountain: case TileType.Pit:
      case TileType.SolidRock: case TileType.Wall:
        return false;
      default: return true;
    }
  }

  public static bool IsUsuallyRisky(TileType tt)
  { return tt==TileType.DeepIce || tt==TileType.Ice || tt==TileType.ShallowWater;
  }

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
            case 'i': type=TileType.Ice; break;
            case 'I': type=TileType.DeepIce; break;
            case 'L': type=TileType.Lava; break;
            case 'h': type=TileType.HardRock; break;
            case 'H': type=TileType.HardWall; break;
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

    map.mapID = root.Attributes["id"].Value;
    map.name  = Xml.Attr(root, "name");
    map.Type  = (MapType)Enum.Parse(typeof(MapType), Xml.Attr(root, "type", "Other"));

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
      Link  li = new Link(pt, toSection==null ? section : (toDungeon==null ? section.Dungeon
                                                                           : Dungeon.GetDungeon(toDungeon))[toSection],
                          toLevel);
      if(type!=TileType.Border) map.SetType(pt, type);
      map.AddLink(li);
      
      av = Xml.Attr(link, "id");
      if(av!=null) links[av] = li;
    }
    #endregion

    node = root.SelectSingleNode("spawns");
    if(node!=null)
    { if(!Xml.IsEmpty("max")) map.SpawnMax = Xml.Int(node, "min");
      if(!Xml.IsEmpty("rate")) map.SpawnRate = Xml.Int(node, "rate");
      int spawnStart = Xml.IsEmpty(node, "start") ? 15 : Xml.Int(node, "start");
      map.Spawns = new EntityGroup(node);
      while(map.numCreatures<spawnStart) map.Spawn(map.Spawns.NextEntity());
    }

    foreach(XmlNode npc in root.SelectNodes("npc"))
    { Entity e = new Entity(npc);
      e.Pos = map.FindXmlLocation(npc, links);
      map.Entities.Add(e);
    }

    return map;
  }

  Point FindLink(TileType type)
  { for(int i=0; i<links.Length; i++)
    { Point pt = links[i].FromPoint;
      if(this[pt].Type==type) return pt;
    }
    throw new ArgumentException("link not found: "+type);
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
            if(IsUsuallyPassable(pt) && !IsUsuallyDangerous(pt)) goto found;
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

  void OnAdd(Entity e)
  { e.OnMapChange(this);
    numCreatures++;
  }

  void OnRemove(Entity e)
  { e.OnMapChange(null);
    if(thinking!=0) removedEntities.Add(e);
    numCreatures--;
  }

  Tile[,] map;
  Room[] rooms = new Room[0];
  Link[] links = new Link[0];
  ushort[,] scentmap;
  string mapID, name;
  Queue thinkQueue = new Queue();
  ArrayList removedEntities = new ArrayList();
  int timer, thinking, numCreatures;

  static Point[] soundStack;
}
#endregion

#region Pathfinding
public struct PathNode
{ public enum State : byte { New, Open, Closed };

  public Point Parent;
  public int   Base, Cost;
  public State Type;
  public byte  Length;
}

public sealed class PathFinder
{ public PathFinder() { queue = new PriorityQueue<Point>(new NodeComparer(this)); }

  public const int BadPathCost = 1000;

  public PathNode GetPathNode(Point pt) { return nodes[pt.Y, pt.X]; }

  public bool Plan(Map map, Point start, Point goal)
  { queue.Clear();
    if(!map.Contains(start) || !map.Contains(goal)) return false;

    if(nodes==null || nodes.GetLength(0)!=map.Height || nodes.GetLength(1)!=map.Width)
      nodes = new PathNode[map.Height, map.Width];
    else Array.Clear(nodes, 0, map.Width*map.Height);

    this.map = map;
    Insert(goal); // work backwards

    byte maxlen = (byte)Math.Min(Math.Max(map.Width, map.Height)*2, 255);
    while(queue.Count>0)
    { Point nodePt = queue.Dequeue();
      if(nodePt==start) { queue.Clear(); return true; } // work backwards
      PathNode node = nodes[nodePt.Y, nodePt.X];
      if(node.Length==maxlen) break;
      nodes[nodePt.Y, nodePt.X].Type = PathNode.State.Closed;

      for(int yi=-1; yi<=1; yi++)
        for(int xi=-1; xi<=1; xi++)
        { if(xi==0 && yi==0) continue;
          Point neiPt = new Point(nodePt.X+xi, nodePt.Y+yi);
          if(!map.Contains(neiPt)) continue;
          PathNode nei = nodes[neiPt.Y, neiPt.X];
          int move=MoveCost(neiPt), bcost=node.Base+move,
              heur=Math.Max(Math.Abs(neiPt.X-goal.X), Math.Abs(neiPt.Y-goal.Y))/8,
              cost=bcost + (move>2 ? heur : heur/4);
          if(nei.Type!=PathNode.State.New && cost>=nei.Cost) continue;
          nei.Parent = nodePt;
          nei.Base   = bcost;
          nei.Cost   = cost;
          nei.Length = (byte)(node.Length+1);
          nodes[neiPt.Y, neiPt.X] = nei;
          Insert(neiPt);
        }
    }
    queue.Clear();
    return false;
  }

  void Insert(Point pt)
  { queue.Enqueue(pt);
    nodes[pt.Y, pt.X].Type = PathNode.State.Open;
  }

  // TODO: we should have the ability to take into account an entity's abilities. eg, normally passable land is
  // deadly for a fish, but deep water is just what we want.
  int MoveCost(Point pt)
  { TileType type = map[pt].Type;
    int cost = map.IsUsuallyDangerous(pt)   ? 2000 : // dangerous tiles
               Map.IsUsuallyRisky(type)     ? 10   : // risky tiles (eg, ice, shallow water)
               Map.IsUsuallyPassable(type)  ? 1    : // freely passable tiles
               Map.IsDoor(type) && !map.GetFlag(pt, TileFlag.Locked) ? 2 : // closed, unlocked doors
               map.GetFlag(pt, TileFlag.Seen) ? 10000 : // known unpassable areas
               13; // unknown areas
    if(Map.IsUsuallyPassable(type) && map.GetEntity(pt)!=null) cost += 10; // entity in the way
    return cost;
  }

  sealed class NodeComparer : IComparer<Point>
  { public NodeComparer(PathFinder pf) { this.pf=pf; }

    public int Compare(Point a, Point b)
    { 
      int n = pf.nodes[a.Y, a.X].Cost - pf.nodes[b.Y, b.X].Cost;
      if(n==0)
      { n = a.X - b.X;
        if(n==0) n = a.Y - b.Y;
      }

      return n;
    }
    
    PathFinder pf;
  }

  PriorityQueue<Point> queue;
  Map map;
  PathNode[,] nodes;
}
#endregion

#region Room
public class Room
{ public Room(Rectangle area, string name) { OuterArea=area; Name=name; }
  
  public Rectangle InnerArea { get { Rectangle r = OuterArea; r.Inflate(-1, -1); return r; } }

  public Rectangle OuterArea;
  public string Name;
}
#endregion

#region Shop
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

  public bool Accepts(Item item) { return Type.Accepts(item.Class); }
  public bool Accepts(ItemClass ic) { return Type.Accepts(ic); }

  public Point      Door;
  public Direction  DoorSide;
  public Shopkeeper Shopkeeper;
  public ShopType   Type;
}
#endregion

#region ShopType
public sealed class ShopType
{ ShopType(XmlNode node)
  { ID        = Xml.Attr(node, "id");
    Name      = Xml.Attr(node, "name");
    Chance    = Xml.Int(node, "chance");
    SizeLimit = Xml.Int(node, "sizeLimit", -1);

    XmlNodeList list = node.SelectNodes("item");
    items = new RoomItem[list.Count];
    int total = 0;
    for(int i=0; i<items.Length; i++)
    { XmlNode itemnode = list[i];
      int chance = Xml.Int(itemnode, "chance");
      string s = Xml.Attr(itemnode, "type");
      if(Xml.IsEmpty(s)) items[i] = new RoomItem(Global.GetItemIndex(Xml.Attr(itemnode, "class")), chance);
      else items[i] = new RoomItem((ItemType)Enum.Parse(typeof(ItemType), s), chance);

      total += chance;
    }
    if(total!=100) throw new ApplicationException("Chances for room type "+ID+"'s items don't add up to 100");
  }

  public bool Accepts(ItemClass ic)
  { foreach(RoomItem ri in items)
      if(ri.Type==ic.Type || ri.Class==ic.Index || ri.Type==ItemType.Any && ri.Class==-1) return true;
    return false;
  }

  public bool IsTooBig(int size) { return SizeLimit!=-1 && size>SizeLimit; }

  public int RandItem()
  { int left = Global.Rand(100);
    while(true)
    { RoomItem ri = items[itemIndex];
      if(++itemIndex==items.Length) itemIndex = 0;
      left -= ri.Chance;
      if(left<0)
        return ri.Type==ItemType.Any ? ri.Class==-1 ? Global.RandItem() : ri.Class : Global.RandItem(ri.Type);
    }
  }

  public readonly string ID, Name;
  public readonly int Chance, SizeLimit;

  RoomItem[] items;
  int itemIndex;

  static ShopType()
  { XmlDocument  doc = Global.LoadXml("shops.xml");
    XmlNodeList  rts = doc.SelectNodes("//shopType");
    ShopType[] types = new ShopType[rts.Count];

    int total = 0;
    bool needFallback = false;
    for(int i=0; i<types.Length; i++)
    { types[i] = new ShopType(rts[i]);
      total += types[i].Chance;
      if(types[i].IsTooBig(int.MaxValue)) needFallback = true;
    }

    if(total!=100) throw new ApplicationException("Room type chances don't add up to 100");
    
    ShopType.types = new SortedList(types.Length);
    for(int i=0; i<types.Length; i++) ShopType.types[types[i].ID] = types[i];
    
    string fb = Xml.Attr(doc.DocumentElement, "fallbackType");
    if(Xml.IsEmpty(fb))
    { if(needFallback) throw new ApplicationException("Fallback type needed, but not found");
      fallback = -1;
    }
    else
    { fallback = ShopType.types.IndexOfKey(fb);
      if(fallback==-1) throw new ApplicationException("Fallback type not found: "+fb);
    }
  }
  
  public static ShopType Get(string id)
  { ShopType st = (ShopType)types[id];
    if(st==null) throw new ArgumentException("No such shop type: "+id);
    return st;
  }

  public static ShopType GetRandom(int numSquares)
  { int left = Global.Rand(100);
    while(true)
    { ShopType st = (ShopType)types.GetByIndex(typeIndex);
      if(++typeIndex==types.Count) typeIndex = 0;
      left -= st.Chance;
      if(left<0) return st.IsTooBig(numSquares) ? (ShopType)types.GetByIndex(fallback) : st;
    }
  }

  struct RoomItem
  { public RoomItem(ItemType type, int chance) { Type=type; Chance=(byte)chance; Class=-1; }
    public RoomItem(int itemClass, int chance) { Class=itemClass; Chance=(byte)chance; Type=ItemType.Any; }

    public readonly int Class;
    public readonly ItemType Type;
    public readonly byte Chance;
  }

  static SortedList types;
  static int typeIndex, fallback;
}
#endregion

#region Tile
public struct Tile
{ public bool Is(TileFlag flag) { return (Flags&flag)!=0; }
  public void Set(TileFlag flag, bool on) { if(on) Flags|=flag; else Flags&=~flag; }

  public ItemPile  Items;
  public Entity    Entity;   // in memory, creature on tile. in map, owner of trap.
  public TileType  Type;
  public TileFlag  Flags;
  public byte      Subtype;  // subtype of tile (ie, type of trap/altar/etc)
  public byte      Sound;

  public static readonly Tile Border = new Tile();
}
#endregion

} // namespace Chrono