using System;
using System.Drawing;

namespace Chrono
{

public class Player : Creature
{ 
  public override void Think()
  { base.Think();
    App.IO.Render(this);

    Input inp = App.IO.GetNextInput();
    switch(inp.Action)
    { case Action.Move:
      { Point newpos = Global.Move(Position, inp.Direction);
        if(Level.Map.IsPassable(newpos)) Position = newpos;
        break;
      }
      case Action.OpenDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Level.Map[newpos];
          if(tile.Type==TileType.OpenDoor) App.IO.Print("That door is already open.");
          else if(tile.Type!=TileType.ClosedDoor) App.IO.Print("There is no door there.");
          else if(tile.GetFlag(Tile.Flag.Locked)) App.IO.Print("That door is locked.");
          else Level.Map.SetTile(newpos, TileType.OpenDoor);
        }
        break;
      }
      case Action.CloseDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Level.Map[newpos];
          if(tile.Type==TileType.ClosedDoor) App.IO.Print("That door is already closed.");
          else if(tile.Type!=TileType.ClosedDoor) App.IO.Print("There is no door there.");
          else Level.Map.SetTile(newpos, TileType.ClosedDoor);
        }
        break;
      }
      case Action.Quit: if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false)) App.Quit=true; break;
    }
  }
}

} // namespace Chrono