using System;
using System.Drawing;

namespace Chrono
{

public class Player : Entity
{ public Player() { Color=Color.White; Timer=50; /*we get a headstart*/ }

  public override void Die(Entity killer, Item impl) { Die(killer.aName); }
  public override void Die(Death cause) { Die(cause.ToString().ToLower()); }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);
    LevelUp(); ExpLevel--; // players start with slightly higher stats
    ExpPool = (level+1)*25;
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
    { case Action.Carve:
      { Item item;
        IInventory inv;
        if(!GroundPackUse(typeof(Corpse), "Butcher", out item, out inv, ItemClass.Corpse)) goto next;
        Corpse corpse = (Corpse)item;
        if(corpse.Skeleton) { App.IO.Print("You can't find any usable meat on this skeleton."); goto next; }

        int turn=1, turns=(corpse.Weight+99)/100;
        corpse.CarveTurns++;
        if(turns>1) App.IO.Print("You begin carving the {0}.", corpse.Name);
        while(turn<turns)
        { corpse.CarveTurns=++turn;
          if(ThinkUpdate(ref vis)) break;
        }
        if(turn<turns) App.IO.Print("You stop carving the {0}.", corpse.Name);
        else
        { inv.Remove(corpse);
          Flesh food = new Flesh(corpse);
          food.Count = Global.Rand(turns/2)+1;
          inv.Add(food);
          if(turns>1) App.IO.Print("You finish carving the {0}.", corpse.Name);
        }
        break;
      }
      
      case Action.CloseDoor:
      { Direction dir = App.IO.ChooseDirection(false, false);
        if(dir!=Direction.Invalid)
        { Point newpos = Global.Move(Position, dir);
          Tile tile = Map[newpos];
          if(tile.Type==TileType.ClosedDoor) App.IO.Print("That door is already closed.");
          else if(tile.Type!=TileType.OpenDoor) App.IO.Print("There is no door there.");
          else if(Map.HasItems(newpos) || Map.GetEntity(newpos)!=null) App.IO.Print("The door is blocked.");
          else
          { Map.SetType(newpos, TileType.ClosedDoor);
            int noise = (10-Stealth) * 5;
            if(noise>0) Map.MakeNoise(newpos, this, Noise.Bang, (byte)noise);
            break;
          }
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

        Item item=null;
        IInventory inv=null;
        if(!GroundPackUse(typeof(Food), "Eat", out item, out inv, ItemClass.Food)) goto next;

        Food toEat = (Food)item;
        bool split=false, consumed=false, stopped=false;
        if(toEat.Count>1) { toEat = (Food)toEat.Split(1); split=true; }
        if(toEat.FoodLeft>Food.MaxFoodPerTurn) App.IO.Print("You begin to eat {0}.", toEat);
        while(true)
        { if(toEat.Eat(this)) { consumed=true; break; }
          if(Hunger<Food.MaxFoodPerTurn) break;
          if(ThinkUpdate(ref vis)) { App.IO.Print("You stop eating."); stopped=true; break; }
        }
        if(consumed && !split) inv.Remove(toEat);
        else if(split && !consumed) inv.Add(toEat);
        if(consumed) App.IO.Print("You finish eating.");
        if(Hunger<Food.MaxFoodPerTurn) App.IO.Print("You feel full.");
        if(stopped) goto next;
        break;
      }

      case Action.Fire:
      { Direction d = App.IO.ChooseDirection(false, false);
        if(d==Direction.Invalid) goto nevermind;
        Attack(d);
        break;
      }

      case Action.GoDown:
      { if(Map[Position].Type!=TileType.DownStairs) { App.IO.Print("You can't go down here!"); goto next; }
        Map.SaveMemory(Memory);
        Link link = Map.GetLink(Position);
        Map.Entities.Remove(this);
        App.Dungeon[App.CurrentLevel=link.ToLevel].Entities.Add(this);
        Position = link.To;
        break;
      }

      case Action.GoUp:
      { if(Map[Position].Type!=TileType.UpStairs) { App.IO.Print("You can't go up here!"); goto next; }
        if(App.CurrentLevel==0)
        { if(App.IO.YesNo("If you go up here, you will leave the dungeon. Are you sure?", false)) App.Quit=true;
        }
        else
        { Map.SaveMemory(Memory);
          Link link = Map.GetLink(Position);
          Map.Entities.Remove(this);
          App.Dungeon[App.CurrentLevel=link.ToLevel].Entities.Add(this);
          Position = link.To;
        }
        break;
      }

      case Action.Invoke:
      { Inventory inv = new Inventory();
        for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) inv.Add(Hands[i]);
        if(inv.Count==0) { App.IO.Print("You have no items equipped!"); goto next; }
        if(inv.Count==1) { Invoke(inv[0]); break; }
        MenuItem[] items = App.IO.ChooseItem("Invoke which item?", inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        Invoke(items[0].Item);
        break;
      }

      case Action.ManageSkills: App.IO.ManageSkills(this); goto next;

      case Action.Move:
      { Point np = Global.Move(Position, inp.Direction);
        Entity c = Map.GetEntity(np);
        if(c!=null) Attack(c);
        else if(Map.IsPassable(np))
        { Position = np;
          int noise = (10-Stealth)*12; // stealth = 0 to 10
          if(noise>0) Map.MakeNoise(np, this, Noise.Walking, (byte)noise);
          if(count<=1 && Map.HasItems(np)) App.IO.DisplayTileItems(Map[np].Items);
          inp.Count = count;
        }
        else goto next;
        break;
      }

      case Action.MoveToInteresting: // this needs to be improved
      { Point  np = Global.Move(Position, inp.Direction);
        int noise = (10-Stealth)*12; // stealth = 0 to 10
        if(!Map.IsPassable(np)) goto next;
        Position = np;
        if(noise>0) Map.MakeNoise(np, this, Noise.Walking, (byte)noise);

        int options=0;
        Direction dir = inp.Direction;
        Direction[] chk = new Direction[5] { dir-2, dir-1, dir, dir+1, dir+2 };
        for(int i=0; i<5; i++)
        { np = Global.Move(Position, chk[i]);
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
          if(newopts!=options) goto next;

          np = Global.Move(Position, inp.Direction);
          if(Map.IsPassable(np) && !Map.IsDangerous(np))
          { Position = np;
            if(noise>0) Map.MakeNoise(np, this, Noise.Walking, (byte)noise);
          }
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
          else
          { int noise = (10-Stealth) * 2;
            if(noise>0) Map.MakeNoise(newpos, this, Noise.Bang, (byte)noise);
            Map.SetType(newpos, TileType.OpenDoor); break;
          }
        }
        goto next;
      }

      case Action.Pickup:
      { if(!Map.HasItems(Position)) { App.IO.Print("There are no items here."); goto next; }
        if(Inv.IsFull) { App.IO.Print("Your pack is full."); goto next; }
        ItemPile inv = Map[Position].Items;
        if(inv.Count==1)
        { Item item = inv[0], newItem = Pickup(inv, 0);
          string s = string.Format("{0} - {1}", newItem.Char, item.FullName);
          if(item.Count!=newItem.Count) s += string.Format(" (now {0})", newItem.Count);
          App.IO.Print(s);
        }
        else
          foreach(MenuItem item in App.IO.Menu(inv, MenuFlag.AllowNum|MenuFlag.Multi|MenuFlag.Reletter))
          { Item newItem = item.Count==item.Item.Count ? Pickup(inv, item.Item) : Pickup(item.Item.Split(item.Count));
            string s = string.Format("{0} - {1}", newItem.Char, item.Item.FullName);
            if(item.Count!=newItem.Count) s += string.Format(" (now {0})", newItem.Count);
            App.IO.Print(s);
          }
        break;
      }

      case Action.Quaff:
      { Item potion;
        IInventory inv;
        if(!GroundPackUse(typeof(Potion), "Drink", out potion, out inv, ItemClass.Potion)) goto next;
        if(potion.Count>1) ((Potion)potion.Split(1)).Drink(this);
        else { inv.Remove(potion); ((Potion)potion).Drink(this); }
        break;
      }

      case Action.Quit:
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false)) App.Quit=true;
        break;

