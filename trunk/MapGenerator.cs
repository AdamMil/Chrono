using System;
using System.Collections;
using System.Drawing;
using System.Xml;

namespace Chrono
{

public class UnableToGenerateException : ApplicationException
{ public UnableToGenerateException(string message) : base(message) { }
}

public abstract class MapGenerator
{ public virtual Size DefaultSize { get { return new Size(60, 60); } }

  public static Map Generate(XmlNode root, Dungeon.Section section, int index)
  { Type type = Type.GetType("Chrono."+root.Attributes["generator"].Value);
    if(type==null) throw new UnableToGenerateException("No such generator "+root.Attributes["generator"].Value);
    MapGenerator gen = (MapGenerator)type.GetConstructor(Type.EmptyTypes).Invoke(null);
    
    Map map;
    XmlNode opts = root.SelectSingleNode("generatorOptions");
    if(opts==null)
    { gen.Rand = new Random();
      map = new Map(gen.DefaultSize);
    }
    else
    { XmlAttribute attr = opts.Attributes["seed"];
      gen.Rand    = attr==null ? new Random() : new Random(int.Parse(attr.Value));
      Size size   = gen.DefaultSize;
      size.Width  = Xml.RangeInt(opts.Attributes["width"], size.Width);
      size.Height = Xml.RangeInt(opts.Attributes["height"], size.Height);
      map = new Map(size);
    }

    map.Section = section;
    map.Index   = index;
    
    gen.Generate(map, root);
    return map;
  }

  protected abstract void Generate(Map map, XmlNode root);

  protected Random Rand;
}

#region RoomyMapGenerator
public class RoomyMapGenerator : MapGenerator
{ protected override void Generate(Map map, XmlNode root)
  { this.map = map;
    int minRooms, maxRooms;

    { int size = map.Width*map.Height;
      minRooms = Math.Max(size/900, 1);
      maxRooms = Math.Max(size/300, 1);
      maxRoomSize = new Size(20, 20);
    }

    XmlNode opts = root.SelectSingleNode("generatorOptions");
    if(opts!=null)
    { maxRoomSize.Width  = Xml.RangeInt(opts.Attributes["maxRoomWidth"], maxRoomSize.Width);
      maxRoomSize.Height = Xml.RangeInt(opts.Attributes["maxRoomHeight"], maxRoomSize.Height);
      minRooms = Xml.IntValue(opts.Attributes["minRooms"], minRooms);
      maxRooms = Xml.IntValue(opts.Attributes["maxRooms"], maxRooms);
    }

    rooms.Clear();
    foreach(XmlNode room in root.SelectNodes("room[@required=true]"))
      if(!AddRoom(room)) throw new UnableToGenerateException("Couldn't add required rooms.");
    foreach(XmlNode room in root.SelectNodes("room[@required!=true]")) if(!AddRoom(room)) break;

    while(rooms.Count<minRooms) if(!AddRoom()) throw new UnableToGenerateException("Couldn't add enough rooms.");
    while(rooms.Count<maxRooms) if(!AddRoom()) break;

    for(int i=1; i<rooms.Count; i++) Connect((Room)rooms[i-1], (Room)rooms[i]);

    if(map.Index>0) AddStairs(false);
    if(map.Index<map.Section.Depth-1) AddStairs(true);
  }

  struct Room
  { public Room(Rectangle area) { Area=area; Connected=false; }
    public Rectangle Area;
    public bool Connected;
  }

  bool AddRoom() { return AddRoom(null); }
  bool AddRoom(XmlNode room)
  { Rectangle area;
    if(FindRoom(room, out area))
    { DigRoom(area);
      Room r = new Room(area);
      rooms.Add(r);
      map.AddRoom(r.Area, Xml.Attr(room, "id"));
      return true;
    }
    return false;
  }

  void AddStairs(bool down)
  { Point point = map.RandomTile(TileType.RoomFloor);
    map.SetType(point, down ? TileType.DownStairs : TileType.UpStairs);
    map.AddLink(new Link(point, down, map.Section, map.Index + (down ? 1 : -1)));
  }

