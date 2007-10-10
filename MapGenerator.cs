using System;
using System.Collections.Generic;
using System.Xml;
using Point=System.Drawing.Point;
using Rectangle=System.Drawing.Rectangle;
using Size=System.Drawing.Size;

namespace Chrono
{

public class UnableToGenerateException : ApplicationException
{
  public UnableToGenerateException(string message) : base(message) { }
}

public abstract class MapGenerator
{
  public virtual Size DefaultSize { get { return new Size(60, 60); } }

  public static Map Generate(XmlNode root, Dungeon.Section section, int index)
  {
    Type type = Type.GetType("Chrono."+root.Attributes["generator"].Value);
    if(type==null) throw new UnableToGenerateException("No such generator "+root.Attributes["generator"].Value);
    MapGenerator gen = (MapGenerator)type.GetConstructor(Type.EmptyTypes).Invoke(null);

    Map map;
    XmlNode opts = root.SelectSingleNode("generatorOptions");
    if(opts==null)
    {
      gen.Rand = new Random();
      map = new Map(gen.DefaultSize);
    }
    else
    {
      XmlAttribute attr = opts.Attributes["seed"];
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

#region RoomyDungeonGenerator
public class RoomyDungeonGenerator : MapGenerator
{
  protected override void Generate(Map map, XmlNode root)
  {
    this.map = map;
    int minRooms, maxRooms;

    {
      int size = map.Width*map.Height;
      minRooms = Math.Max(size/900, 1);
      maxRooms = Math.Max(size/300, 1);
      defaultWidth.R = defaultHeight.R = 20;
    }

    XmlNode opts = root.SelectSingleNode("generatorOptions");
    if(opts!=null)
    {
      defaultWidth.R  = Xml.RangeInt(opts, "maxRoomWidth", defaultWidth.R);
      defaultHeight.R = Xml.RangeInt(opts, "maxRoomHeight", defaultHeight.R);
      minRooms = Xml.Int(opts, "minRooms", minRooms);
      maxRooms = Xml.Int(opts, "maxRooms", maxRooms);
    }

    rooms.Clear();
    foreach(XmlNode room in root.SelectNodes("room[@required=true]"))
      if(!AddRoom(room)) throw new UnableToGenerateException("Couldn't add required rooms.");
    foreach(XmlNode room in root.SelectNodes("room[@required!=true]")) if(!AddRoom(room)) break;

    while(rooms.Count<minRooms) if(!AddRoom()) throw new UnableToGenerateException("Couldn't add enough rooms.");
    while(rooms.Count<maxRooms) if(!AddRoom()) break;

    for(int i=1; i<rooms.Count; i++) Connect((Room)rooms[i-1], (Room)rooms[i]);

    AddStairs(false);
    if(map.Index<map.Section.Depth-1) AddStairs(true);
  }

  sealed class Room
  {
    public Room(Rectangle area) { Area=area; Connected=false; }
    public Rectangle Area;
    public bool Connected;
  }

  bool AddRoom() { return AddRoom(null); }
  bool AddRoom(XmlNode room)
  {
    Rectangle area;
    if(FindRoom(room, out area))
    {
      DigRoom(area);
      Room r = new Room(area);
      rooms.Add(r);
      map.AddRoom(r.Area, Xml.Attr(room, "id"));
      return true;
    }
    return false;
  }

  void AddStairs(bool down)
  {
    Point point = map.RandomTile(TileType.RoomFloor);
    map.SetType(point, down ? TileType.DownStairs : TileType.UpStairs);
    map.AddLink(new Link(point, map.Section, map.Index + (down ? 1 : -1)));
  }

  bool FindRoom(XmlNode room, out Rectangle area)
  {
    int tri, r;
    Range width=defaultWidth, height=defaultHeight;

    if(room!=null)
    {
      if(!Xml.IsEmpty(room, "width")) width = new Range(room, "width");
      if(!Xml.IsEmpty(room, "height")) height = new Range(room, "height");
    }

    area = new Rectangle();
    for(tri=0; tri<50; tri++)
    {
      area = new Rectangle(Rand.Next(map.Width-width.L), Rand.Next(map.Height-height.L),
                           width.RandValue(), height.RandValue());
      if(area.Right>map.Width || area.Bottom>map.Height) continue; // size/location
      if(area.Height*3<=area.Width || area.Width*3<=area.Height) continue; // aspect ratio
      Rectangle bounds = area; bounds.Inflate(2, 2);
      for(r=0; r<rooms.Count; r++) if(bounds.IntersectsWith(((Room)rooms[r]).Area)) break;
      if(r==rooms.Count) break;
    }
    return tri<50;
  }

  bool Connect(Room r1, Room r2)
  {
    Point p1 = RectCenter(r1.Area), p2 = RectCenter(r2.Area), save = new Point();
    int fail = 0;
    bool rnd = false;
    while(rnd || p1 != p2)
    {
      Direction dir;
      if(rnd) { p2 = save; rnd = false; }
      else if(Rand.Next(10)==0)
      {
        save = p2;
        p2   = new Point(p1.X+Rand.Next(-1, 1), p1.Y+Rand.Next(-1, 1));
        rnd  = true;
      }
      int xd = p2.X-p1.X, yd = p2.Y-p1.Y;
      if(Math.Abs(xd)>Math.Abs(yd)) dir = xd<0 ? Direction.Left : Direction.Right;
      else dir = yd<0 ? Direction.Up : Direction.Down;
      if(!TryDig(ref p1, p2, dir))
      {
        if(++fail==2) return false;
        if(rnd) continue;
        Point temp = p1; p1=p2; p2=temp;
      }
      else fail=0;
    }
    r1.Connected = r2.Connected = true;
    return true;
  }

  void DigRoom(Rectangle area)
  {
    for(int x=area.X; x<area.Right; x++)
    {
      map.SetType(x, area.Top, TileType.Wall);
      map.SetType(x, area.Bottom-1, TileType.Wall);
    }
    for(int y=area.Y+1; y<area.Bottom-1; y++)
    {
      map.SetType(area.Left, y, TileType.Wall);
      for(int x=area.X+1; x<area.Right-1; x++) map.SetType(x, y, TileType.RoomFloor);
      map.SetType(area.Right-1, y, TileType.Wall);
    }
  }

  bool TryDig(ref Point p1, Point p2, Direction dir)
  {
    int x = p1.X+Global.DirMap[(int)dir].X, y = p1.Y+Global.DirMap[(int)dir].Y;
    if(!map.Contains(x, y)) return false;
    if(!CanDig(x, y))
    {
      int x2=p1.X, y2=p1.Y;
      if(dir==Direction.Left || dir==Direction.Right)
      {
        if(p1.Y < p2.Y)
        {
          if(y2<map.Height-1 && CanDig(x2, y2+1)) y2++;
          else goto Failed;
        }
        else if(y2>0 && CanDig(x2, y2-1)) y2--;
        else goto Failed;
      }
      else // up/down
      {
        if(p1.X < p2.X)
        {
          if(x2<map.Width-1 && CanDig(x2+1, y2)) x2++;
          else goto Failed;
        }
        else if(x2>0 && CanDig(x2-1, y2)) x2--;
        else goto Failed;
      }
      x=x2; y=y2;
      goto Success;
      Failed:
      if(dir==Direction.Left || dir==Direction.Right)
      {
        if(p1.Y < p2.Y)
        {
          if(y<map.Height-1 && CanDig(x, y+1)) y++;
          else return false;
        }
        else if(y>0 && CanDig(x, y-1)) y--;
        else return false;
      }
      else // up/down
      {
        if(p1.X < p2.X)
        {
          if(x<map.Width-1 && CanDig(x+1, y)) x++;
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
  {
    if(map.IsUsuallyPassable(x, y)) return true;
    return !map.IsWall(x, y) || !IsWallJunction(x, y) && !HasAdjacentDoor(x, y, true);
  }

  void Dig(int x, int y)
  {
    if(map.IsUsuallyPassable(x, y) || map.IsDoor(x, y)) return;
    if(map.IsWall(x, y))
    {
      map.SetType(x, y, TileType.ClosedDoor);
      if(Rand.Next(100) < 8) map.SetFlag(x, y, TileFlag.Locked, true);
    }
    else map.SetType(x, y, TileType.Corridor);
  }

  bool IsWallJunction(int x, int y)
  {
    return map.IsWall(x, y) &&
           (map.IsWall(x-1, y) || map.IsWall(x+1, y)) && (map.IsWall(x, y-1) || map.IsWall(x, y+1));
  }

  bool HasAdjacentDoor(int x, int y, bool fourWay)
  {
    if(fourWay) return map.IsDoor(x-1, y) || map.IsDoor(x, y-1) || map.IsDoor(x+1, y) || map.IsDoor(x, y+1);
    else
    {
      for(int y2=y-1; y2<=y+1; y2++)
      {
        for(int x2=x-1; x2<=x+1; x2++)
        {
          if(map.IsDoor(x2, y2) && (x2!=x || y2!=y)) return true;
        }
      }
    }
    return false;
  }

  static Point RectCenter(Rectangle rect) { return new Point(rect.X+rect.Width/2, rect.Y+rect.Height/2); }

  Map map;
  List<Room> rooms = new List<Room>();
  Range defaultWidth=new Range(4, 12), defaultHeight=new Range(4, 12);
}
#endregion

#region MetaCaveGenerator
public class MetaCaveGenerator : MapGenerator
{
  protected override void Generate(Map map, XmlNode root)
  {
    int ncircles = Xml.Int(Xml.AttrNode(root.SelectSingleNode("generatorOptions"), "circles"), 50);

    Point[] centers = new Point[ncircles];
    PathFinder path = new PathFinder();
    int width = map.Width, height = map.Height;

    while(true)
    {
      for(int i=0; i<ncircles; i++) centers[i] = new Point(Global.Rand(width-8)+4, Global.Rand(height-8)+4);

      for(int y=0; y<height; y++)
        for(int x=0; x<width; x++)
        {
          double sum=0;
          for(int i=0; i<ncircles; i++)
          {
            int xd=x-centers[i].X, yd=y-centers[i].Y;
            sum += 1.0/(xd*xd+yd*yd);
          }
          map.SetType(x, y, sum>0.2 ? TileType.RoomFloor : TileType.Wall);
        }
      for(int x=0; x<width; x++)
      {
        map.SetType(x, 0, TileType.Wall);
        map.SetType(x, height-1, TileType.Wall);
      }
      for(int y=0; y<height; y++)
      {
        map.SetType(0, y, TileType.Wall);
        map.SetType(width-1, y, TileType.Wall);
      }

      Point up=AddStairs(map, false);
      if(map.Index<map.Section.Depth-1)
      {
        Point down=AddStairs(map, true);
        if(!path.Plan(map, up, down) || path.GetPathNode(up).Cost>=PathFinder.BadPathCost)
        {
          map.ClearLinks();
          map.Fill(TileType.SolidRock);
          continue;
        }
      }
      break;
    }
  }

  Point AddStairs(Map map, bool down)
  {
    Point point = map.RandomTile(TileType.RoomFloor);
    map.SetType(point, down ? TileType.DownStairs : TileType.UpStairs);
    map.AddLink(new Link(point, map.Section, map.Index + (down ? 1 : -1)));
    return point;
  }
}
#endregion

#region TownGenerator
public class TownGenerator : MapGenerator
{
  public override Size DefaultSize { get { return new Size(100, 60); } }

  protected override void Generate(Map map, XmlNode root)
  {
    this.map = map;

    map.Fill(TileType.Grass);
    int size = map.Width*map.Height;
    for(int ntrees=size/50; ntrees>0; ntrees--) map.SetType(map.FreeSpace(), TileType.Tree);

    foreach(XmlNode room in root.SelectNodes("room[@required='true']"))
      if(!AddRoom(room)) throw new UnableToGenerateException("Couldn't add required rooms.");
    foreach(XmlNode room in root.SelectNodes("room[@required!='true']")) if(!AddRoom(room)) break;

    AddShop(ShopType.Get("General"));
    AddShop(ShopType.Get("Food"));
    AddShop(ShopType.Get("ArmorWeapons"));
    AddShop(ShopType.Get("Magic"));

    while(AddRandomRoom()) ;

    // FIXME: this could cause problems because of its randomness... (eg, a room may fill up with people and then
    // FreeSpace(Rectangle) may fail, or something similar)
    int entityIndex = Global.GetEntityIndex("builtin/Townsperson");
    for(int npeople=size/200; npeople>0; npeople--)
    {
      Entity peon = new Entity(entityIndex);
      peon.XL  = Global.Rand(1, 3);
      peon.Pos = map.FreeSpace();
      // TODO: peon.SocialGroup = map.GroupID;
      map.Entities.Add(peon);
    }
  }

  bool AddRoom(XmlNode room)
  {
    Range width  = new Range(room, "width", 5, 12);
    Range height = new Range(room, "height", 5, 12);

    Rectangle rect;
    if(DigRoom(width, height, out rect))
    {
      map.AddRoom(rect, Xml.Attr(room, "id"));
      return true;
    }
    return false;
  }

  bool AddRandomRoom()
  {
    Rectangle rect;
    if(Rand.Next(12)==11) // 1 in 12 buildings are shops
    {
      if(!DigRoom(4, 10, out rect)) return false;
      Shop temp = new Shop(rect, null, null);
      map.AddShop(rect, ShopType.GetRandom(temp.ItemArea.Width*temp.ItemArea.Height));
    }
    else
    {
      if(!DigRoom(5, 12, out rect)) return false;
      map.AddRoom(rect);
    }
    return true;
  }

  void AddShop(ShopType type)
  {
    Rectangle rect;
    if(!DigRoom(4, 10, out rect)) throw new UnableToGenerateException("Couldn't add enough shops");
    map.AddShop(rect, type);
  }

  bool DigRoom(int min, int max, out Rectangle rect)
  {
    return DigRoom(new Range(min, max), new Range(min, max), out rect);
  }
  bool DigRoom(Range width, Range height, out Rectangle rect)
  {
    Rectangle bounds;
    rect = bounds = new Rectangle();

    int tri, r;
    for(tri=0; tri<50; tri++)
    {
      rect = new Rectangle(Rand.Next(map.Width-width.L)+2, Rand.Next(map.Height-height.L)+2,
                           width.RandValue(), height.RandValue());
      if(rect.Right>map.Width-2 || rect.Bottom>map.Height-2) continue; // size/location
      if(rect.Height*3<=rect.Width || rect.Width*3<=rect.Height) continue; // aspect ratio
      bounds = rect; bounds.Inflate(3, 2);
      for(r=0; r<map.Rooms.Length; r++) if(bounds.IntersectsWith(map.Rooms[r].OuterArea)) break; // spacing
      if(r==map.Rooms.Length) break;
    }
    if(tri==50) return false;

    for(int x=rect.X; x<rect.Right; x++)
    {
      map.SetType(x, rect.Top, TileType.Wall);
      map.SetType(x, rect.Bottom-1, TileType.Wall);
    }
    for(int y=rect.Y+1; y<rect.Bottom-1; y++)
    {
      map.SetType(rect.Left, y, TileType.Wall);
      for(int x=rect.X+1; x<rect.Right-1; x++) map.SetType(x, y, TileType.RoomFloor);
      map.SetType(rect.Right-1, y, TileType.Wall);
    }

    for(int x=bounds.X+1; x<bounds.Right-1; x++)
    {
      if(map[x, bounds.Y].Type==TileType.Grass) map.SetType(x, bounds.Y, TileType.DirtSand);
      if(map[x, bounds.Bottom-1].Type==TileType.Grass) map.SetType(x, bounds.Bottom-1, TileType.DirtSand);
    }
    for(int x=bounds.X; x<bounds.Right; x++)
    {
      if(map[x, bounds.Y+1].Type==TileType.Grass) map.SetType(x, bounds.Y+1, TileType.DirtSand);
      if(map[x, bounds.Bottom-2].Type==TileType.Grass) map.SetType(x, bounds.Bottom-2, TileType.DirtSand);
    }
    for(int y=bounds.Y+2; y<bounds.Bottom-2; y++)
      for(int xo=0; xo<3; xo++)
      {
        if(map[bounds.X+xo, y].Type==TileType.Grass) map.SetType(bounds.X+xo, y, TileType.DirtSand);
        if(map[bounds.Right-xo-1, y].Type==TileType.Grass) map.SetType(bounds.Right-xo-1, y, TileType.DirtSand);
      }

    Point start = new Point(map.Width/2, map.Height/2);
    if(rect.Contains(start)) start = new Point();
    Point pt = Global.TraceLine(start, new Point(rect.X+rect.Width/2, rect.Y+rect.Height/2), -1, true,
                                new TracePoint(DoorTrace), rect).End;
    if(pt.X==rect.X || pt.X==rect.Right-1)
    {
      if(pt.Y==rect.Y) pt.Y++;
      else if(pt.Y==rect.Bottom-1) pt.Y--;
    }
    map.SetType(pt, Rand.Next(100)<50 ? TileType.ClosedDoor : TileType.OpenDoor);
    return true;
  }


  TraceAction DoorTrace(Point pt, object context)
  {
    return ((Rectangle)context).Contains(pt) && map[pt].Type==TileType.Wall ? TraceAction.Stop : TraceAction.Go;
  }

  Map map;
}
#endregion

} // namespace Chrono