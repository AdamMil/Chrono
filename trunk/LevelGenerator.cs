using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public class UnableToGenerateException : ApplicationException
{ public UnableToGenerateException(string message) : base(message) { }
}

public class LevelGenerator
{ public void Reseed(int s) { Rand = new Random(s); }
  public Random Rand = new Random();
}

public class RoomyLevelGenerator : LevelGenerator
{ public int DarkChance { get { return darkChance; } set { darkChance=value; } }
  public Size MaxRoomSize { get { return maxRoomSize; } set { maxRoomSize=value; } }

  public Level Generate() { return Generate(60, 60, 4, 12); }
  public Level Generate(int minrooms, int maxrooms) { return Generate(60, 60, minrooms, maxrooms); }
  public Level Generate(int width, int height, int minrooms, int maxrooms)
  { lev = new Level(width, height);
    rooms.Clear();
    while(rooms.Count<minrooms) if(!AddRoom(lev)) throw new UnableToGenerateException("Couldn't add enough rooms.");
    while(rooms.Count<maxrooms) if(!AddRoom(lev)) break;
    
    Map map = lev.Map;
    foreach(Room r in rooms)
    { if(Rand.Next(darkChance)!=0)
      { for(int y=r.Area.Y+1; y<r.Area.Bottom; y++)
          for(int x=r.Area.X+1; x<r.Area.Right; x++)
            map.SetFlag(x, y, Tile.Flag.PermaLit, true);
      }
    }

    for(int i=1; i<rooms.Count; i++) Connect(lev, (Room)rooms[i-1], (Room)rooms[i]);

    AddStairs(lev, true);
    AddStairs(lev, false);
    return lev;
  }
  
  struct Room
  { public Rectangle Area;
    public bool Connected;
  }

  bool AddRoom(Level lev)
  { Room room = new Room();
    int tri, r;
    for(tri=0; tri<50; tri++)
    { room.Area = new Rectangle(Rand.Next(lev.Width-4), Rand.Next(lev.Height-4),
                                Rand.Next(4, maxRoomSize.Width), Rand.Next(4, maxRoomSize.Height));
      if(room.Area.Right>lev.Width || room.Area.Bottom>lev.Height) continue;
      Rectangle bounds = room.Area; bounds.Offset(-2, -2); bounds.Inflate(4, 4);
      for(r=0; r<rooms.Count; r++) if(bounds.IntersectsWith(((Room)rooms[r]).Area)) break;
      if(r==rooms.Count) break;
    }
    if(tri==50) return false;

    for(int x=room.Area.X; x<room.Area.Right; x++)
    { lev.Map.SetTile(x, room.Area.Top, TileType.Wall);
      lev.Map.SetTile(x, room.Area.Bottom-1, TileType.Wall);
    }
    for(int y=room.Area.Y+1; y<room.Area.Bottom-1; y++)
    { lev.Map.SetTile(room.Area.Left, y, TileType.Wall);
      for(int x=room.Area.X+1; x<room.Area.Right-1; x++) lev.Map.SetTile(x, y, TileType.RoomFloor);
      lev.Map.SetTile(room.Area.Right-1, y, TileType.Wall);
    }
    rooms.Add(room);
    return true;
  }
  
  bool Connect(Level lev, Room r1, Room r2)
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
      if(!TryDig(lev, ref p1, p2, dir))
      { if(++fail==2) return false;
        if(rnd) continue;
        Point temp = p1; p1=p2; p2=temp;
      }
      else fail=0;
    }
    r1.Connected = r2.Connected = true;
    return true;
  }
  
  bool TryDig(Level lev, ref Point p1, Point p2, Direction dir)
  { int x = p1.X+Global.DirMap[(int)dir].X, y = p1.Y+Global.DirMap[(int)dir].Y;
    if(!lev.Map.Contains(x, y)) return false;
    if(!CanDig(x, y))
    { int x2=p1.X, y2=p1.Y;
      if(dir==Direction.Left || dir==Direction.Right)
      { if(p1.Y < p2.Y)
        { if(y2<lev.Height-1 && CanDig(x2, y2+1)) y2++;
          else goto Failed;
        }
        else if(y2>0 && CanDig(x2, y2-1)) y2--;
        else goto Failed;
      }
      else // up/down
      { if(p1.X < p2.X)
        { if(x2<lev.Width-1 && CanDig(x2+1, y2)) x2++;
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
        { if(y<lev.Height-1 && CanDig(x, y+1)) y++;
          else return false;
        }
        else if(y>0 && CanDig(x, y-1)) y--;
        else return false;
      }
      else // up/down
      { if(p1.X < p2.X)
        { if(x<lev.Width-1 && CanDig(x+1, y)) x++;
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
  { if(lev.Map.IsPassable(x, y)) return true;
    return !lev.Map.IsWall(x, y) || !IsWallJunction(x, y) && !HasAdjacentDoor(x, y, true);
  }

  void Dig(int x, int y)
  { if(lev.Map.IsPassable(x, y) || lev.Map.IsDoor(x, y)) return;
    if(lev.Map.IsWall(x, y))
    { lev.Map.SetTile(x, y, TileType.ClosedDoor);
      if(Rand.Next(100)<10) lev.Map.SetFlag(x, y, Tile.Flag.Locked, true);
    }
    else lev.Map.SetTile(x, y, TileType.Corridor);
  }

  void AddStairs(Level lev, bool up)
  { lev.Map.SetTile(lev.Map.RandomTile(TileType.RoomFloor), up ? TileType.UpStairs : TileType.DownStairs);
  }

  bool IsWallJunction(int x, int y)
  { return lev.Map.IsWall(x, y) &&
           (lev.Map.IsWall(x-1, y) || lev.Map.IsWall(x+1, y)) &&
           (lev.Map.IsWall(x, y-1) || lev.Map.IsWall(x, y+1));
  }

  bool HasAdjacentDoor(int x, int y, bool fourWay)
  { if(fourWay)
      return lev.Map.IsDoor(x-1, y) || lev.Map.IsDoor(x, y-1) || lev.Map.IsDoor(x+1, y) || lev.Map.IsDoor(x, y+1);
    else
      for(int y2=y-1; y2<=y+1; y2++)
        for(int x2=x-1; x2<=x+1; x2++)
          if(lev.Map.IsDoor(x2, y2) && (x2!=x || y2!=y)) return true;
    return false;
  }
  
  static Point RectCenter(Rectangle rect) { return new Point(rect.X+rect.Width/2, rect.Y+rect.Height/2); }
  
  Level lev;
  ArrayList rooms = new ArrayList();
  Size maxRoomSize = new Size(22, 15);
  int  darkChance  = 12;
}

} // namespace Chrono