      case Action.Read:
      { Item read;
        IInventory inv;
        if(!GroundPackUse(typeof(Readable), "Read", out read, out inv, ItemClass.Scroll, ItemClass.Spellbook))
          goto next;
        Scroll scroll = read as Scroll;
        if(scroll != null)
        { if(scroll.Count>1) ((Scroll)scroll.Split(1)).Read(this);
          else { inv.Remove(scroll); scroll.Read(this); }
        }
        else
        { /* read spellbook */
        }
        break;
      }

      case Action.Reassign:
      { if(Inv.Count==0) { App.IO.Print("You're not carrying anything!"); goto next; }
        MenuItem[] items = App.IO.ChooseItem("Reassign which item?", Inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        char c;
        while(true)
        { c = App.IO.CharChoice("Reassign to?", null);
          if(c==0) goto nevermind;
          if(char.IsLetter(c)) break;
          else App.IO.Print("Invalid character.");
        }
        Item other = Inv[c];
        Inv.Remove(c);
        Inv.Remove(items[0].Item.Char);
        if(other!=null && other!=items[0].Item)
        { other.Char = items[0].Item.Char;
          Inv.Add(other);
          App.IO.Print("{0} - {1}", other.Char, other);
        }
        items[0].Item.Char = c;
        Inv.Add(items[0].Item);
        App.IO.Print("{0} - {1}", c, items[0].Item);
        goto next;
      }

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
            if(ThinkUpdate(ref vis) || !fullHP && HP==MaxHP || !fullMP && MP==MaxMP)
              goto next;
        }
        break;