  bool FindRoom(XmlNode room, out Rectangle area)
  { int tri, r;
    Size min=new Size(4, 4), max=maxRoomSize;

    if(room!=null)
    { int mw=min.Width, mh=min.Height, Mw=max.Width, Mh=max.Height;
      Xml.Range(room.Attributes["width"], ref mw, ref Mw);
      Xml.Range(room.Attributes["height"], ref mh, ref Mh);
      min = new Size(mw, mh); max = new Size(Mw, Mh);
    }

    area = new Rectangle();
    for(tri=0; tri<50; tri++)
    { area = new Rectangle(Rand.Next(map.Width-min.Width), Rand.Next(map.Height-min.Height),
                           Rand.Next(min.Width, max.Width), Rand.Next(min.Height, max.Height));
      if(area.Right>map.Width || area.Bottom>map.Height) continue; // size/location
      if(area.Height*3<=area.Width || area.Width*3<=area.Height) continue; // aspect ratio
      Rectangle bounds = area; bounds.Inflate(2, 2);
      for(r=0; r<rooms.Count; r++) if(bounds.IntersectsWith(((Room)rooms[r]).Area)) break;
      if(r==rooms.Count) break;
    }
    return tri<50;
  }

  bool Connect(Room r1, Room r2)
  { Point  p1 = RectCenter(r1.Area), p2 = RectCenter(r2.Area), save = new Point();
    int  fail = 0;
    bool  rnd = false;
    while(rnd || p1 != p2)
    { Direction dir;
      if(rnd) { p2 = save; rnd = false; }
      else if(Rand.Next(10)==0)
      { save = p2;
        p2   = new Point(p1.X+Rand.Next(-1, 1), p1.Y+Rand.Next(-1, 1));
        rnd  = true;
      }
      int xd = p2.X-p1.X, yd = p2.Y-p1.Y;
      if(Math.Abs(xd)>Math.Abs(yd)) dir = xd<0 ? Direction.Left : Direction.Right;
      else dir = yd<0 ? Direction.Up : Direction.Down;
      if(!TryDig(ref p1, p2, dir))
      { if(++fail==2) return false;
        if(rnd) continue;
        Point temp = p1; p1=p2; p2=temp;
      }
      else fail=0;
    }
    r1.Connected = r2.Connected = true;
    return true;
  }

  void DigRoom(Rectangle area)
  { for(int x=area.X; x<area.Right; x++)
    { map.SetType(x, area.Top, TileType.Wall);
      map.SetType(x, area.Bottom-1, TileType.Wall);
    }
    for(int y=area.Y+1; y<area.Bottom-1; y++)
    { map.SetType(area.Left, y, TileType.Wall);
      for(int x=area.X+1; x<area.Right-1; x++) map.SetType(x, y, TileType.RoomFloor);
      map.SetType(area.Right-1, y, TileType.Wall);
    }
  }

  bool TryDig(ref Point p1, Point p2, Direction dir)
  { int x = p1.X+Global.DirMap[(int)dir].X, y = p1.Y+Global.DirMap[(int)dir].Y;
    if(!map.Contains(x, y)) return false;
    if(!CanDig(x, y))
    { int x2=p1.X, y2=p1.Y;
      if(dir==Direction.Left || dir==Direction.Right)
      { if(p1.Y < p2.Y)
        { if(y2<map.Height-1 && CanDig(x2, y2+1)) y2++;
          else goto Failed;
        }
        else if(y2>0 && CanDig(x2, y2-1)) y2--;
        else goto Failed;
      }
      else // up/down
      { if(p1.X < p2.X)
        { if(x2<map.Width-1 && CanDig(x2+1, y2)) x2++;
          else goto Failed;
        }
        else if(x2>0 && CanDig(x2-1, y2)) x2--;
        else goto Failed;
      }
      x=x2; y=y2;
      goto Success;
      Failed:
      if(dir==Direction.Left || dir==Direction.Right)
      { if(p1.Y < p2.Y)
        { if(y<map.Height-1 && CanDig(x, y+1)) y++;
          else return false;
        }
        else if(y>0 && CanDig(x, y-1)) y--;
        else return false;
      }
      else // up/down
      { if(p1.X < p2.X)
        { if(x<map.Width-1 && CanDig(x+1, y)) x++;
          else return false;
        }
        else if(x>0 && CanDig(x-1, y)) x--;
        else return false;
      }
    }
    Success:
    Dig(x, y);
    p1.X=x; p1.Y=y;
    return true;
  }

  bool CanDig(int x, int y)
  { if(map.IsPassable(x, y)) return true;
    return !map.IsWall(x, y) || !IsWallJunction(x, y) && !HasAdjacentDoor(x, y, true);
  }

  void Dig(int x, int y)
  { if(map.IsPassable(x, y) || map.IsDoor(x, y)) return;
    if(map.IsWall(x, y))
    { map.SetType(x, y, TileType.ClosedDoor);
      if(Rand.Next(100)<8) map.SetFlag(x, y, Tile.Flag.Locked, true);
    }
    else map.SetType(x, y, TileType.Corridor);
  }

  bool IsWallJunction(int x, int y)
  { return map.IsWall(x, y) &&
           (map.IsWall(x-1, y) || map.IsWall(x+1, y)) &&
           (map.IsWall(x, y-1) || map.IsWall(x, y+1));
  }

