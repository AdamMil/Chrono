using System;
using System.Drawing;

namespace Chrono
{

public class Player : Creature
{ public Player() { Color=Color.White; Timer=50; /*we get a headstart*/ }

  public override void Die(string cause)
  { App.IO.Print("You die.");
    App.IO.Print("Goodbye {0} the {1}... you were killed by: {2}", Name, Title, cause);
    if(!App.IO.YesNo("Die?", false))
    { App.IO.Print("Okay, you're alive again.");
      HP = MaxHP;
    }
    else App.Quit = true;
  }

  public override void Generate(int level, CreatureClass myClass)
  { base.Generate(level, myClass);
    LevelUp(); ExpLevel--; // players get an advantage
  }

  public override void Think()
  { if(interrupt) { inp.Count=0; interrupt=false; }
    base.Think();
    Point[] vis = VisibleTiles();
    goto next;

    nevermind: App.IO.Print("Never mind.");

    next:
    int count = inp.Count;
    if(--count<=0)
    { UpdateMemory(vis);
      App.IO.Render(this);
      inp = App.IO.GetNextInput();
      count = inp.Count;
    }
    inp.Count=0; // inp.Count drops to zero unless set to 'count' by an action

    switch(inp.Action)
    { case Action.CloseDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Map[newpos];
          if(tile.Type==TileType.ClosedDoor) App.IO.Print("That door is already closed.");
          else if(tile.Type!=TileType.OpenDoor) App.IO.Print("There is no door there.");
          else if(Map.HasItems(newpos) || Map.GetCreature(newpos)!=null) App.IO.Print("The door is blocked.");
          else { Map.SetType(newpos, TileType.ClosedDoor); break;  }
        }
        goto next;
      }

      case Action.Drop:
      { if(Inv.Count==0) { App.IO.Print("You're not carrying anything."); goto next; }
        MenuItem[] items = App.IO.ChooseItem("Drop what?", Inv, MenuFlag.AllowNum|MenuFlag.Multi, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        foreach(MenuItem i in items)
          if((!Wearing(i.Item) || TryRemove(i.Item)) && (!Equipped(i.Item) || TryUnequip(i.Item)))
            Drop(i.Char, i.Count);
        break;
      }

      case Action.Eat:
      { if(Hunger<Food.MaxFoodPerTurn) { App.IO.Print("You're still full."); goto next; }

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
          if(items.Length==0) goto nevermind;
          toEat = items[0].Item as Food;
          if(toEat==null) { App.IO.Print("You can't eat that!"); goto next; }
          inv = Inv;
        }
        if(toEat==null) goto next;
        bool split=false, consumed=false;
        if(toEat.Count>1) { toEat = (Food)toEat.Split(1); split=true; }
        if(toEat.FoodLeft>Food.MaxFoodPerTurn) App.IO.Print("You begin to eat {0}.", toEat);
        while(true)
        { if(toEat.Eat(this)) { consumed=true; break; }
          if(Hunger<Food.MaxFoodPerTurn) break;
          if(ThinkUpdate(ref vis)) { App.IO.Print("You stop eating."); goto next; }
        }
        if(consumed && !split) inv.Remove(toEat);
        else if(split && !consumed) inv.Add(toEat);
        App.IO.Print(consumed ? "You finish eating." : "You feel full.");
        break;
      }

      case Action.Move:
      { Point np = Global.Move(Position, inp.Direction);
        Creature c = Map.GetCreature(np);
        if(c!=null) Attack(c);
        else if(Map.IsPassable(np))
        { Position = np;
          if(count<=1 && Map.HasItems(np)) App.IO.DisplayTileItems(Map[np].Items);
          inp.Count = count;
        }
        else goto next;
        break;
      }

      case Action.MoveToInteresting: // this needs to be improved
      { Point np = Global.Move(Position, inp.Direction);
        if(!Map.IsPassable(np)) goto next;
        Position = np;

        int dir = (int)inp.Direction, options=0;
        int[] chk = new int[5] { dir-2, dir-1, dir, dir+1, dir+2 };
        for(int i=0; i<5; i++)
        { if(chk[i]<0) chk[i] += 8;
          else if(chk[i]>=8) chk[i] -= 8;
          np = Global.Move(Position, chk[i]);
          if(Map.IsPassable(np) || Map.IsDoor(np)) options++;
        }
        while(true)
        { if(ThinkUpdate(ref vis)) goto next;
          TileType type = Map[np].Type;
          if(type==TileType.UpStairs || type==TileType.DownStairs) goto next;
          if(Map.HasItems(np)) { App.IO.DisplayTileItems(Map[np].Items); goto next; }

          int newopts=0;
          for(int i=0; i<5; i++)
          { np = Global.Move(Position, chk[i]);
            if(Map.IsPassable(np) || Map.IsDoor(np)) newopts++;
          }
          if(newopts!=options || IsMonsterVisible(vis)) goto next;

          np = Global.Move(Position, inp.Direction);
          if(Map.IsPassable(np) && !Map.IsDangerous(np)) Position = np;
          else goto next;
        }
      }