      case Action.ShowMap: App.IO.DisplayMap(this); goto next;
      
      case Action.SwapAB:
      { Wieldable w=null;
        for(int i=0; i<Hands.Length; i++)
          if(Hands[i]!=null && (Hands[i].Char=='a' || Hands[i].Char=='b')) { w=Hands[i]; break; }
        if(w==null)
        { for(char c='a'; c<='b'; c++) if(Inv[c] is Wieldable && TryEquip((Wieldable)Inv[c])) goto done;
        }
        else
        { char c = w.Char=='a' ? 'b' : 'a';
          Item i = Inv[c];
          if(!(i is Wieldable) || !TryEquip((Wieldable)i))
          { App.IO.Print("No suitable item found to equip."); goto next;
          }
        }
        done: break;
      }

      case Action.UseItem:
      { Inventory inv = new Inventory();
        foreach(Item ii in Inv) if(ii.UseDirection || ii.UseTarget) inv.Add(ii);
        MenuItem[] items = App.IO.ChooseItem("Use which item?", inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        Item i = items[0].Item;
        bool consume;
        if(i.UseTarget)
        { RangeTarget r = App.IO.ChooseTarget(this, i.UseDirection);
          if(r.Dir==Direction.Invalid && r.Point.X==-1) goto nevermind;
          consume = r.Dir==Direction.Invalid ? i.Use(this, r.Point) : i.Use(this, r.Dir);
        }
        else
        { Direction d = App.IO.ChooseDirection();
          if(d==Direction.Invalid) goto nevermind;
          consume = i.Use(this, d);
        }
        if(consume)
        { if(i.Count>1) i.Count--;
          else Inv.Remove(i.Char);
        }
        break;
      }

      case Action.ViewItem:
      { MenuItem[] items = App.IO.ChooseItem("Examine which item?", Inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        App.IO.ViewItem(items[0].Item);
        goto next;
      }

      case Action.Wear:
      { MenuItem[] items = App.IO.ChooseItem("Wear what?", Inv, MenuFlag.None, wearableClasses);
        if(items.Length==0) goto nevermind;
        Wearable item = items[0].Item as Wearable;
        if(item==null) { App.IO.Print("You can't wear that!"); goto next; }
        if(Wearing(item)) { App.IO.Print("You're already wearing that!"); goto next; }
        if(item.Slot==Slot.Ring)
        { if(Wearing(Slot.LRing) && Wearing(Slot.RRing))
          { App.IO.Print("You're already wearing two rings!"); goto next;
          }
        }
        else if(Wearing(item.Slot) && !TryRemove(item.Slot)) goto next;
        Wear(item);
        break;
      }

      case Action.Wield:
      { MenuItem[] items = App.IO.ChooseItem("Wield what?", Inv, MenuFlag.AllowNothing, ItemClass.Weapon, ItemClass.Shield);
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

  public static Player Generate(EntityClass myClass, Race race)
  { if(race==Race.RandomRace) { race = (Race)Global.Rand((int)Race.NumRaces); }
    Player p = new Player();
    p.Race = race;
    p.Generate(0, myClass);
    return p;
  }

  public override void LevelUp()
  { base.LevelUp();
    if(Age>0) App.IO.Print(Color.Green, "You are now a level {0} {1}!", ExpLevel, Class);
  }

  protected bool GroundPackUse(Type type, string verb, out Item item, out IInventory inv, params ItemClass[] classes)
  { item = null; inv = null;
    if(Map.HasItems(Position))
    { Item[] items = Map[Position].Items.GetItems(classes);
      foreach(Item i in items)
        if(App.IO.YesNo(string.Format("There {0} {1} here. {2} {3}?", i.AreIs, i, verb, i.ItOne),
                        false))
        { item=i; inv=Map[Position].Items; break;
        }
    }
    if(item==null)
    { if(!Inv.Has(classes)) { App.IO.Print("You have nothing to {0}!", verb.ToLower()); return false; }
      MenuItem[] items = App.IO.ChooseItem(verb+" what?", Inv, MenuFlag.None, classes);
      if(items.Length==0) { App.IO.Print("Never mind."); return false; }
      if(!type.IsInstanceOfType(items[0].Item)) { App.IO.Print("You can't {0} that!", verb.ToLower()); return false; }
      item = items[0].Item;
      inv = Inv;
    }
    return true;
  }
  
  protected virtual void OnAge()
  { Hunger++;
    if(HungerLevel>oldHungerLevel)
    { if(HungerLevel==HungerLevel.Starved)
      { App.IO.Print("The world grows dim and you faint from starvation. You don't wake up.");
        Die("starvation");
      }
      else if(HungerLevel==HungerLevel.Starving) App.IO.Print(Color.Dire, "You're starving!");
      else if(HungerLevel==HungerLevel.Hungry)   App.IO.Print(Color.Warning, "You're getting hungry.");
      oldHungerLevel = HungerLevel;
      Interrupt();
    }
    
    Smell += Map.MaxScentAdd/100; // smelliness refills from 0 over 100 turns
    Map.AddScent(X, Y, Smell);
    Map.SpreadScent();
  }

  public override void OnAttrChange(Attr attribute, int amount, bool fromExercise)
  { string feel=null;
    switch(attribute)
    { case Attr.AC:      feel = amount>0 ? "tough"   : "frail"; break;
      case Attr.Dex:     feel = amount>0 ? "agile"   : "clumsy"; break;
      case Attr.EV:      feel = amount>0 ? "elusive" : "sluggish"; break;
      case Attr.Int:     feel = amount>0 ? "smart"   : "stupid"; break;
      case Attr.Light:   feel = amount>0 ? "aware"   : "unobservant"; break;
      case Attr.Speed:   feel = amount>0 ? "quick"   : "slow"; break;
      case Attr.Stealth: feel = amount>0 ? "cunning" : "exposed"; break;
      case Attr.Str:     feel = amount>0 ? "strong"  : "weak"; break;
    }
    if(feel==null) return;
    App.IO.Print(Color.Green, "You feel {0}!", feel);
  }
  public override void OnDrink(Potion potion) { App.IO.Print("You drink {0}.", potion); }
  public override void OnDrop(Item item) { App.IO.Print("You drop {0}.", item); }
  public override void OnEquip(Wieldable item) { App.IO.Print("You equip {0}.", item); }
  public override void OnHit(Entity hit, Weapon w, int damage)
  { App.IO.Print(damage>0 ? "You hit {0}." : "You hit {0}, but do no damage.", hit.theName);
  }
  public override void OnHitBy(Entity hit, Weapon w, int damage)
  { Interrupt();
    App.IO.Print(damage>0 ? "{0} hits you!" : "{0} hits you, but does no damage.", hit.TheName);
  }
  public override void OnInvoke(Item item) { App.IO.Print("You invoke {0}.", item); }
  public override void OnKill(Entity killed) { App.IO.Print("You kill {0}!", killed.TheName); }
  public override void OnMiss(Entity hit, Weapon w) { App.IO.Print("You miss {0}.", hit.theName); }
  public override void OnMissBy(Entity hit, Weapon w) { App.IO.Print("{0} misses you.", hit.TheName); }
  public override void OnNoise(Entity source, Noise type, int volume)
  { if(type==Noise.Alert) { App.IO.Print("You hear a shout!"); Interrupt(); }
  }
  public override void OnReadScroll(Scroll item) { App.IO.Print("You read {0}.", item); }
  public override void OnRemove(Wearable item) { App.IO.Print("You remove {0}.", item); }
  public override void OnSick(string howSick) { App.IO.Print(Color.Dire, "You feel {0}.", howSick); }
  public override void OnSkillUp(Skill skill)
  { App.IO.Print(Color.Green, "Your {0} skill went up!", skill.ToString().ToLower());
  }
  public override void OnUnequip(Wieldable item) { App.IO.Print("You unequip {0}.", item); }
  public override void OnWear(Wearable item)
  { if(item.EquipText!=null) App.IO.Print(item.EquipText);
    else App.IO.Print("You put on {0}.", item);
  }

  protected internal override void OnMapChanged()
  { base.OnMapChanged();
    if(Map==null) Memory=null;
    else
    { Memory = Map.RestoreMemory();
      if(Memory==null) Memory = new Map(Map.Width, Map.Height);
    }
  }

  protected virtual void Die(string cause)
  { App.IO.Print("You die.");
    App.IO.Print("Goodbye {0} the {1}... you were killed by: {2}", Name, Title, cause);
    if(!App.IO.YesNo("Die?", false))
    { App.IO.Print("Okay, you're alive again.");
      HP = MaxHP;
    }
    else App.Quit = true;
  }

  protected bool ThinkUpdate(ref Point[] vis)
  { Map.Simulate(this);
    base.Think();
    if(vis!=null) UpdateMemory(vis);
    vis = VisibleTiles();
    if(IsCreatureVisible(vis)) interrupt=true;
    OnAge();
    if(interrupt) { interrupt=false; return true; }
    return false;
  }
  
  protected static readonly ItemClass[] wearableClasses =  new ItemClass[]
  { ItemClass.Amulet, ItemClass.Armor, ItemClass.Ring
  };

  Input inp;
  HungerLevel oldHungerLevel;
}

} // namespace Chrono