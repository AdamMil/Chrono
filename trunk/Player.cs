using System;
using System.Drawing;

namespace Chrono
{

public class Player : Creature
{ public Player() { Timer=100; } // we go first

  public override void Generate(int level, CreatureClass myClass, Race race)
  { base.Generate(level, myClass, race);
    for(int i=0; i<5; i++) LevelUp(); // players get an advantage over monsters
    ExpLevel -= 5;
  }

  public override void Think()
  { base.Think();
    App.IO.Render(this);

    next:
    if(inp.Count==0) inp = App.IO.GetNextInput();
    else inp.Count--;
    switch(inp.Action)
    { case Action.Rest: VisibleTiles(); break;
      case Action.Move:
      { Point newpos = Global.Move(Position, inp.Direction);
        if(Map.IsPassable(newpos)) Position = newpos;
        else { inp.Count=0; goto next; }
        break;
      }
      case Action.MoveToInteresting: // this needs to be improved, and made continuable
      { inp.Count = 0;

        Point np = Global.Move(Position, inp.Direction);
        if(!Map.IsPassable(np)) goto next;
        Position = np;

        int dir = (int)inp.Direction, options=0;
        int[] chk = new int[5] { dir-2, dir-1, dir, dir+1, dir+2 };
        for(int i=0; i<5; i++)
        { if(chk[i]<0) chk[i] += 8;
          else if(chk[i]>=8) chk[i] -= 8;
          np = Global.Move(Position, (Direction)chk[i]);
          if(Map.IsPassable(np) || Map.IsDoor(np)) options++;
        }
        while(true)
        { Map.Simulate(this); base.Think();
          int newopts=0;
          for(int i=0; i<5; i++)
          { np = Global.Move(Position, (Direction)chk[i]);
            if(Map.IsPassable(np) || Map.IsDoor(np)) newopts++;
          }
          if(newopts!=options) goto done;
          np = Global.Move(Position, inp.Direction);
          if(Map.IsPassable(np)) Position = np;
          else goto done;
        }
        done:
        break;
      }
      case Action.OpenDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        inp.Count = 0;
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Map[newpos];
          if(tile.Type==TileType.OpenDoor) App.IO.Print("That door is already open.");
          else if(tile.Type!=TileType.ClosedDoor) App.IO.Print("There is no door there.");
          else if(tile.GetFlag(Tile.Flag.Locked)) App.IO.Print("That door is locked.");
          else { Map.SetTile(newpos, TileType.OpenDoor); break; }
        }
        goto next;
      }
      case Action.CloseDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        inp.Count = 0;
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Map[newpos];
          if(tile.Type==TileType.ClosedDoor) App.IO.Print("That door is already closed.");
          else if(tile.Type!=TileType.OpenDoor) App.IO.Print("There is no door there.");
          else { Map.SetTile(newpos, TileType.ClosedDoor); break;  }
        }
        goto next;
      }
      case Action.Quit: 
        inp.Count = 0;
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false)) App.Quit=true;
        break;
    }
  }
  
  public static Player Generate(CreatureClass myClass, Race race)
  { return (Player)Creature.Generate(typeof(Player), 0, myClass, race);
  }
  
  Input inp;
}

} // namespace Chrono