using System;
using System.Drawing;

namespace Chrono
{

public class Player : Entity
{ public Player() { Color=Color.White; Timer=50; /*we get a headstart*/ }

  public override void Die(object killer, Death cause)
  { switch(cause)
    { case Death.Combat:
        if(killer is Entity) Die(((Entity)killer).aName);
        else Die(killer.ToString());
        break;
      case Death.Falling:
        if(killer is TileType)
        { TileType tile = (TileType)killer;
          if(tile==TileType.DownStairs) Die("falling down stairs");
          else if(tile==TileType.Pit) Die("falling into a pit");
          else Die("falling");
        }
        else Die("falling");
        break;
      case Death.Poison: case Death.Sickness:
      { string prefix = cause==Death.Poison ? "poisoned by " : "sickened by ";
        if(killer is Entity) Die(prefix + ((Entity)killer).aName);
        else if(killer is Food)
        { Food food = (Food)killer;
          if((food.Flags&Food.Flag.Rotten)!=0) prefix += "rotten ";
          if((food.Flags&Food.Flag.Tainted)!=0) prefix += "tainted ";
          Die(prefix+"food");
        }
        else if(killer is Item) Die(prefix + ((Item)killer).FullName);
        else Die(cause==Death.Poison ? "poison" : "sickness");
        break;
      }
      case Death.Starvation: Die("starvation"); break;
      case Death.Trap: Die("TRAP - finish me"); break;
      default: Die("unknown"); break;
    }
  }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);
    LevelUp(); ExpLevel--; // players start with slightly higher stats
    ExpPool = (level+1)*25;
  }

  public override void Think()
  { if(interrupt) { inp.Count=0; interrupt=false; }
    base.Think();
    Point[] vis = VisibleTiles(); UpdateMemory(vis);
    goto next;

    nevermind: App.IO.Print("Never mind."); goto next;
    carrytoomuch: App.IO.Print("You're carrying too much!"); goto next;

    next:
    int count = inp.Count;
    if(--count<=0)
    { UpdateMemory(vis);
      App.IO.Render(this);
      inp = Is(Flag.Asleep) ? new Input(Action.Rest) : App.IO.GetNextInput();
      count = inp.Count;
    }
    inp.Count=0; // inp.Count drops to zero unless set to 'count' by an action

    switch(inp.Action)
    { case Action.Carve:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Item item;
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
        if(turn<turns) { App.IO.Print("You stop carving the {0}.", corpse.Name); goto next; }
        else
        { inv.Remove(corpse);
          Flesh food = new Flesh(corpse);
          food.Count = Global.Rand(turns/2)+1;
          if(turns>1) App.IO.Print("You finish carving the {0}.", corpse.Name);
          if(inv.Add(food)==null)
          { App.IO.Print("The {0} does not fit in your pack, so you put it down.", food);
            Map.AddItem(Position, food);
          }
        }
        break;
      }
      
      case Action.CloseDoor:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Direction dir = App.IO.ChooseDirection(false, false);
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
        if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;

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
        else if(split && !consumed && inv.Add(toEat)==null)
        { App.IO.Print("The {0} does not fit in your pack, so you put it down.", toEat);
          Map.AddItem(Position, toEat);
        }
        if(consumed) App.IO.Print("You finish eating.");
        if(Hunger<Food.MaxFoodPerTurn) App.IO.Print("You feel full.");
        if(stopped) goto next;
        break;
      }

      case Action.ExamineTile: App.IO.ExamineTile(this, Position); goto next;

      case Action.Fire:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Weapon w = Weapon;
        if(w!=null && w.Ranged)
        { Ammo ammo;
          if(w.wClass==WeaponClass.Thrown) ammo=null;
          else
          { ammo = SelectAmmo(w);
            if(ammo==null) { App.IO.Print("You have no suitable ammunition!"); goto next; }
          }
          RangeTarget rt = App.IO.ChooseTarget(this, true);
          if(rt.Dir!=Direction.Invalid) Attack(rt.Dir, w, ammo);
          else if(rt.Point.X!=-1) Attack(rt.Point, w, ammo);
          else goto nevermind;
        }
        else
        { Direction d = App.IO.ChooseDirection(false, false);
          if(d==Direction.Invalid) goto nevermind;
          Attack(d, w, null);
        }
        break;
      }

      case Action.GoDown:
      { CarryStress stress = CarryStress;
        if(Map[Position].Type!=TileType.DownStairs) { App.IO.Print("You can't go down here!"); goto next; }
        Map.SaveMemory(Memory);
        Link link = Map.GetLink(Position);
        Map.Entities.Remove(this);
        App.Dungeon[App.CurrentLevel=link.ToLevel].Entities.Add(this);
        Position = link.To;
        if(stress>=CarryStress.Stressed || stress==CarryStress.Burdened && Global.Coinflip())
        { App.IO.Print("You fall down the stairs!");
          DoDamage(Death.Falling, Global.NdN((int)stress, 20));
        }
        break;
      }

      case Action.GoUp:
      { if(CarryStress>CarryStress.Stressed) goto carrytoomuch;
        if(Map[Position].Type!=TileType.UpStairs) { App.IO.Print("You can't go up here!"); goto next; }
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
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Inventory inv = new Inventory();
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
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Point np = Global.Move(Position, inp.Direction);
        Entity c = Map.GetEntity(np);
        if(c!=null)
        { Weapon w = Weapon;
          Ammo   a = SelectAmmo(w);
          if(w!=null && a==null && w.Ranged && w.wClass!=WeaponClass.Thrown)
            App.IO.Print(Color.Warning, "You're out of "+((FiringWeapon)w).AmmoName+'!');
          Attack(np, w, a);
        }
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
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Point  np = Global.Move(Position, inp.Direction);
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
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Direction dir = App.IO.ChooseDirection(false, false);
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
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        if(!Map.HasItems(Position)) { App.IO.Print("There are no items here."); goto next; }
        if(Inv.IsFull) { App.IO.Print("Your pack is full."); goto next; }
        ItemPile inv = Map[Position].Items;
        CarryStress stress = CarryStress, newstress;
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
        newstress = CarryStress;
        if(newstress>stress)
          App.IO.Print(newstress==CarryStress.Burdened ? Color.Warning : Color.Dire,
                       "You are {0}{1}", newstress.ToString().ToLower(), newstress==CarryStress.Burdened ? '.' : '!');
        break;
      }

      case Action.Quaff:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        Item potion;
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
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        Item read;
        IInventory inv;
        if(!GroundPackUse(typeof(Readable), "Read", out read, out inv, ItemClass.Scroll, ItemClass.Spellbook))
          goto next;
        Scroll scroll = read as Scroll;
        if(scroll != null) // read scroll
        { if(scroll.Count>1) ((Scroll)scroll.Split(1)).Read(this);
          else { inv.Remove(scroll); scroll.Read(this); }
        }
        else // read spellbook
        { Spellbook book = (Spellbook)read;
          if(book.Reads==0) { App.IO.Print("This spellbook is too worn to read."); goto next; }
          if(book.Spells.Length==0) { App.IO.Print("This spellbook is empty!"); goto next; }
          Spell spell = App.IO.ChooseSpell(this, book);
          if(spell==null) goto nevermind;
          int knowledge = SpellKnowledge(spell), chance = Math.Max(knowledge/50, spell.LearnChance(this));
          if(knowledge>0)
          { if(!App.IO.YesNo("You already know this spell. Refresh your memory?", false)) goto next;
          }
          else if(chance<25)
          { if(!App.IO.YesNo("This spell seems very difficult. Continue?", false)) goto next;
          }
          else if(chance<50 && !App.IO.YesNo("This spell seems difficult. Continue?", false)) goto next;

          bool success = Global.Rand(100)<chance;
          if(success) // succeeded
          { int turns = spell.Level;
            while(--turns>0 && !ThinkUpdate(ref vis));
            if(turns>0) { App.IO.Print("Your concentration is broken!"); goto next; } // don't use up the book
          }

          book.Reads--;
          if(book.Reads<5) App.IO.Print("This spellbook is getting extremely worn!");
          else if(book.Reads<10) App.IO.Print("This spellbook is getting worn.");

          if(success)
          { int newknow = Math.Max(Math.Min(chance, 100)*100, 1000);
            if(newknow>knowledge)
            { MemorizeSpell(spell, newknow);
              App.IO.Print("You {0} the {1} spell.", knowledge>0 ? "refresh your memory of" : "memorize", spell.Name);
            }
            else App.IO.Print("You don't feel that your knowledge of that spell has improved.");
          }
          else
          { App.IO.Print("Something has gone wrong! Dark energies cloud your mind.");
            int effect = Global.Rand(chance);
            AddEffect(new Effect(book, Flag.Confused, 100-effect));
            if(effect<30) TeleportSpell.Default.Cast(this);
            if(effect<10) AmnesiaSpell.Default.Cast(this);
            if(effect<5)
            { App.IO.Print("You start to shake uncontrollably, and the world goes dark.");
              AddEffect(new Effect(book, Flag.Asleep, Global.NdN(4, 15)));
            }
          }
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
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        Inventory inv = new Inventory();
        for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) inv.Add(Slots[i]);
        for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null && Hands[i].Class==ItemClass.Shield) inv.Add(Hands[i]);
        if(inv.Count==0) { App.IO.Print("You're not wearing anything!"); goto next; }
        MenuItem[] items = App.IO.ChooseItem("Remove what?", inv, MenuFlag.Multi, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        foreach(MenuItem i in items)
          if(i.Item.Class==ItemClass.Shield) TryUnequip(i.Item);
          else TryRemove(i.Item);
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

      case Action.ShowMap:
      { Point pt = App.IO.DisplayMap(this);
        if(pt.X==-1 || Position==pt || !path.Plan(Memory, Position, pt)) goto next;
        PathNode node = path.GetPathFrom(Position);
        do
        { Point od = node.Parent.Point;
          TileType type = Map[od].Type;
          if(Map.IsPassable(type)) { Position = od; node = node.Parent; }
          else
          { if(type==TileType.ClosedDoor)
            { if(Map.GetFlag(od, Tile.Flag.Locked))
              { App.IO.Print("This door is locked.");
                break;
              }
              Map.SetType(od, TileType.OpenDoor);
              continue;
            }
            else
            { if(!path.Plan(Memory, Position, pt)) break;
              node = path.GetPathFrom(Position);
              if(node.Parent.Point==od) break;
            }
          }
        } while(!ThinkUpdate(ref vis) && Position!=pt);
        goto next;
      }
      
      case Action.SwapAB:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Wieldable w=null;
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

      case Action.Throw:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Throw which item?", Inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        if(Wearing(items[0].Item)) { App.IO.Print("You can't throw something you're wearing!"); goto next; }
        RangeTarget rt = App.IO.ChooseTarget(this, true);
        if(rt.Dir!=Direction.Invalid) ThrowItem(items[0].Item, rt.Dir);
        else if(rt.Point.X!=-1) ThrowItem(items[0].Item, rt.Point);
        else goto nevermind;
        break;
      }

      case Action.UseItem:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Inventory inv = new Inventory();
        foreach(Item ii in Inv) if(ii.Usability!=ItemUse.NoUse) inv.Add(ii);
        if(inv.Count==0) { App.IO.Print("You have no useable items."); goto next; }
        MenuItem[] items = App.IO.ChooseItem("Use which item?", inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        Item i = items[0].Item;
        bool consume;
        if((i.Usability&ItemUse.UseTarget)!=0)
        { RangeTarget r = App.IO.ChooseTarget(this, (i.Usability&ItemUse.UseDirection)!=0);
          if(r.Dir==Direction.Invalid && r.Point.X==-1) goto nevermind;
          consume = r.Dir==Direction.Invalid ? i.Use(this, r.Point) : i.Use(this, r.Dir);
        }
        else if(i.Usability==ItemUse.UseDirection)
        { Direction d = App.IO.ChooseDirection();
          if(d==Direction.Invalid) goto nevermind;
          consume = i.Use(this, d);
        }
        else consume = i.Use(this, Direction.Self);
        if(consume)
        { if(i.Count>1) i.Count--;
          else Inv.Remove(i.Char);
        }
        break;
      }

      case Action.ViewItem:
      { MenuItem[] items = App.IO.ChooseItem("Examine which item?", Inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        App.IO.ExamineItem(this, items[0].Item);
        goto next;
      }

      case Action.Wear:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Wear what?", Inv, MenuFlag.None, wearableClasses);
        if(items.Length==0) goto nevermind;
        if(items[0].Item.Class==ItemClass.Shield)
        { Shield shield = (Shield)items[0].Item;
          if(Equipped(shield)) { App.IO.Print("That shield is already equipped!"); goto next; }
          TryEquip(shield);
        }
        else
        { Wearable item = items[0].Item as Wearable;
          if(item==null) { App.IO.Print("You can't wear that!"); goto next; }
          if(Wearing(item)) { App.IO.Print("You're already wearing that!"); goto next; }
          if(item.Slot==Slot.Ring)
          { if(Wearing(Slot.LRing) && Wearing(Slot.RRing))
            { App.IO.Print("You're already wearing two rings!"); goto next;
            }
          }
          else if(Wearing(item.Slot) && !TryRemove(item.Slot)) goto next;
          Wear(item);
        }
        break;
      }

      case Action.Wield:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Wield what?", Inv, MenuFlag.AllowNothing, ItemClass.Weapon);
        if(items.Length==0) goto nevermind;
        if(items[0].Item==null) TryEquip(null);
        else
        { Weapon item = items[0].Item as Weapon;
          if(item==null) { App.IO.Print("You can't wield that!"); goto next; }
          if(Equipped(item)) { App.IO.Print("You're already wielding that!"); goto next; }
          TryEquip(item);
        }
        break;
      }
      
      case Action.ZapWand:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Zap what?", Inv, MenuFlag.None, ItemClass.Wand);
        if(items.Length==0) goto nevermind;
        Wand wand = items[0].Item as Wand;
        if(wand==null) { App.IO.Print("You can't zap that!"); goto next; }
        RangeTarget rt = App.IO.ChooseTarget(this, wand.Spell, true); // FIXME: should be null if not identified
        bool destroy;
        if(rt.Dir!=Direction.Invalid)
        { Point np = rt.Dir>=Direction.Above ? Position : Global.Move(Position, rt.Dir);
          destroy=wand.Zap(this, np, rt.Dir);
        }
        else if(rt.Point.X!=-1) destroy=wand.Zap(this, rt.Point);
        else goto nevermind;
        if(destroy) Inv.Remove(wand);
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
        Die(Death.Starvation);
      }
      else if(HungerLevel==HungerLevel.Starving) App.IO.Print(Color.Dire, "You're starving!");
      else if(HungerLevel==HungerLevel.Hungry)   App.IO.Print(Color.Warning, "You're getting hungry.");
      oldHungerLevel = HungerLevel;
      Interrupt();
    }
    
    Smell += Map.MaxScentAdd/200; // smelliness refills from 0 over 200 turns
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
  public override void OnFlagsChanged(Chrono.Entity.Flag oldFlags, Chrono.Entity.Flag newFlags)
  { Flag diff = oldFlags ^ newFlags;
    if((diff&newFlags&Flag.Asleep)!=0) App.IO.Print("You wake up.");
    if((diff&Flag.Confused)!=0)
      App.IO.Print((newFlags&Flag.Confused)==0 ? "Your head clears a bit." : "You stumble, confused.");
    if((diff&Flag.Hallucinating)!=0)
      App.IO.Print((newFlags&Flag.Hallucinating)==0 ? "Everything looks SO boring now." : "Whoa, trippy, man!");
    if((diff&(Flag.Invisible|Flag.SeeInvisible))!=0)
    { if((diff&Flag.Invisible)!=0) // invisibility changed
      { if((newFlags&Flag.Invisible)!=0 && (newFlags&Flag.SeeInvisible)==0) App.IO.Print("You vanish from sight.");
        else if((newFlags&Flag.Invisible)==0 && (oldFlags&newFlags&Flag.SeeInvisible)==0)
          App.IO.Print("Suddenly you can see yourself again.");
      }
      else if((newFlags&Flag.SeeInvisible)==0)
      { if((newFlags&Flag.Invisible)!=0) App.IO.Print("You vanish from sight.");
      }
      else if((oldFlags&newFlags&Flag.Invisible)!=0) App.IO.Print("Suddenly, you can see yourself again.");
    }
  }

  public override void OnHit(Entity hit, object item, int damage)
  { if(item==null || item is Weapon)
      App.IO.Print(damage>0 ? "You hit {0}." : "You hit {0}, but do no damage.", hit==this ? "yourself" : hit.theName);
    else if(item is Spell)
      App.IO.Print(damage>0 ? "The spell hits {0}." : "The spell hits {0}, but {1} unaffected.",
                   hit==this ? "you" : hit.theName, hit==this ? "you are" : "it appears");
  }
  public override void OnHitBy(Entity attacker, object item, int damage)
  { Interrupt();
    if(attacker!=this)
      App.IO.Print(damage>0 ? "{0} hits you!" : "{0} hits you, but does no damage.", attacker.TheName);
  }
  public override void OnInvoke(Item item) { App.IO.Print("You invoke {0}.", item); }
  public override void OnKill(Entity killed) { App.IO.Print("You kill {0}!", killed.TheName); }
  public override void OnMiss(Entity hit, object item)
  { if(item==null || item is Weapon) App.IO.Print("You miss {0}.", hit.theName);
    else if(item is Spell && CanSee(hit)) 
      App.IO.Print("The spell {0} {1}.", Global.Coinflip() ? "misses" : "whizzes by", hit==this ? "you" : hit.theName);
  }
  public override void OnMissBy(Entity attacker, object item)
  { if(attacker!=this) App.IO.Print("{0} misses you.", attacker.TheName);
  }
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
  public override void OnWear(Wearable item) { App.IO.Print("You put on {0}.", item); }

  protected internal override void OnMapChanged()
  { base.OnMapChanged();
    if(Map==null) Memory=null;
    else
    { Memory = Map.RestoreMemory();
      if(Memory==null) Memory = new Map(Map.Width, Map.Height, TileType.Border, false);
    }
  }

  protected virtual void Die(string cause)
  { App.IO.Print("You die.");
    if(Is(Flag.Asleep)) cause += ", while sleeping";
    App.IO.Print("Goodbye {0} the {1}...", Name, Title);
    App.IO.Print("You were killed by: "+cause);
    if(!App.IO.YesNo("Die?", false))
    { App.IO.Print("Okay, you're alive again.");
      HP = MaxHP;
    }
    else App.Quit = true;
  }

  protected bool ThinkUpdate(ref Point[] vis)
  { Map.Simulate(this);
    base.Think();
    vis = VisibleTiles();
    UpdateMemory(vis);
    if(IsCreatureVisible(vis)) interrupt=true;
    OnAge();
    if(interrupt) { interrupt=false; return true; }
    return false;
  }
  
  protected static readonly ItemClass[] wearableClasses =  new ItemClass[]
  { ItemClass.Amulet, ItemClass.Armor, ItemClass.Ring, ItemClass.Shield
  };

  Input inp;
  HungerLevel oldHungerLevel;
  PathFinder path = new PathFinder();
}

} // namespace Chrono