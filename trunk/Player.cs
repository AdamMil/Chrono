using System;
using System.Drawing;

namespace Chrono
{

public class Player : Creature
{ public Player() { Color=Color.White; Timer=100; /*we go first*/ }

  public override void Generate(int level, CreatureClass myClass, Race race)
  { base.Generate(level, myClass, race);
    for(int i=0; i<5; i++) LevelUp(); // players get an advantage over monsters
    ExpLevel -= 5;
  }

  public override void Think()
  { base.Think();

    Point[] vis = VisibleTiles();
    next:
    int count = inp.Count;
    if(count==0)
    { UpdateMemory(vis);
      App.IO.Render(this);
      inp = App.IO.GetNextInput();
    }
    else
    { count--;
      inp.Count = 0;
    }
    switch(inp.Action)
    { case Action.Rest:
        if(IsMonsterVisible(vis)) goto next;
        inp.Count = count;
        break;

      case Action.Move:
      { Point newpos = Global.Move(Position, inp.Direction);
        if(Map.IsPassable(newpos)) { inp.Count = count; Position = newpos; }
        else goto next;
        break;
      }

      case Action.MoveToInteresting: // this needs to be improved, and made continuable
      { Point np = Global.Move(Position, inp.Direction);
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
        { Map.Simulate(this); base.Think(); UpdateMemory(vis); vis = VisibleTiles();
          if(Map.HasItems(np)) goto done;
          int newopts=0;
          for(int i=0; i<5; i++)
          { np = Global.Move(Position, (Direction)chk[i]);
            if(Map.IsPassable(np) || Map.IsDoor(np)) newopts++;
          }
          if(newopts!=options || IsMonsterVisible(vis)) goto done;

          np = Global.Move(Position, inp.Direction);
          if(Map.IsPassable(np)) Position = np;
          else goto done;
        }
        done:
        break;
      }

      case Action.Pickup:
      { if(!Map.HasItems(Position)) { App.IO.Print("There are no items here."); goto next; }
        Inventory inv = Map[Position].Items;
        if(inv.Count==1)
        { Item item = Pickup(inv, 0);
          App.IO.Print("{0} - {1}", item.Char, item.FullName);
        }
        else
          foreach(MenuItem item in App.IO.Menu(inv, MenuFlag.AllowNum|MenuFlag.Multi))
          { if(item.Count==item.Item.Count) Pickup(inv, item.Item);
            else Pickup(item.Item.Split(item.Count));
          }
        break;
      }
      
      case Action.Drop:
      { if(Inv==null || Inv.Count==0) { App.IO.Print("You're not carrying anything."); goto next; }
        char c = App.IO.CharChoice("Drop which item?", Inv.CharString()+"?");
        if(c==0) { App.IO.Print("Never mind."); goto next; }
        Drop(c);
        break;
      }

      case Action.OpenDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Map[newpos];
          if(tile.Type==TileType.OpenDoor) App.IO.Print("That door is already open.");
          else if(tile.Type!=TileType.ClosedDoor) App.IO.Print("There is no door there.");
          else if(tile.GetFlag(Tile.Flag.Locked)) App.IO.Print("That door is locked.");
          else { Map.SetType(newpos, TileType.OpenDoor); break; }
        }
        goto next;
      }

      case Action.CloseDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Map[newpos];
          if(tile.Type==TileType.ClosedDoor) App.IO.Print("That door is already closed.");
          else if(tile.Type!=TileType.OpenDoor) App.IO.Print("There is no door there.");
          else { Map.SetType(newpos, TileType.ClosedDoor); break;  }
        }
        goto next;
      }

      case Action.Quit: 
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false)) App.Quit=true;
        break;
    }
  }
  
  public static Player Generate(CreatureClass myClass, Race race)
  { return (Player)Creature.Generate(typeof(Player), 0, myClass, race);
  }

  protected internal override void OnMapChanged()
  { base.OnMapChanged();
    Memory = Map==null ? null : new Map(Map.Width, Map.Height);
  }

  Input inp;
}

} // namespace Chrono