  bool HasAdjacentDoor(int x, int y, bool fourWay)
  { if(fourWay)
      return map.IsDoor(x-1, y) || map.IsDoor(x, y-1) || map.IsDoor(x+1, y) || map.IsDoor(x, y+1);
    else
      for(int y2=y-1; y2<=y+1; y2++)
        for(int x2=x-1; x2<=x+1; x2++)
          if(map.IsDoor(x2, y2) && (x2!=x || y2!=y)) return true;
    return false;
  }

  static Point RectCenter(Rectangle rect) { return new Point(rect.X+rect.Width/2, rect.Y+rect.Height/2); }

  Map map;
  ArrayList rooms = new ArrayList();
  Size maxRoomSize;
}
#endregion

#region MetaCaveGenerator
public class MetaCaveGenerator : MapGenerator
{ protected override void Generate(Map map, XmlNode root)
  { int ncircles = Xml.IntValue(Xml.AttrNode(root.SelectSingleNode("generatorOptions"), "circles"), 50);

    Point[] centers = new Point[ncircles];
    PathFinder path = new PathFinder();
    int width = map.Width, height = map.Height;

    while(true)
    { for(int i=0; i<ncircles; i++) centers[i] = new Point(Global.Rand(width-8)+4, Global.Rand(height-8)+4);

      for(int y=0; y<height; y++)
        for(int x=0; x<width; x++)
        { double sum=0;
          for(int i=0; i<ncircles; i++)
          { int xd=x-centers[i].X, yd=y-centers[i].Y;
            sum += 1.0/(xd*xd+yd*yd);
          }
          map.SetType(x, y, sum>0.2 ? TileType.RoomFloor : TileType.Wall);
        }
      for(int x=0; x<width; x++)
      { map.SetType(x, 0, TileType.Wall);
        map.SetType(x, height-1, TileType.Wall);
      }
      for(int y=0; y<height; y++)
      { map.SetType(0, y, TileType.Wall);
        map.SetType(width-1, y, TileType.Wall);
      }
        
      if(map.Index==0) AddStairs(map, true);
      else if(map.Index>=map.Section.Depth) AddStairs(map, false);
      else
      { Point up=AddStairs(map, false), down=AddStairs(map, true);
        if(!path.Plan(map, up, down) || path.GetPathFrom(up).Cost>=1000)
        { map.ClearLinks();
          map.Fill(TileType.SolidRock);
          continue;
        }
      }
      break;
    }
  }

  Point AddStairs(Map map, bool down)
  { Point point = map.RandomTile(TileType.RoomFloor);
    map.SetType(point, down ? TileType.DownStairs : TileType.UpStairs);
    map.AddLink(new Link(point, down, map.Section, map.Index + (down ? 1 : -1)));
    return point;
  }
}
#endregion

#region TownGenerator
public class TownGenerator : MapGenerator
{ public override Size DefaultSize { get { return new Size(100, 60); } }

  protected override void Generate(Map map, XmlNode root)
  { this.map = map;
    if(map.GroupID==-1) map.GroupID = Global.NewSocialGroup(false, true);

    map.Fill(TileType.Grass);
    int size = map.Width*map.Height;
    for(int ntrees=size/50; ntrees>0; ntrees--) map.SetType(map.FreeSpace(), TileType.Tree);

    foreach(XmlNode room in root.SelectNodes("room[@required='true']"))
      if(!AddRoom(room)) throw new UnableToGenerateException("Couldn't add required rooms.");
    foreach(XmlNode room in root.SelectNodes("room[@required!='true']")) if(!AddRoom(room)) break;

    AddShop(ShopType.General);
    AddShop(ShopType.Food);
    AddShop(ShopType.ArmorWeapons);
    AddShop(ShopType.Magic);

    while(AddRandomRoom());
    
    // TODO: this could cause problems because of its randomness... (eg, a room may fill up with people and then
    // FreeSpace(Rectangle) may fail, or something similar)
    for(int npeople=size/200; npeople>0; npeople--)
    { Entity peon = Entity.Generate(typeof(Townsperson), Global.Rand(0, 3), EntityClass.Plain);
      peon.Position = map.FreeSpace();
      peon.SocialGroup = map.GroupID;
      map.Entities.Add(peon);
    }
  }

