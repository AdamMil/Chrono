using System;
using System.Drawing;

namespace Chrono
{

public class Player : Creature
{ public Player() { Color=Color.White; Timer=50; /*we get a headstart*/ }

  public override void Generate(int level, CreatureClass myClass, Race race)
  { base.Generate(level, myClass, race);
    for(int i=0; i<5; i++) LevelUp(); // players get an advantage over monsters
    ExpLevel -= 5;
  }

  public override void Think()
  { if(interrupt) { inp.Count=0; interrupt=false; }
    base.Think();
    Point[] vis = VisibleTiles();

    next:
    int count = inp.Count;
    if(count==0) // inp.Count drops to zero unless set to 'count' by an action
    { UpdateMemory(vis);
      App.IO.Render(this);
      inp = App.IO.GetNextInput();
      count = inp.Count;
    }
    else count--;
    inp.Count=0;

    switch(inp.Action)
    { case Action.CloseDoor:
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

      case Action.Drop:
      { if(Inv.Count==0) { App.IO.Print("You're not carrying anything."); goto next; }
        MenuItem[] items = App.IO.ChooseItem("Drop what?", Inv, MenuFlag.AllowNum|MenuFlag.Multi, ItemClass.Any);
        if(items.Length==0) { App.IO.Print("Never mind."); goto next; }
        foreach(MenuItem i in items)
        { if(Wearing(i.Item) && TryRemove(i.Item)) break;
          Drop(i.Char, i.Count);
        }
        break;
      }

      case Action.Eat:
      { if(Hunger<100) { App.IO.Print("You're still full."); goto next; }

        Food toEat=null;
        IInventory inv=null;
        if(Map.HasItems(Position))
        { Item[] items = Map[Position].Items.GetItems(ItemClass.Food);
          foreach(Item i in items)
            if(App.IO.YesNo(string.Format("There {0} {1} here. Eat {2}?", i.AreIs, i, i.ItOne),
                            false))
            { toEat=(Food)i; inv=Map[Position].Items;
              break;
            }
        }
        if(toEat==null)
        { if(!Inv.Has(ItemClass.Food)) { App.IO.Print("You have nothing to eat!"); goto next; }
          MenuItem[] items = App.IO.ChooseItem("What do you want to eat?", Inv, MenuFlag.None, ItemClass.Food);
          if(items.Length==0) { App.IO.Print("Never mind."); goto next; }
          toEat = items[0].Item as Food;
          if(toEat==null) { App.IO.Print("You can't eat that!"); goto next; }
          inv = Inv;
        }
        if(toEat==null) goto next;
        if(toEat.Count>1)
        { toEat = (Food)toEat.Split(1);
          if(!toEat.Eat(this)) inv.Add(toEat);
        }
        else if(toEat.Eat(this)) inv.Remove(toEat);
        if(Hunger<100) App.IO.Print("You feel full.");
        App.IO.RedrawStats=true;
        break;
      }

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
        { if(ThinkUpdate(ref vis)) break;
          TileType type = Map[np].Type;
          if(type==TileType.UpStairs || type==TileType.DownStairs || Map.HasItems(np)) break;

          int newopts=0;
          for(int i=0; i<5; i++)
          { np = Global.Move(Position, (Direction)chk[i]);
            if(Map.IsPassable(np) || Map.IsDoor(np)) newopts++;
          }
          if(newopts!=options || IsMonsterVisible(vis)) break;

          np = Global.Move(Position, inp.Direction);
          if(Map.IsPassable(np) && !Map.IsDangerous(np)) Position = np;
          else break;
        }
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

      case Action.Pickup:
      { if(!Map.HasItems(Position)) { App.IO.Print("There are no items here."); goto next; }
        ItemPile inv = Map[Position].Items;
        if(inv.Count==1)
        { Item item = inv[0], newItem = Pickup(inv, 0);
          string s = string.Format("{0} - {1}", newItem.Char, item.FullName);
          if(item.Count!=newItem.Count) s += string.Format(" (now {0})", newItem.Count);
          App.IO.Print(s);
        }
        else
          foreach(MenuItem item in App.IO.Menu(inv, MenuFlag.AllowNum|MenuFlag.Multi))
          { if(item.Count==item.Item.Count) Pickup(inv, item.Item);
            else Pickup(item.Item.Split(item.Count));
          }
        break;
      }
      
      case Action.Quit:
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false)) App.Quit=true;
        break;

      case Action.Rest:
        if(IsMonsterVisible(vis)) break;
        inp.Count = count;
        break;
      
      case Action.Wear:
      { MenuItem[] items = App.IO.ChooseItem("Wear what?", Inv, MenuFlag.None, wearableClasses);
        if(items.Length==0) { App.IO.Print("Never mind."); goto next; }
        Wearable item = items[0].Item as Wearable;
        if(item==null) { App.IO.Print("You can't wear that!"); goto next; }
        if(Wearing(item)) { App.IO.Print("You're already wearing that!"); goto next; }
        if(Wearing(item.Slot) && !TryRemove(item.Slot)) goto next;
        Wear(item);
        break;
      }
    }

    OnAge();
  }
  
  public static Player Generate(CreatureClass myClass, Race race)
  { return (Player)Creature.Generate(typeof(Player), 0, myClass, race);
  }

  protected virtual void OnAge()
  { Hunger++;
    if(Hunger==HungryAt || Hunger==StarvingAt || Hunger==StarveAt)
    { if(Hunger==HungryAt) App.IO.Print(Color.Warning, "You're getting hungry.");
      else if(Hunger==StarvingAt) App.IO.Print(Color.Dire, "You're starving!");
      else
      { App.IO.Print("You die of starvation.");
      }
      App.IO.RedrawStats = true;
      Interrupt();
    }
  }

  public override void OnDrop(Item item) { App.IO.Print("You drop {0}.", item); }
  public override void OnRemove(Wearable item) { App.IO.Print("You remove {0}.", item); }
  public override void OnWear(Wearable item)
  { if(item.EquipText!=null) App.IO.Print(item.EquipText);
    else App.IO.Print("You put on {0}.", item);
  }

  protected internal override void OnMapChanged()
  { base.OnMapChanged();
    Memory = Map==null ? null : new Map(Map.Width, Map.Height);
  }
  
  protected bool ThinkUpdate(ref Point[] vis)
  { Map.Simulate(this);
    base.Think();
    if(vis!=null) UpdateMemory(vis);
    vis = VisibleTiles();
    if(interrupt) { interrupt=false; return true; }
    return false;
  }
  
  protected static readonly ItemClass[] wearableClasses =  new ItemClass[]
  { ItemClass.Amulet, ItemClass.Armor, ItemClass.Ring
  };

  Input inp;
}

} // namespace Chrono