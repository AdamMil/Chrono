using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public class UnableToGenerateException : ApplicationException
{ public UnableToGenerateException(string message) : base(message) { }
}

public abstract class MapGenerator
{ public void Reseed(int s) { Rand = new Random(s); }
  public Random Rand = new Random();

  public Map Generate() { return Generate(60, 60); }
  public abstract Map Generate(int width, int height);
}

#region RoomyMapGenerator
public class RoomyMapGenerator : MapGenerator
{ public Size MaxRoomSize { get { return maxRoomSize; } set { maxRoomSize=value; } }

  public override Map Generate(int width, int height)
  { int size = width*height;
    return Generate(width, height, Math.Max(size/900, 1), Math.Max(size/300, 1));
  }
  public Map Generate(int width, int height, int minrooms, int maxrooms)
  { map = new Map(width, height);
    maxRoomSize = new Size(width/3, height/3);
    rooms.Clear();
    while(rooms.Count<minrooms) if(!AddRoom()) throw new UnableToGenerateException("Couldn't add enough rooms.");
    while(rooms.Count<maxrooms) if(!AddRoom()) break;

    for(int i=1; i<rooms.Count; i++) Connect((Room)rooms[i-1], (Room)rooms[i]);

    AddStairs(false);
    AddStairs(true);
    return map;
  }

  struct Room
  { public Rectangle Area;
    public bool Connected;
  }

  bool AddRoom()
  { Room room = new Room();
    int tri, r;
    for(tri=0; tri<50; tri++)
    { room.Area = new Rectangle(Rand.Next(map.Width-4), Rand.Next(map.Height-4),
                                Rand.Next(4, maxRoomSize.Width), Rand.Next(4, maxRoomSize.Height));
      if(room.Area.Right>map.Width || room.Area.Bottom>map.Height) continue; // size
      if(room.Area.Height*3<=room.Area.Width || room.Area.Width*3<=room.Area.Height) continue; // aspect ratio
      Rectangle bounds = room.Area; bounds.Offset(-2, -2); bounds.Inflate(4, 4);
      for(r=0; r<rooms.Count; r++) if(bounds.IntersectsWith(((Room)rooms[r]).Area)) break;
      if(r==rooms.Count) break;
    }
    if(tri==50) return false;

    for(int x=room.Area.X; x<room.Area.Right; x++)
    { map.SetType(x, room.Area.Top, TileType.Wall);
      map.SetType(x, room.Area.Bottom-1, TileType.Wall);
    }
    for(int y=room.Area.Y+1; y<room.Area.Bottom-1; y++)
    { map.SetType(room.Area.Left, y, TileType.Wall);
      for(int x=room.Area.X+1; x<room.Area.Right-1; x++) map.SetType(x, y, TileType.RoomFloor);
      map.SetType(room.Area.Right-1, y, TileType.Wall);
    }
    rooms.Add(room);
    return true;
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

  void AddStairs(bool down)
  { Point point = map.RandomTile(TileType.RoomFloor);
    map.SetType(point, down ? TileType.DownStairs : TileType.UpStairs);
    map.AddLink(new Link(point, down));
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

public class MetaCaveGenerator : MapGenerator
{
  public override Map Generate(int width, int height) { return Generate(width, height, 50); }
  public Map Generate(int width, int height, int ncircles)
  { Point[] centers = new Point[ncircles];
    for(int i=0; i<ncircles; i++) centers[i] = new Point(Global.Rand(width-8)+4, Global.Rand(height-8)+4);

    Map map = new Map(width, height);
    for(int y=0; y<height; y++)
      for(int x=0; x<width; x++)
      { double sum=0;
        for(int i=0; i<ncircles; i++)
        { int xd=x-centers[i].X, yd=y-centers[i].Y;
          sum += 1.0/(xd*xd+yd*yd);
        }
        map.SetType(x, y, sum>0.2 ? TileType.RoomFloor : TileType.Wall); // 0.178571
      }
    for(int x=0; x<width; x++)
    { map.SetType(x, 0, TileType.Wall);
      map.SetType(x, height-1, TileType.Wall);
    }
    for(int y=0; y<height; y++)
    { map.SetType(0, y, TileType.Wall);
      map.SetType(width-1, y, TileType.Wall);
    }
      
    AddStairs(map, false);
    AddStairs(map, true);
    return map;
  }

  void AddStairs(Map map, bool down)
  { Point point = map.RandomTile(TileType.RoomFloor);
    map.SetType(point, down ? TileType.DownStairs : TileType.UpStairs);
    map.AddLink(new Link(point, down));
  }
}

} // namespace Chrono