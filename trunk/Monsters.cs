using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chrono
{

#region Orc
[Serializable]
public class Orc : AI
{ public Orc() { Race=Race.Orc; Color=Color.Yellow; baseKillExp=10; canOpenDoors=true; }
  public Orc(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);

    SetSkill(Skill.Fighting, 1);

    if(Global.Rand(100)<10)
    { SetSkill(Skill.Bow, 1);
      AddItem(new Bow());
      AddItem(Global.Coinflip() ? (Arrow)new BasicArrow() : (Arrow)new FlamingArrow()).Count = Global.Rand(5) + 5;
    }
    else if(Global.Rand(100)<10) { AddItem(new ShortSword()); SetSkill(Skill.ShortBlade, 1); }
    else if(Global.Rand(100)<10) { AddItem(new PoisonDart()).Count = 5; SetSkill(Skill.Throwing, 1); }
    else SetSkill(Skill.UnarmedCombat, 1);
    if(Global.Rand(100)<10) AddItem(new PaperBag());
    if(Global.Rand(100)<10) AddItem(new TeleportScroll());
    if(Global.Rand(100)<10) AddItem(new HealPotion());
  }
}
#endregion

#region Townsperson
[Serializable]
public class Townsperson : AI
{ public Townsperson()
  { switch(Global.Rand(4))
    { case 0: case 1: case 2: Race=Race.Human; break;
      case 3: Race=Race.Orc; break;
    }
    IsAdult = Global.Coinflip();
    GotoState(defaultState = Global.OneIn(4) ? AIState.Wandering : AIState.Idle);

    if(IsAdult)
    { baseName    = jobs[Global.Rand(jobs.Length)];
      baseKillExp = 3;
    }
    else baseKillExp = 1;

    alwaysHostile=false;
    canOpenDoors =true;
  }
  public Townsperson(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);

    if(IsAdult)
    { SetSkill(Skill.Fighting, 1);
      SetSkill(Skill.UnarmedCombat, 1);
      
      Male = baseName!="houseWife" && baseName!="prostitute";

      switch(baseName)
      { case "hunter":
          SetSkill(Skill.Fighting, 2);
          SetSkill(Skill.Bow, 3);
          SetSkill(Skill.Dagger, 1);
          AlterBaseAttr(Attr.Str, 1);
          AlterBaseAttr(Attr.Dex, 2);
          AddItem(new Bow());
          AddItem(new BasicArrow()).Count = 10;
          // TODO: give dagger and leather armor
          break;
        case "blacksmith":
          SetSkill(Skill.ShortBlade, 1);
          SetSkill(Skill.LongBlade, 1);
          AlterBaseAttr(Attr.Str, 3);
          // TODO: give long blade
          // TODO: give armor
          break;
        case "tailor": case "tinkerer":
          SetSkill(Skill.Dagger, 1);
          AlterBaseAttr(Attr.Dex, 2);
          // TODO: give weapon
          break;
        case "cleric": case "priest":
          SetSkill(Skill.Casting, 3);
          SetSkill(Skill.Channeling, 3);
          AlterBaseAttr(Attr.Int, 4);
          MP = AlterBaseAttr(Attr.MaxMP, 8);

          // TODO: give spells
          SetSkill(Skill.Telekinesis, 3); // TODO: remove this
          MemorizeSpell(ForceBolt.Default, -1); // TODO: and this

          if(baseName=="cleric")
          { SetSkill(Skill.Fighting, 2);
            SetSkill(Skill.MaceFlail, 1);
            AlterBaseAttr(Attr.Str, 1);
            AlterBaseAttr(Attr.Int, 2);
            MP = AlterBaseAttr(Attr.MaxMP, 8);
            // TODO: give weapon
          }
          break;
        case "carpenter": case "farmer": AlterBaseAttr(Attr.Str, 2); break;
        case "hobo": case "prostitute": case "housewife":
          AlterBaseAttr(Attr.Str, -1);
          if(baseName!="hobo" && Global.Coinflip())
          { // TODO: give knife
          }
          break;
        case "shepherd":
          SetSkill(Skill.Staff, 2);
          // TODO: give weapon
          break;
      }
    }
    else
    { Male = Global.Coinflip();
      baseName = Race.ToString().ToLower() + (Male ? " boy" : " girl");
      AlterBaseAttr(Attr.Str, -1);    // children are weak,
      AlterBaseAttr(Attr.Dex, -1);    // clumsy,
      HP = SetBaseAttr(Attr.MaxHP, GetBaseAttr(Attr.MaxHP)/2);  // frail,
      AlterBaseAttr(Attr.Speed, 10);  // and slow
      if(Male)
      { SetSkill(Skill.Dagger, 1);
        // TODO: give him a knife
      }
    }
  }

  public override void OnHitBy(Entity attacker, object item, Damage damage)
  { if(SocialGroup!=-1 && attacker.SocialGroup==App.Player.SocialGroup && !Global.GetSocialGroup(SocialGroup).Hostile)
    { Map.MakeNoise(attacker.Position, attacker, Noise.Alert, Map.MaxSound);
      App.IO.Print("You hear an alarm bell ring!");
      Global.UpdateSocialGroup(SocialGroup, true);
    }
    base.OnHitBy(attacker, item, damage);
  }

  public override void TalkTo()
  { if(HostileTowards(App.Player)) Say(GetQuip(IsAdult ? Quips.Attacking : Quips.KidAttacking));
    else if(!IsAdult) Say(GetQuip(Quips.Kid));
    else switch(baseName)
    { case "hunter": Say(GetQuip(Quips.Fighter)); break;
      case "blacksmith": case "tailor": case "tinkerer": case "carpenter": case "shepherd": case "clerk":
        Say(GetQuip(Quips.Worker));
        break;
      case "farmer": Say(GetQuip(Quips.Farmer)); break;
      case "cleric": case "priest": Say(GetQuip(Quips.Holyman)); break;
      case "hobo": Say(GetQuip(Quips.Hobo)); break;
      case "housewife": Say(GetQuip(Quips.Housewife)); break;
      case "prostitute": Say(GetQuip(Quips.Prostitute)); break;
      default: throw new NotImplementedException("Unhandled job: "+baseName);
    }
  }

  protected bool IsAdult
  { get { return Color==Color.Cyan; }
    set { Color = value ? Color.Cyan : Color.LightCyan; } // light cyan is a child, cyan is an adult
  }

  static readonly string[] jobs = new string[]
  { // female jobs
    "housewife", "housewife", "housewife", "prostitute", // housewives get selected more often
    // male jobs
    "farmer", "blacksmith", "tanner", "tinkerer", "carpenter", "hunter", "shepherd",
    // either sex
    "clerk", "hobo", "tailor", "cleric", "priest",
  };
}
#endregion