  bool AddRoom(XmlNode room)
  { Size min=new Size(5, 5), max=new Size(12, 12);
    int mw=min.Width, mh=min.Height, Mw=max.Width, Mh=max.Height;
    Xml.Range(room.Attributes["width"], ref mw, ref Mw);
    Xml.Range(room.Attributes["height"], ref mh, ref Mh);
    min = new Size(mw, mh); max = new Size(Mw, Mh);

    Rectangle rect;
    if(DigRoom(min, max, out rect))
    { map.AddRoom(rect, Xml.Attr(room, "id"));
      return true;
    }
    return false;
  }

  bool AddRandomRoom()
  { Rectangle rect;
    if(Rand.Next(8)==7) // 1 in 8 buildings are shops
    { if(!DigRoom(5, 10, out rect)) return false;
      map.AddShop(rect, (ShopType)Rand.Next((int)ShopType.NumTypes));
    }
    else
    { if(!DigRoom(5, 12, out rect)) return false;
      map.AddRoom(rect);
    }
    return true;
  }

  void AddShop(ShopType type)
  { Rectangle rect;
    if(!DigRoom(5, 10, out rect)) throw new UnableToGenerateException("Couldn't add enough shops");
    map.AddShop(rect, type);
  }

  bool DigRoom(int min, int max, out Rectangle rect)
  { return DigRoom(new Size(min, min), new Size(max, max), out rect);
  }
  bool DigRoom(Size min, Size max, out Rectangle rect)
  { Rectangle bounds;
    rect = bounds = new Rectangle();

    int tri, r;
    for(tri=0; tri<50; tri++)
    { rect = new Rectangle(Rand.Next(map.Width-min.Width)+2, Rand.Next(map.Height-min.Height)+2,
                           Rand.Next(min.Width, max.Width), Rand.Next(min.Height, max.Height));
      if(rect.Right>map.Width-2 || rect.Bottom>map.Height-2) continue; // size/location
      if(rect.Height*3<=rect.Width || rect.Width*3<=rect.Height) continue; // aspect ratio
      bounds = rect; bounds.Inflate(3, 2);
      for(r=0; r<map.Rooms.Length; r++) if(bounds.IntersectsWith(map.Rooms[r].OuterArea)) break; // spacing
      if(r==map.Rooms.Length) break;
    }
    if(tri==50) return false;

    for(int x=rect.X; x<rect.Right; x++)
    { map.SetType(x, rect.Top, TileType.Wall);
      map.SetType(x, rect.Bottom-1, TileType.Wall);
    }
    for(int y=rect.Y+1; y<rect.Bottom-1; y++)
    { map.SetType(rect.Left, y, TileType.Wall);
      for(int x=rect.X+1; x<rect.Right-1; x++) map.SetType(x, y, TileType.RoomFloor);
      map.SetType(rect.Right-1, y, TileType.Wall);
    }

    for(int x=bounds.X+1; x<bounds.Right-1; x++)
    { if(map[x, bounds.Y].Type==TileType.Grass) map.SetType(x, bounds.Y, TileType.DirtSand);
      if(map[x, bounds.Bottom-1].Type==TileType.Grass) map.SetType(x, bounds.Bottom-1, TileType.DirtSand);
    }
    for(int x=bounds.X; x<bounds.Right; x++)
    { if(map[x, bounds.Y+1].Type==TileType.Grass) map.SetType(x, bounds.Y+1, TileType.DirtSand);
      if(map[x, bounds.Bottom-2].Type==TileType.Grass) map.SetType(x, bounds.Bottom-2, TileType.DirtSand);
    }
    for(int y=bounds.Y+2; y<bounds.Bottom-2; y++)
      for(int xo=0; xo<3; xo++)
      { if(map[bounds.X+xo, y].Type==TileType.Grass) map.SetType(bounds.X+xo, y, TileType.DirtSand);
        if(map[bounds.Right-xo-1, y].Type==TileType.Grass) map.SetType(bounds.Right-xo-1, y, TileType.DirtSand);
      }

    Point start = new Point(map.Width/2, map.Height/2);
    if(rect.Contains(start)) start = new Point();
    Point pt = Global.TraceLine(start, new Point(rect.X+rect.Width/2, rect.Y+rect.Height/2), -1, true,
                                new LinePoint(DoorTrace), rect).Point;
    if(pt.X==rect.X || pt.X==rect.Right-1)
    { if(pt.Y==rect.Y) pt.Y++;
      else if(pt.Y==rect.Bottom-1) pt.Y--;
    }
    map.SetType(pt, Rand.Next(100)<50 ? TileType.ClosedDoor : TileType.OpenDoor);
    return true;
  }

  
  TraceAction DoorTrace(Point pt, object context)
  { return ((Rectangle)context).Contains(pt) && map[pt].Type==TileType.Wall ? TraceAction.Stop : TraceAction.Go;
  }

  Map map;
}
#endregion

} // namespace Chrono