      case Action.Inventory: App.IO.DisplayInventory(Inv); goto next;

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
          foreach(MenuItem item in App.IO.Menu(inv, MenuFlag.AllowNum|MenuFlag.Multi|MenuFlag.Reletter))
          { if(item.Count==item.Item.Count) Pickup(inv, item.Item);
            else Pickup(item.Item.Split(item.Count));
          }
        break;
      }

      case Action.Quit:
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false)) App.Quit=true;
        break;

      case Action.Remove:
      { Inventory inv = new Inventory();
        for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) inv.Add(Slots[i]);
        if(inv.Count==0) { App.IO.Print("You're not wearing anything!"); goto next; }
        MenuItem[] items = App.IO.ChooseItem("Remove what?", inv, MenuFlag.Multi, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        foreach(MenuItem i in items) TryRemove(i.Item);
        break;
      }

      case Action.Rest:
        if(count>1)
        { bool fullHP=(HP==MaxHP), fullMP=(MP==MaxMP);
          while(--count>0)
            if(ThinkUpdate(ref vis) || IsMonsterVisible(vis) || !fullHP && HP==MaxHP || !fullMP && MP==MaxMP)
              goto next;
        }
        break;

      case Action.ShowMap: App.IO.DisplayMap(this); goto next;

      case Action.Wear:
      { MenuItem[] items = App.IO.ChooseItem("Wear what?", Inv, MenuFlag.None, wearableClasses);
        if(items.Length==0) goto nevermind;
        Wearable item = items[0].Item as Wearable;
        if(item==null) { App.IO.Print("You can't wear that!"); goto next; }
        if(Wearing(item)) { App.IO.Print("You're already wearing that!"); goto next; }
        if(Wearing(item.Slot) && !TryRemove(item.Slot)) goto next;
        Wear(item);
        break;
      }

      case Action.Wield:
      { MenuItem[] items = App.IO.ChooseItem("Wield what?", Inv, MenuFlag.AllowNothing, ItemClass.Weapon);
        if(items.Length==0) goto nevermind;
        if(items[0].Item==null) TryEquip(null);
        else
        { Wieldable item = items[0].Item as Wieldable;
          if(item==null) { App.IO.Print("You can't wield that!"); goto next; }
          if(Equipped(item)) { App.IO.Print("You're already wielding that!"); goto next; }
          TryEquip(item);
        }
        break;
      }
    }

    OnAge();
  }

  public static Player Generate(CreatureClass myClass, Race race)
  { if(race==Race.RandomRace) { race = (Race)Global.Rand((int)Race.NumRaces); }
    Player p = new Player();
    p.Race = race;
    p.Generate(0, myClass);
    return p;
  }

  public override void LevelUp()
  { base.LevelUp();
    if(Age>0) App.IO.Print(Color.Green, "You are now a level {0} {1}!", ExpLevel+1, Class);
  }

  protected virtual void OnAge()
  { Hunger++;
    if(Hunger==HungryAt || Hunger==StarvingAt || Hunger==StarveAt)
    { if(Hunger==HungryAt) App.IO.Print(Color.Warning, "You're getting hungry.");
      else if(Hunger==StarvingAt) App.IO.Print(Color.Dire, "You're starving!");
      else
      { App.IO.Print("The world grows dim and you faint from starvation. You don't wake up.");
        Die("starvation");
      }
      Interrupt();
    }
    
    Map.AddScent(X, Y);
    Map.SpreadScent();
  }

  public override void OnDrop(Item item) { App.IO.Print("You drop {0}.", item); }
  public override void OnEquip(Wieldable item) { App.IO.Print("You equip {0}.", item); }
  public override void OnHit(Creature hit, Weapon w, int damage)
  { App.IO.Print(damage>0 ? "You hit {0}." : "You hit {0}, but do no damage.", hit.theName);
  }
  public override void OnHitBy(Creature hit, Weapon w, int damage)
  { App.IO.Print(damage>0 ? "{0} hits you!" : "{0} hits you, but does no damage.", hit.TheName);
  }
  public override void OnKill(Creature killed) { App.IO.Print("You kill {0}!", killed.TheName); }
  public override void OnMiss(Creature hit, Weapon w) { App.IO.Print("You miss {0}.", hit.theName); }
  public override void OnMissBy(Creature hit, Weapon w) { App.IO.Print("{0} misses you.", hit.TheName); }
  public override void OnRemove(Wearable item) { App.IO.Print("You remove {0}.", item); }
  public override void OnUnequip(Wieldable item) { App.IO.Print("You unequip {0}.", item); }
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
    OnAge();
    if(interrupt) { interrupt=false; return true; }
    return false;
  }

  protected static readonly ItemClass[] wearableClasses =  new ItemClass[]
  { ItemClass.Amulet, ItemClass.Armor, ItemClass.Ring
  };

  Input inp;
}

} // namespace Chrono