#region Shopkeeper
[Serializable]
public class Shopkeeper : AI
{ public Shopkeeper()
  { Race     = Race.Human; // TODO: not always human...
    baseName = "shopkeeper";
    alwaysHostile = false;
    canOpenDoors  = true;
    baseKillExp   = 50;
  }
  public Shopkeeper(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public int Credit { get { return credit; } }

  public Shop Shop
  { get
    { foreach(Room r in Map.Rooms)
      { Shop s = r as Shop;
        if(s!=null && s.Shopkeeper==this) return s;
      }
      return null;
    }
  }

  public int BuyCost(Item item)
  { if(item.Class==ItemClass.Gold) return item.Count;
    if(!Shop.Accepts(item)) return 0;
    int baseCost = (int)item.GetType().GetField("ShopValue", BindingFlags.Static|BindingFlags.Public).GetValue(item);
    return (int)Math.Round(baseCost*item.Count / (priceMod*2));
  }

  public void ClearUnpaidItems(Inventory inv) { ClearUnpaidItems(Shop, inv); }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);
    Male = Global.Rand(100)<80; // 80% chance of being male
    priceMod = Global.Rand(85, 115) / 100.0;

    // shopkeepers are tough
    SetSkill(Skill.Armor, 5);
    SetSkill(Skill.Casting, 5);
    SetSkill(Skill.Dodge, 2);
    SetSkill(Skill.Elemental, 4);
    SetSkill(Skill.Fighting, 5);
    SetSkill(Skill.MagicResistance, 3);
    SetSkill(Skill.Telekinesis, 4);
    SetSkill(Skill.UnarmedCombat, 3);
    
    AlterBaseAttr(Attr.AC, 3);
    AlterBaseAttr(Attr.Dex, 3);
    AlterBaseAttr(Attr.EV, 2);
    AlterBaseAttr(Attr.Int, 3);
    AlterBaseAttr(Attr.Speed, -10);
    AlterBaseAttr(Attr.Str, 3);

