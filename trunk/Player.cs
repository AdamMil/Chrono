using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

[Serializable]
public class Player : Entity
{ public Player() { Color=Color.White; Timer=50; SocialGroup=Global.NewSocialGroup(false, true); }
  public Player(SerializationInfo info, StreamingContext context) : base(info, context) { }

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
        else if(killer is Item) Die(prefix + ((Item)killer).GetAName(this));
        else Die(cause==Death.Poison ? "poison" : "sickness");
        break;
      }
      case Death.Quit: Die("giving up"); break;
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

  public override void OnDrink(Potion potion) { App.IO.Print("You drink {0}.", potion.GetAName(this)); }

  public override bool OnDrop(Item item)
  { App.IO.Print("You drop {0}.", item.GetAName(this));
    if(item.Shop==null)
    { Shop shop = Map.GetShop(Position);
      if(shop!=null && shop.Shopkeeper!=null)
      { if(item.Class==ItemClass.Gold)
        { int itemcost = shop.Shopkeeper.GetPlayerBill(this, false);
          if(shop.Shopkeeper.Credit<0)
          { int take = Math.Min(-shop.Shopkeeper.Credit, item.Count);
            shop.Shopkeeper.Say("{0}I'll cancel {1} gold from your debt.", itemcost==0 ? "" : "First, ", take);

            shop.Shopkeeper.GiveCredit(take);
            if(take<item.Count) shop.Shopkeeper.Pickup(item.Split(take));
            else { shop.Shopkeeper.Pickup(item); return false; }
            if(itemcost==0) return true;
          }

          if(itemcost==0)
          { if(App.IO.YesNo("Would you like me to take that money and give you credit?", false))
            { shop.Shopkeeper.Pickup(item);
              shop.Shopkeeper.Say("Okay, you have {0} credit.", shop.Shopkeeper.Credit);
              return false;
            }
          }
          else if(itemcost>item.Count)
            shop.Shopkeeper.Say("That's not enough to cover your debt. Here, take your money back. Either give me "+
                                "the full amount, or talk to me and we'll work something out.");
          else
          { shop.Shopkeeper.ClearUnpaidItems(Inv);
            if(itemcost==item.Count)
            { shop.Shopkeeper.Pickup(item);
              shop.Shopkeeper.Say("Thanks, that covers everything.");
              return false;
            }
            else
            { shop.Shopkeeper.Pickup(item.Split(itemcost));
              shop.Shopkeeper.Say("Thanks, that covers everything. Don't forget to pick up your change!");
            }
          }
        }
        else
        { int price = shop.Shopkeeper.BuyCost(item);
          if(price==0) App.IO.Print("{0} seems uninterested in that.", shop.Shopkeeper.TheName);
          else if(App.IO.YesNo(string.Format("{0} offers you {1} gold for that. Accept?",
                                              shop.Shopkeeper.TheName, price), true))
          { if(shop.Shopkeeper.Credit<0)
            { int take = Math.Min(-shop.Shopkeeper.Credit, price);
              shop.Shopkeeper.Say("{0}I'll cancel {1} gold from your debt.", take==price ? "" : "First, ", take);
              shop.Shopkeeper.GiveCredit(take);
              price -= take;
            }

            Gold gold = shop.Shopkeeper.GetGold(price, true);
            int count = gold==null ? 0 : gold.Count;
            if(gold!=null) Pickup(gold);
            if(count<price)
            { shop.Shopkeeper.Say("Sorry, I don't have enough money, but I'll give you {0} gold "+
                                  "and {1} additional credit at my shop.", count, price-count);
              shop.Shopkeeper.GiveCredit(price-count);
            }
            item.Shop = shop;
          }
        }
      }
    }

    return true;
  }

  public override void OnEquip(Wieldable item)
  { App.IO.Print("You equip {0}.", item.GetAName(this));
    if(item.Cursed)
    { App.IO.Print("The {0} welds itself to your {1}!", item.Name, item.Class==ItemClass.Shield ? "arm" : "hand");
      item.Status |= ItemStatus.KnowCB;
    }
  }

  public override void OnFlagsChanged(Chrono.Entity.Flag oldFlags, Chrono.Entity.Flag newFlags)
  { Flag diff = oldFlags ^ newFlags;
    if((diff&oldFlags&Flag.Asleep)!=0) App.IO.Print("You wake up.");
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

  public override void OnHit(Entity hit, object item, Damage damage)
  { int dam = damage.Total;
    if(hit!=this && !CanSee(hit))
      App.IO.Print("{0} it.", item is Spell ? "The spell hits" : "You hit");
    else if(item==null || item is Weapon)
      App.IO.Print(dam>0 ? "You hit {0}." : "You hit {0}, but do no damage.",
                   hit==this ? "yourself" : hit.theName);
    else if(item is Spell)
      App.IO.Print(dam>0 ? "The spell hits {0}." : "The spell hits {0}, but {1} unaffected.",
                   hit==this ? "you" : hit.theName, hit==this ? "you are" : "it appears");
  }

  public override void OnHitBy(Entity attacker, object item, Damage damage)
  { Interrupt();
    if(attacker!=this) App.IO.Print(damage.Total>0 ? "{0} hits you!" : "{0} hits you, but does no damage.",
                                    item is Spell ? "The spell" : CanSee(attacker) ? attacker.TheName : "It");
  }

  public override void OnInvoke(Item item) { App.IO.Print("You invoke {0}.", item.GetAName(this)); }
  public override void OnKill(Entity killed) { App.IO.Print("You kill {0}!", killed.theName); }

  public override void OnMapChanged()
  { base.OnMapChanged();
    if(Map==null) Memory=null;
    else
    { Memory = Map.RestoreMemory();
      if(Memory==null) Memory = new Map(Map.Width, Map.Height, TileType.Border, false);
    }
  }

  public override void OnMiss(Entity hit, object item)
  { if(item==null || item is Weapon) App.IO.Print("You miss {0}.", hit.theName);
    else if(item is Spell && CanSee(hit)) 
      App.IO.Print("The spell {0} {1}.", Global.Coinflip() ? "misses" : "whizzes by", hit==this ? "you" : hit.theName);
  }

  public override void OnMissBy(Entity attacker, object item)
  { if(Global.Coinflip()) Interrupt();
    if(attacker!=this)
      App.IO.Print("{0} misses you.", item is Spell ? "The spell" : CanSee(attacker) ? attacker.TheName : "It");
  }
  
  public override void OnMove(Point newPos, Map newMap)
  { Shop oldShop=Map.GetShop(Position);
    if(oldShop==null)
    { Shop newShop = newMap==null ? Map.GetShop(newPos) : newMap.GetShop(newPos);
      if(newShop!=null)
      { if(newShop.Shopkeeper==null) App.IO.Print("This shop appears deserted.");
        else newShop.Shopkeeper.GreetPlayer(this);
      }
    }
    else if(oldShop.Shopkeeper!=null)
    { if(newMap!=null || Map.GetShop(newPos)!=oldShop) oldShop.Shopkeeper.PlayerLeft(this);
      else if(newPos==oldShop.Door)
      { int bill = oldShop.Shopkeeper.GetPlayerBill(this);
        if(bill>0)
        { int credit = oldShop.Shopkeeper.Credit;
          oldShop.Shopkeeper.Say("{0}! You owe me {1} gold. Please pay before you leave{2}.",
                                 Name, bill, credit>0 ? " (you have "+credit+" credit)" : "");
        }                         
      }
    }
    base.OnMove(newPos, newMap);
  }

  public override void OnNoise(Entity source, Noise type, int volume)
  { if(type==Noise.Alert) { App.IO.Print("You hear a shout!"); Interrupt(); }
  }

  public override void OnPickup(Item item, IInventory from)
  { if(item.Shop!=null && from!=null && item.Shop.Shopkeeper!=null && from==Map[Position].Items)
      item.Shop.Shopkeeper.PlayerTook(item);
  }

  public override void OnReadScroll(Scroll item) { App.IO.Print("You read {0}.", item.GetAName(this)); }
  public override void OnRemove(Wearable item) { App.IO.Print("You remove {0}.", item.GetAName(this)); }
  public override void OnSick(string howSick) { App.IO.Print(Color.Dire, "You feel {0}.", howSick); }

  public override void OnSkillUp(Skill skill)
  { App.IO.Print(Color.Green, "Your {0} skill went up!", skill.ToString().ToLower());
  }

  public override void OnUnequip(Wieldable item) { App.IO.Print("You unequip {0}.", item.GetAName(this)); }
  public override void OnWear(Wearable item) { App.IO.Print("You put on {0}.", item.GetAName(this)); }

  public void Quit() { HP=0; App.Quit=true; Die(Death.Quit); }
  public void Save() { App.Quit=true; }

  public override void TalkTo() { App.IO.Print("Talking to yourself again?"); }

  public override void Think()
  { if(Map.IsOverworld) OverworldThink();
    else DungeonThink();
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
  
  protected const int OverworldMoveTime=100;

  #region DefaultHandler
  protected bool DefaultHandler(ref Point[] vis)
  { switch(inp.Action)
    { case Action.Drop: case Action.DropType:
      { if(Inv.Count==0) { App.IO.Print("You're not carrying anything."); return false; }
        if(Map.IsOverworld)
          App.IO.Print(Color.Warning, "Warning: Items dropped here will disappear if you leave the area.");
        CarryStress stress = CarryStress, newstress;
        MenuItem[] items;
        if(inp.Action==Action.Drop)
          items = App.IO.ChooseItem("Drop what?", this, MenuFlag.AllowNum|MenuFlag.Multi, ItemClass.Any);
        else
        { System.Collections.ArrayList list = new System.Collections.ArrayList();
          list.Add(new MenuItem("All types", 'a'));
          char c = 'b';
          for(int i=0; i<(int)ItemClass.NumClasses; c++,i++)
            foreach(Item item in Inv)
              if(item.Class==(ItemClass)i) { list.Add(new MenuItem(item.Class.ToString(), c)); break; }
          list.Add(new MenuItem("Auto-select every item", 'A'));
          foreach(Item item in Inv)
            if(item.KnownBlessed) { list.Add(new MenuItem("Items known to be blessed", 'B')); break; }
          foreach(Item item in Inv)
            if(item.KnownCursed) { list.Add(new MenuItem("Items known to be cursed", 'C')); break; }
          foreach(Item item in Inv)
            if(item.Shop!=null) { list.Add(new MenuItem("Unpaid items", 'P')); break; }
          foreach(Item item in Inv)
            if(item.KnownUncursed) { list.Add(new MenuItem("Items known to be uncursed", 'U')); break; }
          foreach(Item item in Inv)
            if(item.CBUnknown) { list.Add(new MenuItem("Items of unknown B/C/U status", 'X')); break; }

          items = App.IO.Menu((MenuItem[])list.ToArray(typeof(MenuItem)), MenuFlag.Multi);
          if(items.Length==0) goto nevermind;
          foreach(MenuItem item in items)
            if(item.Char=='A')
            { for(int i=0; i<Inv.Count; i++)
                if((!Wearing(Inv[i]) || TryRemove(Inv[i])) && (!Equipped(Inv[i]) || TryUnequip(Inv[i])))
                { Drop(Inv[i].Char, Inv[i].Count);
                  i--;
                }
              goto done;
            }

          Inventory inv = new Inventory();

          list.Clear();
          foreach(MenuItem item in items)
            if(char.ToLower(item.Char)==item.Char)
              if(item.Char!='a') list.Add(Enum.Parse(typeof(ItemClass), item.Text));
              else
              { foreach(Item i in Inv) inv.Add(i);
                goto menu;
              }

          if(list.Count>0)
          { Item[] mi = Inv.GetItems((ItemClass[])list.ToArray(typeof(ItemClass)));
            list.Clear();
            for(int i=0; i<mi.Length; i++) list.Add(mi[i]);
          }

          foreach(MenuItem item in items)
            switch(item.Char)
            { case 'B': foreach(Item i in Inv) if(i.KnownBlessed  && !list.Contains(i)) list.Add(i); break;
              case 'C': foreach(Item i in Inv) if(i.KnownCursed   && !list.Contains(i)) list.Add(i); break;
              case 'P': foreach(Item i in Inv) if(i.Shop!=null    && !list.Contains(i)) list.Add(i); break;
              case 'U': foreach(Item i in Inv) if(i.KnownUncursed && !list.Contains(i)) list.Add(i); break;
              case 'X': foreach(Item i in Inv) if(i.CBUnknown     && !list.Contains(i)) list.Add(i); break;
            }

          foreach(Item i in list) inv.Add(i);
          menu: items = App.IO.Menu(this, inv, MenuFlag.Multi|MenuFlag.AllowNum, ItemClass.Any);
        }
        if(items.Length==0) goto nevermind;
        foreach(MenuItem i in items)
          if((!Wearing(i.Item) || TryRemove(i.Item)) && (!Equipped(i.Item) || TryUnequip(i.Item)))
            Drop(i.Char, i.Count);

        done:
        newstress = CarryStress;
        if(newstress<stress)
          switch(newstress)
          { case CarryStress.Normal: App.IO.Print("Your actions are no longer burdened."); break;
            default: App.IO.Print("You are still {0}.", newstress.ToString().ToLower()); break;
          }
        break;
      }

      case Action.Eat:
      { if(Hunger<Food.MaxFoodPerTurn) { App.IO.Print("You're still full."); return false; }
        if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;

        Item item=null;
        IInventory inv=null;
        if(!GroundPackUse(typeof(Food), "Eat", out item, out inv, ItemClass.Food)) return false;

        Food toEat = (Food)item;
        bool split=false, consumed=false, stopped=false;
        if(toEat.Count>1) { toEat=(Food)toEat.Split(1); split=true; }
        if(toEat.FoodLeft>Food.MaxFoodPerTurn) App.IO.Print("You begin to eat {0}.", toEat.GetAName(this));
        Use(toEat);
        while(true)
        { Map.MakeNoise(Position, this, Noise.Item, toEat.GetNoise(this));
          if(toEat.Eat(this)) { consumed=true; break; }
          if(Hunger<Food.MaxFoodPerTurn) break;
          if(ThinkUpdate(ref vis)) { App.IO.Print("You stop eating."); stopped=true; break; }
        }

        if(consumed && !split) inv.Remove(toEat);
        else if(split && !consumed && inv.Add(toEat)==null)
        { App.IO.Print("The {0} does not fit in your pack, so you put it down.", toEat.GetFullName(this));
          Map.AddItem(Position, toEat);
        }

        if(consumed) App.IO.Print("You finish eating.");
        if(Hunger<Food.MaxFoodPerTurn) App.IO.Print("You feel full.");
        if(stopped) return false;
        break;
      }

      case Action.ExamineTile: App.IO.ExamineTile(this, Position); return false;

      case Action.GoDown: case Action.GoUp:
      { TileType type = Map[Position].Type;
        bool down = inp.Action==Action.GoDown;
        if(!Map.IsLink(type) || Map.GetLink(Position, false).Down != down)
        { App.IO.Print("You can't go {0} here!", down ? "down" : "up");
          return false;
        }
        
        if(type==TileType.UpStairs && CarryStress>CarryStress.Stressed) goto carrytoomuch;

        OnMove(Map.GetLink(Position));

        if(type==TileType.DownStairs)
        { CarryStress stress = CarryStress;
          if(stress>=CarryStress.Stressed || stress==CarryStress.Burdened && Global.Coinflip())
          { App.IO.Print("You fall down the stairs!");
            DoDamage(Death.Falling, Global.NdN((int)stress, 20));
          }
        }
        break;
      }

      case Action.ManageSkills: App.IO.ManageSkills(this); return false;

      case Action.MoveToInteresting: // this needs to be improved
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Point  np = Global.Move(Position, inp.Direction);
        int noise = (10-Stealth)*12; // stealth = 0 to 10
        if(!ManualMove(np)) return false;

        int options=0;
        Direction dir = inp.Direction;
        Direction[] chk = new Direction[5] { dir-2, dir-1, dir, dir+1, dir+2 };
        for(int i=0; i<5; i++)
        { np = Global.Move(Position, chk[i]);
          if(Map.IsPassable(np) || Map.IsDoor(np)) options++;
        }
        while(true)
        { if(ThinkUpdate(ref vis)) return false;
          TileType type = Map[np].Type;
          if(Map.IsLink(np) || Map.HasItems(np)) { DescribeTile(np); return false; }

          int newopts=0;
          for(int i=0; i<5; i++)
          { np = Global.Move(Position, chk[i]);
            if(Map.IsPassable(np) || Map.IsDoor(np)) newopts++;
          }
          if(newopts!=options) return false;

          np = Global.Move(Position, inp.Direction);
          if(Map.IsPassable(np) && !Map.IsDangerous(np))
          { if(Map.IsOverworld) PassTime(WalkTime(np));
            OnMove(np);
            if(noise>0) Map.MakeNoise(np, this, Noise.Walking, (byte)noise);
          }
          else return false;
        }
      }

      case Action.Inventory: App.IO.DisplayInventory(this); return false;

      case Action.NameItem:
      { MenuItem[] items = App.IO.ChooseItem("Name which item?", this, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        string name = App.IO.Ask("Choose a name for this "+items[0].Item.GetFullName(this)+':', true, null);
        items[0].Item.Title = name!="" ? name : null;
        return false;
      }

      case Action.Pickup:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        if(!Map.HasItems(Position)) { App.IO.Print("There are no items here."); return false; }
        if(Inv.IsFull) { App.IO.Print("Your pack is full."); return false; }
        ItemPile inv = Map[Position].Items;

        CarryStress stress=CarryStress, newstress;
        int weight=CarryWeight, max=CarryMax, next;
        if(weight<BurdenedAt*max/100) next = BurdenedAt*max/100;
        else if(weight<StressedAt*max/100) next = StressedAt*max/100;
        else next = OvertaxedAt*max/100;

        // TODO: maybe this shouldn't use a turn if we don't pick anything up (due to answering no to all the
        // confirmations)?
        if(inv.Count==1)
        { if(inv[0].FullWeight+weight<next ||
             App.IO.YesNo("You're having trouble lifting "+inv[0].GetAName(this)+". Continue?", false))
          { Item item = inv[0], newitem = Pickup(inv, 0);
            string s = string.Format("{0} - {1}", newitem.Char, item.GetAName(this));
            if(newitem.Count!=item.Count) s += string.Format(" (now {0})", newitem.Count);
            App.IO.Print(s);
          }
        }
        else
          foreach(MenuItem item in App.IO.Menu(this, inv, MenuFlag.AllowNum|MenuFlag.Multi|MenuFlag.Reletter))
          { if(Inv.IsFull) { App.IO.Print("Your pack is full!"); return false; }
            if(weight+item.Item.Weight*item.Count >= next)
            { if(App.IO.YesNo("You're having trouble lifting "+inv[0].GetAName(this)+". Continue?", false))
                next = int.MaxValue;
              else continue;
            }
            Item newItem = item.Count==item.Item.Count ? Pickup(inv, item.Item) : Pickup(item.Item.Split(item.Count));
            string s = string.Format("{0} - {1}", newItem.Char, item.Item.GetAName(this));
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
        if(!GroundPackUse(typeof(Potion), "Drink", out potion, out inv, ItemClass.Potion)) return false;
        Potion toDrink;
        if(potion.Count>1) toDrink = (Potion)potion.Split(1);
        else { inv.Remove(potion); toDrink=(Potion)potion; }
        Use(toDrink);
        toDrink.Drink(this);
        Map.MakeNoise(Position, this, Noise.Item, potion.GetNoise(this));
        break;
      }

      case Action.Quit:
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false))
        { if(App.IO.YesNo("Do you want to save?", true)) Save();
          else Quit();
        }
        break;

      case Action.Read:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        Item read;
        IInventory inv;
        if(!GroundPackUse(typeof(Readable), "Read", out read, out inv, ItemClass.Scroll, ItemClass.Spellbook))
          return false;
        Scroll scroll = read as Scroll;
        if(scroll != null) // read scroll
        { Exercise(Attr.Int);
          Exercise(scroll.Spell.Skill);
          if(scroll.Count>1) scroll = (Scroll)scroll.Split(1);
          else inv.Remove(scroll);
          OnReadScroll(scroll);
          Use(scroll);
          scroll.Read(this);
          Map.MakeNoise(Position, this, Noise.Item, scroll.GetNoise(this));
        }
        else // read spellbook
        { Spellbook book = (Spellbook)read;
          if(book.Reads==0) { App.IO.Print("This spellbook is too worn to read."); return false; }
          if(book.Spells.Length==0) { App.IO.Print("This spellbook is empty!"); return false; }
          Spell spell = App.IO.ChooseSpell(this, book);
          if(spell==null) goto nevermind;
          int knowledge = SpellKnowledge(spell), chance = Math.Max(knowledge/50, spell.LearnChance(this));
          if(knowledge>0)
          { if(!App.IO.YesNo("You already know this spell. Refresh your memory?", false)) return false;
          }
          else if(chance<25)
          { if(!App.IO.YesNo("This spell seems very difficult. Continue?", false)) return false;
          }
          else if(chance<50 && !App.IO.YesNo("This spell seems difficult. Continue?", false)) return false;

          if(book.Shop!=null && book.Shop.Shopkeeper!=null &&
             book.Shop.Shopkeeper.Say("Hey! This isn't a lending library!"))
          { App.IO.Print("Your concentration is broken.");
            return false;
          }

          Exercise(Attr.Int);
          Exercise(spell.Skill);

          bool success = Global.Rand(100)<chance;
          if(success) // succeeded
          { int turns = spell.Level;
            while(--turns>0 && !ThinkUpdate(ref vis));
            if(turns>0) { App.IO.Print("Your concentration is broken!"); return false; } // don't use up the book
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
            AddEffect(book, Flag.Confused, 100-effect);
            if(effect<30) TeleportSpell.Default.Cast(this);
            if(effect<10) AmnesiaSpell.Default.Cast(this);
            if(effect<5)
            { App.IO.Print("You start to shake uncontrollably, and the world goes dark.");
              AddEffect(book, Flag.Asleep, Global.NdN(4, 15));
            }
          }
        }
        break;
      }

      case Action.Reassign:
      { if(Inv.Count==0) { App.IO.Print("You're not carrying anything!"); return false; }
        MenuItem[] items = App.IO.ChooseItem("Reassign which item?", this, MenuFlag.None, ItemClass.Any);
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
          App.IO.Print("{0} - {1}", other.Char, other.GetFullName(this));
        }
        items[0].Item.Char = c;
        Inv.Add(items[0].Item);
        App.IO.Print("{0} - {1}", c, items[0].Item.GetFullName(this));
        return false;
      }

      case Action.Remove:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        Inventory inv = new Inventory();
        for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) inv.Add(Slots[i]);
        for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null && Hands[i].Class==ItemClass.Shield) inv.Add(Hands[i]);
        if(inv.Count==0) { App.IO.Print("You're not wearing anything!"); return false; }
        MenuItem[] items = App.IO.ChooseItem("Remove what?", this, inv, MenuFlag.Multi, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        foreach(MenuItem i in items)
          if(i.Item.Class==ItemClass.Shield) TryUnequip(i.Item);
          else TryRemove(i.Item);
        break;
      }

      case Action.Save: Save(); break;

      case Action.ShowKnowledge:
        if(Knowledge==null) App.IO.Print("You're still ignorant of everything!");
        else App.IO.DisplayKnowledge(this);
        return false;

      case Action.ShowMap:
      { Point pt = App.IO.DisplayMap(this);
        if(pt.X==-1 || Position==pt || !path.Plan(Memory, Position, pt)) return false;
        PathNode node = path.GetPathFrom(Position);
        bool first = true;
        do
        { Point od = node.Parent.Point;
          if(Map.IsPassable(od))
          { if(Map.IsOverworld)
            { PassTime(WalkTime(od));
              if(first)
              { if(Map.HasItems(Position)) Map[Position].Items.Clear();
                first = false;
              }
            }
            OnMove(od);
            node = node.Parent;
          }
          else if(Map[od].Type==TileType.ClosedDoor)
          { if(Map.GetFlag(od, Tile.Flag.Locked))
            { App.IO.Print("This door is locked.");
              break;
            }
            Map.SetType(od, TileType.OpenDoor); // FIXME: make an attempt to open the door like the opendoor code
            continue;
          }
          else
          { if(!path.Plan(Memory, Position, pt)) break;
            node = path.GetPathFrom(Position);
            if(node.Parent.Point==od) break;
          }
        } while(!ThinkUpdate(ref vis) && Position!=pt);
        DescribeTile(Position);
        return false;
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
          { App.IO.Print(Color.Warning, "No suitable item found to equip."); return false;
          }
        }
        done: break;
      }

      case Action.TalkTo:
      { Direction d = App.IO.ChooseDirection(true, false);
        if(d==Direction.Invalid) goto nevermind;
        if(d==Direction.Self) TalkTo();
        else
        { Entity e = Map.GetEntity(Global.Move(Position, d));
          if(e==null || !CanSee(e)) { App.IO.Print("There's nothing there!"); return false; }
          e.TalkTo();
        }
        break;
      }

      case Action.UseItem:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Inventory inv = new Inventory();
        foreach(Item ii in Inv) if(ii.Usability!=ItemUse.NoUse) inv.Add(ii);
        if(inv.Count==0) { App.IO.Print("You have no useable items."); return false; }
        MenuItem[] items = App.IO.ChooseItem("Use which item?", this, inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        Item i = items[0].Item;
        bool consume;
        if(Map.IsOverworld && i.Usability!=ItemUse.Self) { App.IO.Print("You can't use that here."); return false; }
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
        Map.MakeNoise(Position, this, Noise.Item, i.GetNoise(this));
        if(consume)
        { if(i.Count>1) Use(i.Split(1));
          else { Inv.Remove(i.Char); Use(i); }
        }
        break;
      }

      case Action.ViewItem:
      { MenuItem[] items = App.IO.ChooseItem("Examine which item?", this, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        App.IO.ExamineItem(this, items[0].Item);
        return false;
      }

      case Action.Wear:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Wear what?", this, MenuFlag.None, wearableClasses);
        if(items.Length==0) goto nevermind;
        if(items[0].Item.Class==ItemClass.Shield)
        { Shield shield = (Shield)items[0].Item;
          if(Equipped(shield)) { App.IO.Print("That shield is already equipped!"); return false; }
          TryEquip(shield);
        }
        else
        { Wearable item = items[0].Item as Wearable;
          if(item==null) { App.IO.Print("You can't wear that!"); return false; }
          if(Wearing(item)) { App.IO.Print("You're already wearing that!"); return false; }
          if(item.Slot==Slot.Ring)
          { if(Wearing(Slot.LRing) && Wearing(Slot.RRing))
            { App.IO.Print("You're already wearing two rings!"); return false;
            }
          }
          else if(Wearing(item.Slot) && !TryRemove(item.Slot)) return false;
          Wear(item);
        }
        break;
      }

      case Action.Wield:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Wield what?", this, MenuFlag.AllowNothing, ItemClass.Weapon);
        if(items.Length==0) goto nevermind;
        if(items[0].Item==null) TryEquip(null);
        else
        { Weapon item = items[0].Item as Weapon;
          if(item==null) { App.IO.Print("You can't wield that!"); return false; }
          if(Equipped(item)) { App.IO.Print("You're already wielding that!"); return false; }
          TryEquip(item);
        }
        break;
      }
      
      default: App.IO.Print("You can't do that here."); return false;
    }
    return true;
    
    nevermind: App.IO.Print("Never mind."); return false;
    carrytoomuch: App.IO.Print("You're carrying too much!"); return false;
  }
  #endregion

  protected void DescribeTile(Point pt)
  { if(Map.HasItems(pt)) App.IO.DisplayTileItems(this, Map[pt].Items);
    TileType type = Map[pt].Type;
    if(Map.IsLink(type))
    { Link link = Map.GetLink(pt, false);
      if(type==TileType.UpStairs || type==TileType.DownStairs)
        App.IO.Print("There are stairs leading {0} here.", type==TileType.UpStairs ? "upwards" : "downwards");
      else
      { string name = link.ToSection[link.ToLevel].Name;
        switch(type)
        { case TileType.Town:
            App.IO.Print("There is a road here leading to {0}.", name==null ? "a town" : name);
            break;
          case TileType.Portal:
            App.IO.Print("There is a portal here leading to {0}.", name==null ? "an unknown location" : name);
            break;
          default:
            throw new ApplicationException(string.Format("UNKNOWN LINK TYPE {0} leading to {1}.",
                                                        type, name==null ? "UNKNOWN LOCATION" : name));
        }
      }
    }
  }

  protected virtual void Die(string cause)
  { App.IO.Print("You die.");
    if(Is(Flag.Asleep)) cause += ", while sleeping";
    App.IO.Print("Goodbye {0} the {1}...", Name, Title);
    App.IO.Print("You were killed by: "+cause);
    if(!App.Quit && !App.IO.YesNo("Die?", false))
    { App.IO.Print("Okay, you're alive again.");
      HP = MaxHP;
    }
    else App.Quit = true;
  }

  #region DungeonThink
  protected virtual void DungeonThink()
  { base.Think();

    if(interrupt) { inp.Count=0; interrupt=false; }
    Point[] vis = VisibleTiles();
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
        Weapon w = Weapon;
        Item si=null;
        if(w!=null && IsSharp(w)) si=w;
        else
        { App.IO.Print("You need a sharp item to carve with.");
          foreach(Item i in Inv)
            if(i.Class==ItemClass.Weapon && (i.KnownUncursed || i.KnownBlessed) && IsSharp((Weapon)i))
            { if(w!=null && !TryUnequip(w))
              { App.IO.Print("You can't equip a suitable item!");
                goto next;
              }
              si = i;
              Equip((Weapon)i);
              break;
            }
          if(si==null) { App.IO.Print("You can't find a suitable (known uncursed) sharp item to use!"); goto next; }
        }

        Item item;
        IInventory inv;
        if(!GroundPackUse(typeof(Corpse), "Butcher", out item, out inv, ItemClass.Corpse)) goto next;
        Corpse corpse = (Corpse)item;
        if((corpse.Flags&Corpse.Flag.Rotting)!=0) App.IO.Print("Eww, disgusting!");
        else if((corpse.Flags&Corpse.Flag.Skeleton)!=0)
        { App.IO.Print("You can't find any usable meat on this skeleton."); goto next;
        }

        int turn=++corpse.CarveTurns, turns=(corpse.Weight+99)/100;
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
          if(turns>1) App.IO.Print("You finish carving the {0}.", corpse.Name);
          if(inv.Add(food)==null)
          { App.IO.Print("The {0} does not fit in your pack, so you put it down.", food);
            Map.AddItem(Position, food);
          }
        }
        if(si!=w)
        { if(TryUnequip(si))
          { if(w!=null) Equip(w);
          }
          else App.IO.Print("You can't unequip your {0}!", si.GetFullName(this));
        }
        break;
      }

      case Action.CastSpell:
      { if(Spells==null || Spells.Count==0) { App.IO.Print("You don't know any spells!"); goto next; }
        if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Spell spell = App.IO.ChooseSpell(this);
        if(spell==null) goto nevermind;
        if(MP<spell.Power) { App.IO.Print("You don't have enough power to cast this spell!"); goto next; }
        MP -= spell.Power;
        Exercise(Attr.Int);
        Exercise(Skill.Casting);
        Exercise(spell.Skill);
        if(spell.Memory<500) App.IO.Print("Your memory of this spell is very faint.");
        else if(spell.Memory<1000) App.IO.Print("Your memory of this spell is faint.");
        if(spell.CastTest(this))
        { switch(spell.GetSpellTarget(this))
          { case SpellTarget.Self: spell.Cast(this); break;
            case SpellTarget.Item:
              MenuItem[] items = App.IO.ChooseItem("Cast on which item?", this, MenuFlag.None, ItemClass.Any);
              if(items.Length==0) App.IO.Print("The energy rises within you, and then fades.");
              else spell.Cast(this, items[0].Item);
              break;
            case SpellTarget.Tile:
              RangeTarget rt = App.IO.ChooseTarget(this, spell, true);
              if(rt.Dir!=Direction.Invalid || rt.Point.X!=-1) spell.Cast(this, rt);
              else App.IO.Print("The energy rises within you, and then fades.");
              break;
          }
          spell.Memory += 3;
        }
        else
        { App.IO.Print("Your spell fizzles.");
          spell.Memory++;
        }
        break;
      }

      case Action.CloseDoor:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Direction dir = GetDirection(TileType.OpenDoor);
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

      case Action.Fire:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Weapon w = Weapon;
        if(w!=null && w.Ranged)
        { Ammo ammo;
          if(w.wClass==WeaponClass.Thrown) ammo=null;
          else
          { ammo = SelectAmmo(w);
            if(ammo==null) { App.IO.Print("You have no suitable ammunition!"); goto next; }
            App.IO.Print("Using {0} - {1}.", ammo.Char, ammo.GetAName(this));
          }
          RangeTarget rt = App.IO.ChooseTarget(this, true);
          if(rt.Dir!=Direction.Invalid) Attack(w, ammo, rt.Dir);
          else if(rt.Point.X!=-1) Attack(w, ammo, rt.Point);
          else goto nevermind;
        }
        else
        { Direction d = App.IO.ChooseDirection(false, false);
          if(d==Direction.Invalid) goto nevermind;
          Attack(w, null, d);
        }
        break;
      }

      case Action.Invoke:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Inventory inv = new Inventory();
        for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) inv.Add(Hands[i]);
        if(inv.Count==0) { App.IO.Print("You have no items equipped!"); goto next; }
        if(inv.Count==1) { Invoke(inv[0]); break; }
        MenuItem[] items = App.IO.ChooseItem("Invoke which item?", this, inv, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        Invoke(items[0].Item);
        break;
      }

      case Action.Move:
      { Point np = Global.Move(Position, inp.Direction);
        Entity c = Map.GetEntity(np);
        if(c!=null)
        { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
          if(c is AI)
          { if(CanSee(c))
            { if(!((AI)c).HostileTowards(this) &&
                 !App.IO.YesNo(string.Format("Are you sure you want to attack {0}?", c.theName), false)) goto next;
            }
            else if(!App.IO.YesNo("There is something here. Attack it?", false)) goto next;
          }
          Weapon w = Weapon;
          Ammo   a = SelectAmmo(w);
          if(w!=null && a==null && w.Ranged && w.wClass!=WeaponClass.Thrown)
            App.IO.Print(Color.Warning, "You're out of "+((FiringWeapon)w).AmmoName+'!');
          else if(a!=null) App.IO.Print("Using {0} - {1}.", a.Char, a.GetAName(this));
          Attack(w, a, np);
        }
        else if(ManualMove(np))
        { if(count<=1) DescribeTile(np);
          inp.Count = count;
        }
        else goto next;
        break;
      }

      case Action.OpenDoor:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        Direction dir = GetDirection(TileType.ClosedDoor);
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

      case Action.Rest:
        if(count>1)
        { bool fullHP=(HP==MaxHP), fullMP=(MP==MaxMP);
          while(--count>0)
            if(ThinkUpdate(ref vis) || !fullHP && HP==MaxHP || !fullMP && MP==MaxMP)
              goto next;
        }
        break;

      case Action.Throw:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Throw which item?", this, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) goto nevermind;
        if(Wearing(items[0].Item)) { App.IO.Print("You can't throw something you're wearing!"); goto next; }
        RangeTarget rt = App.IO.ChooseTarget(this, true);
        if(rt.Dir!=Direction.Invalid) ThrowItem(items[0].Item, rt.Dir);
        else if(rt.Point.X!=-1) ThrowItem(items[0].Item, rt.Point);
        else goto nevermind;
        break;
      }

      case Action.ZapWand:
      { if(CarryStress==CarryStress.Overtaxed) goto carrytoomuch;
        MenuItem[] items = App.IO.ChooseItem("Zap what?", this, MenuFlag.None, ItemClass.Wand);
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
        Map.MakeNoise(Position, this, Noise.Zap, wand.GetNoise(this));
        if(destroy) Inv.Remove(wand);
        Use(wand);
        break;
      }
      
      default:
        if(DefaultHandler(ref vis)) break;
        else goto next;
    }

    OnAge();
  }
  #endregion

  protected Direction GetDirection(TileType type)
  { Point pt = new Point(-1, -1);
    int dir=0;
    for(int d=0; d<8; d++)
      if(Map[Global.Move(Position, d)].Type==type)
      { if(pt.X!=-1) { pt.X=-1; break; }
        pt = Global.Move(Position, d);
        dir=d;
      }
    if(pt.X!=-1) return (Direction)dir;
    return App.IO.ChooseDirection(false, false);
  }

  protected bool GroundPackUse(Type type, string verb, out Item item, out IInventory inv, params ItemClass[] classes)
  { item = null; inv = null;
    if(Map.HasItems(Position))
    { Item[] items = Map[Position].Items.GetItems(classes);
      foreach(Item i in items)
      { char c = App.IO.CharChoice(string.Format("There {0} {1} here. {2} {3}?",
                                                 i.AreIs, i.GetFullName(this), verb, i.ItOne), "ynq", 'q', true);
        if(c=='y') { item=i; inv=Map[Position].Items; break; }
        else if(c=='q') { App.IO.Print("Never mind."); return false; }
      }
    }
    if(item==null)
    { if(!Inv.Has(classes)) { App.IO.Print("You have nothing to {0}!", verb.ToLower()); return false; }
      MenuItem[] items = App.IO.ChooseItem(verb+" what?", this, MenuFlag.None, classes);
      if(items.Length==0) { App.IO.Print("Never mind."); return false; }
      if(!type.IsInstanceOfType(items[0].Item)) { App.IO.Print("You can't {0} that!", verb.ToLower()); return false; }
      item = items[0].Item;
      inv = Inv;
    }
    return true;
  }
  
  protected bool IsSharp(Weapon w)
  { return w.wClass==WeaponClass.Axe || w.wClass==WeaponClass.Dagger || w.wClass==WeaponClass.LongBlade ||
           w.wClass==WeaponClass.ShortBlade;
  }

  protected virtual void OnAge()
  { if(Spells!=null)
      for(int i=0; i<Spells.Count; i++)
      { Spell spell = (Spell)Spells[i];
        if(spell.Memory!=-1 && --spell.Memory==0)
        { Spells.RemoveAt(i--);
          App.IO.Print("You feel as if you've forgotten something... but decide it's nothing.");
        }
      }

    Hunger++;
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

  protected virtual void OverworldThink()
  { base.Think();

    if(interrupt) { inp.Count=0; interrupt=false; }
    Point[] vis = VisibleTiles();

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
    { case Action.Move:
      { Point np = Global.Move(Position, inp.Direction);
        if(ManualMove(np))
        { if(count<=1) DescribeTile(np);
          inp.Count = count;
          break;
        }
        else goto next;
      }

      case Action.Rest:
        if(count>1)
        { bool fullHP=(HP==MaxHP), fullMP=(MP==MaxMP);
          while(--count>0)
            if(ThinkUpdate(ref vis) || !fullHP && HP==MaxHP || !fullMP && MP==MaxMP)
              goto next;
        }
        break;

      default:
        if(DefaultHandler(ref vis)) break;
        else goto next;
    }

    OnAge();
  }

  protected bool ManualMove(Point pt)
  { if(CarryStress==CarryStress.Overtaxed) { App.IO.Print("You're carrying too much!"); return false; }
    TileType type = Map[pt].Type;
    if(Map.IsPassable(type))
    { if(Map.IsDangerous(type) && Map[Position].Type!=type &&
         !App.IO.YesNo(string.Format("Are you sure you want to move into {0}?", type.ToString().ToLower()), false))
        return false;
      if(Map.IsOverworld)
      { if(Map.HasItems(Position)) Map[Position].Items.Clear();
        PassTime(WalkTime(pt));
      }
      OnMove(pt);
      int noise = (10-Stealth)*12; // stealth = 0 to 10
      if(noise>0) Map.MakeNoise(pt, this, Noise.Walking, (byte)noise);
      return true;
    }
    else if(type==TileType.Border && Map.IsTown && App.IO.YesNo("Leave the town?", false))
    { OnMove(Map.GetLink(0));
      return true;
    }
    else return false;
  }

  protected void PassTime(int turns) { while(turns-->0 && HP>0) { base.Think(); OnAge(); } }

  protected bool ThinkUpdate(ref Point[] vis)
  { Map.Simulate(this); // this will return when it's our turn again
    base.Think();
    vis = VisibleTiles();
    UpdateMemory(vis);
    if(IsEnemyVisible(vis)) interrupt=true;
    OnAge();
    if(interrupt) { interrupt=false; return true; }
    return false;
  }

  protected int WalkTime(Point pt)
  { TileType type = Map[pt].Type;
    int time;
    if(type==TileType.Grass || Map.IsLink(type)) time=75;
    else if(type==TileType.Forest) time=90;
    else if(type==TileType.Ice || type==TileType.DirtSand) time=80;
    else if(type==TileType.Hill) time=100;
    else if(type==TileType.Mountain) time=200; // TODO: decrease with proper equipment
    else if(type==TileType.ShallowWater) time=100; // TODO: decrease with proper equipment
    else if(type==TileType.DeepWater) time=200; // TODO: decrease with proper equipment
    else throw new ApplicationException("Unhandled tile type: "+type);
    
    int speed = Speed; // fast characters get a bonus
    if(speed<10) time/=3;
    else if(speed<20) time/=2;
    else if(speed<30) time = time*3/4;

    return time;
  }

  protected static readonly ItemClass[] wearableClasses =  new ItemClass[]
  { ItemClass.Amulet, ItemClass.Armor, ItemClass.Ring, ItemClass.Shield
  };

  [NonSerialized] Input inp;
  HungerLevel oldHungerLevel;
  [NonSerialized] PathFinder path = new PathFinder();
}

} // namespace Chrono