    SetRawFlag(Flag.SeeInvisible, true);
    SetRawFlag(Flag.TeleportControl, true);

    // TODO: give better armor
    AddItem(new Buckler());
    AddItem(new PaperBag());

    AddItem(new HealPotion()).Count += 2;

    switch(Global.Rand(4))
    { case 0:
        SetSkill(Skill.Axe, 5);
        // TODO: give an axe
        break;
      case 1:
        SetSkill(Skill.Crossbow, 5);
        // TODO: give a crossbow
        break;
      case 2:
        SetSkill(Skill.ShortBlade, 5);
        AddItem(new ShortSword());
        break;
      case 3:
        SetSkill(Skill.MaceFlail, 5);
        // TODO: give a mace
        break;
    }
    
    AddItem(new Gold()).Count += Global.Rand(1500, 4000);
  }

  public void GiveCredit(int amount) { credit += amount; }

  public void GreetPlayer(Entity player)
  { if(state!=AIState.Attacking)
    { if(HostileTowards(player))
      { Say("How dare you show your face around here again?");
        Attack(player);
      }
      else if(state==AIState.Working) Say("Hello! Welcome to my shop.");
    }
  }

  public override void OnHitBy(Entity attacker, object item, Damage damage)
  { if(SocialGroup!=-1 && Map.IsTown && attacker.SocialGroup==App.Player.SocialGroup &&
       !Global.GetSocialGroup(SocialGroup).Hostile)
    { Map.MakeNoise(attacker.Position, attacker, Noise.Alert, Map.MaxSound);
      App.IO.Print("You hear an alarm bell ring!");
      Global.UpdateSocialGroup(SocialGroup, true);
    }
    base.OnHitBy(attacker, item, damage);
  }

  public override void OnAttackedBy(Entity attacker)
  { if(state!=AIState.Attacking && attacker==App.Player)
    { Say("What the hell do you think you're doing?");
      Attack(attacker);
    }
    base.OnAttackedBy(attacker);
  }

  public void PlayerDamagedShop(Entity player)
  { if(!alwaysHostile)
    { Say("How dare you defile my shop!");
      alwaysHostile = true;
      Attack(player);
    }
  }

  public void PlayerLeft(Entity player)
  { int bill = GetPlayerBill(player);
    if(bill>0)
    { if(credit>=bill)
      { Say("I'm deducting {0} from your credit for unpaid items.", bill);
        ClearUnpaidItems(Shop, player.Inv);
        credit -= bill;
      }
      else if(!alwaysHostile)
      { App.IO.Print("You stole from the shop! You'd better not let the shopkeeper catch you!");
        priceMod += 0.10;
        alwaysHostile = true;
      }
    }
  }

  public int GetPlayerBill(Entity player) { return GetPlayerBill(player, true); }
  public int GetPlayerBill(Entity player, bool includeItems)
  { return (includeItems ? Math.Max(-credit, 0) : 0) + ContainsMyItems(Shop, player.Inv);
  }

  public void PlayerTook(Item item)
  { if(item.Class!=ItemClass.Gold)
      Say("{0} cost{1} {2} gold.", Global.Cap1(item.GetThatName(App.Player)), item.VerbS, SellCost(item));
  }

  public void PlayerUsed(Item item, bool standardMessage)
  { int cost = SellCost(item);
    item.Shop = null;
    if(cost==0) return;

    int take = Math.Min(Math.Max(credit, 0), cost);
    credit -= cost;
    if(HostileTowards(App.Player)) return;
    cost -= take;

    string yell = "Hey";
    if(!standardMessage)
    { if(item.Class==ItemClass.Food) yell = "You bit it, you bought it";
    }

    if(cost>0)
      Say("{0}! {1}ou owe me {2} gold for {3}!", yell,
          (take>0 ? "I'm deducting "+take+" from your credit and y" : "Y"), cost, item.GetThatName(App.Player));
    else Say("I'm deducting {0} from your credit.", take);
  }

  public int SellCost(Item item)
  { if(item.Class==ItemClass.Gold) return item.Count;
    int baseCost = (int)item.GetType().GetField("ShopValue", BindingFlags.Static|BindingFlags.Public).GetValue(item);
    return (int)Math.Round(baseCost*item.Count * priceMod);
  }

  public override void TalkTo()
  { if(HostileTowards(App.Player))
    { Say(GetQuip(Quips.Attacking)); // TODO: allow player to pay the shopkeeper to calm him down
    }
    else
    { System.Collections.ArrayList list = new System.Collections.ArrayList();
      int debt = -Math.Min(credit, 0), itemcost = ContainsMyItems(Shop, App.Player.Inv, list);
      if(debt==0 && itemcost==0) { Say(GetQuip(Quips.Shopkeeper)); return; }

      bool paidSome = false;
      if(debt>0)
      { Say("First, there's the matter of your debt. You owe me {0} gold.", debt);
        if(App.IO.YesNo("Pay your debt?", true))
        { Gold gold = App.Player.GetGold(debt, false);
          if(gold==null)
          { Say("It seems you don't have enough gold. You'd better get some, and quickly.");
            App.IO.Print("[The shopkeeper eyes your equipment.]");
            return;
          }
          else
          { Inv.Add(gold);
            credit=0;
            Say("Okay, that's taken care of.");
            paidSome = true;
          }
        }
      }
      
      int playerGold = App.Player.Gold;
      bool freeToGo  = true;

      foreach(Item item in list)
      { int cost = SellCost(item);
        if(cost>playerGold)
        { App.IO.Print("You owe {0} gold for {1}, but you can't afford it.", cost, item.GetAName(App.Player));
          freeToGo = false;
        }
        else if(App.IO.YesNo(string.Format("You owe {0} gold for {1}. Pay?", cost, item.GetAName(App.Player)), true))
        { Inv.Add(App.Player.GetGold(cost, false));
          playerGold -= cost;
          item.Shop   = null;
          paidSome    = true;
        }
        else freeToGo = false;
      }
      
      if(paidSome) Say(freeToGo ? "Thanks. That covers everything." : "Thanks. You still owe me some money, though.");
    }
  }

  protected int ContainsMyItems(Shop shop, IInventory inv) { return ContainsMyItems(shop, inv, null); }
  protected int ContainsMyItems(Shop shop, IInventory inv, System.Collections.IList addTo)
  { int total=0;
    foreach(Item i in inv)
    { if(i.Shop==shop)
      { if(addTo!=null) addTo.Add(i);
        total += SellCost(i);
      }
      IInventory ni = i as IInventory;
      if(ni!=null) total += ContainsMyItems(shop, ni);
    }
    return total;
  }

  protected override void Die()
  { Shop shop = Shop;
    ClearUnpaidItems(shop, App.Player.Inv);

    Rectangle area = shop.OuterArea;
    for(int y=area.Y; y<area.Bottom; y++)
      for(int x=area.X; x<area.Right; x++)
      { Tile t = Map[x, y];
        if(t.Items!=null)
          foreach(Item i in t.Items) if(i.Shop==shop) i.Shop=null;
      }

    shop.Shopkeeper = null;
    base.Die();
  }

  protected override bool HandleState(AIState state)
  { // raise prices if the player is making us angry
    if(state==AIState.Attacking && target==App.Player) priceMod += 0.03;

    if(state==AIState.Working)
    { if(App.Player.Map==Map) // move around to get out of the player's way
      { int dist = DistanceTo(App.Player.Position);
        Direction d;
        if(dist<=1) d = Global.Coinflip() ? App.Player.LookAt(Position) : Direction.Self; // move half of the time
        else d = LookAt(Shop.FrontOfDoor);

        if(d!=Direction.Self)
        { Rectangle area = Shop.InnerArea;
          for(int i=0; i<4; i++)
          { Point pt = Global.Move(Position, (int)d+i);
            if(area.Contains(pt) && TryMove(pt)) return true;
            if(i!=0)
            { pt = Global.Move(Position, (int)d-i);
              if(area.Contains(pt) && TryMove(pt)) return true;
            }
          }
        }
      }
      return DoIdleStuff();
    }
    else if(state==defaultState)
    { if(!Shop.InnerArea.Contains(Position))
      { if(!TryMove(Shop.FrontOfDoor)) TeleportSpell.Default.Cast(this, Map.FreeSpace(Shop.InnerArea));
      }
      GotoState(AIState.Working);
      return true;
    }
    else return base.HandleState(state);
  }

  protected int credit;
  protected double priceMod;

  protected static void ClearUnpaidItems(Shop shop, IInventory inv)
  { foreach(Item i in inv)
    { if(i.Shop==shop) i.Shop=null;
      IInventory ni = i as IInventory;
      if(ni!=null) ClearUnpaidItems(shop, ni);
    }
  }
}
#endregion

} // namespace Chrono
