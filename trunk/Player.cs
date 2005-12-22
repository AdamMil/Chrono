using System;
using System.Collections;
using Point=System.Drawing.Point;

namespace Chrono
{

public sealed class Player : Entity
{ public Player(string name) : base(name) { }
  public Player(int entityIndex) : base(entityIndex) { }

  public string Title
  { get
    { return "Newb"; // TODO: implement this
    }
  }

  public void AddKnowledge(ItemClass itemClass)
  { if(!KnowsAbout(itemClass))
    { Knowledge.Add(itemClass.Index);
      Knowledge.Sort();
    }
  }

  public bool KnowsAbout(ItemClass itemClass) { return Knowledge.BinarySearch(itemClass.Index)>=0; }

  public override void Abuse(Attr attr)
  { if(attr!=Attr.Str && attr!=Attr.Int && attr!=Attr.Dex) throw new NotImplementedException();

    int aval = GetBaseAttr(attr);
    if(aval<=3) return; // 3 is the minimum

    if(Global.OneIn(3))
    { attrExp[(int)attr] -= 10;
      if(attrExp[(int)attr]<0)
      { attrExp[(int)attr] = 125;
        AlterBaseAttr(attr, -1);
        OnAttrChange(attr, -1, true);
      }
    }
  }

  public override void Exercise(Attr attr)
  { if(attr!=Attr.Str && attr!=Attr.Int && attr!=Attr.Dex) throw new NotImplementedException();
    if(ExpPool==0) return;

    int aval = GetBaseAttr(attr);
    if(aval>17) // over 17, it gets harder to increase attributes through exercise
    { aval -= 13; // TODO: make this exponential so it reaches 100% much faster
      if(Global.Rand(100) < aval*10) return; // returns: 18:50%, 19:60%, 20:70%, 21:80%, 22:90%, 23+:100%
    }

    if(Global.OneIn(3)) // if it passes the above test, there's a 33% chance of exercise
    { int points = Math.Min(Global.Rand(6), ExpPool);
      if(points==0) return;

      ExpPool -= points;
      attrExp[(int)attr] += points;
      if(attrExp[(int)attr] >= 250)
      { attrExp[(int)attr] -= 250;
        AlterBaseAttr(attr, 1);
        OnAttrChange(attr, 1, true);
      }
    }
  }

  public override void Exercise(Skill skill)
  { if(ExpPool==0 || !Training(skill) || GetSkill(skill)==10) return; // the maximum skill level is 10
    int points = Math.Min(Global.Rand(11), ExpPool);
    if(Global.OneIn(3))
    { int need = NextSkillLevel(skill);
      ExpPool -= points;
      skillExp[(int)skill] += points;
      if(skillExp[(int)skill]>=need)
      { skillExp[(int)skill] -= need;
        skills[(int)skill]++;
        OnSkillUp(skill);
      }
    }
  }

  public override int GetSkill(Skill skill) { return skills[(int)skill]; }
  
  public void Interrupt() { interrupt = true; }

  public void OnSkillUp(Skill skill)
  { App.IO.Print(Color.Green, "Your {0} skill went up!", skill.ToString().ToLower());
  }

  public override void Think()
  { base.Think();

    next: inp = App.IO.GetNextInput();

    switch(inp.Action)
    { 
      // TODO: case Action.CastSpell:

      #region CloseDoor
      case Action.CloseDoor:
      { if(CarryStress>=CarryStress.Strained) goto carrytoomuch;
        Direction dir = FindAdjacent(TileType.OpenDoor);
        if(dir!=Direction.Invalid)
        { Point  pt = Global.Move(Pos, dir);
          Tile tile = Map[pt];
          if(tile.Type==TileType.ClosedDoor) App.IO.Print("That door is already closed.");
          else if(tile.Type!=TileType.OpenDoor) App.IO.Print("There is no door there.");
          else if(Map.HasItems(pt) || Map.GetEntity(pt)!=null) App.IO.Print("The door is blocked.");
          else
          { Map.SetType(pt, TileType.ClosedDoor);
            Map.MakeNoise(pt, this, Noise.Bang, NoiseLevel(120));
            break;
          }
        }
        goto next;
      }
      #endregion

      #region Drop
      case Action.Drop: case Action.DropType:
      { if(Inv==null || Inv.Count==0) { App.IO.Print("You're not carrying anything."); goto next; }
        CarryStress stress = CarryStress, newstress;
        MenuItem[] items;
        if(inp.Action==Action.Drop)
          items = App.IO.ChooseItem("Drop what?", (Inventory)Inv, MenuFlag.AllowNum|MenuFlag.Multi, ItemType.Any);
        else
        { ArrayList list = new ArrayList();
          list.Add(new MenuItem("All types", 'a'));
          char c = 'b';
          for(int i=0; i<(int)ItemType.NumTypes; c++,i++)
            if(Inv.Has((ItemType)i)) { list.Add(new MenuItem(((ItemType)i).ToString(), c)); break; }
          list.Add(new MenuItem("Auto-select every item", 'A'));
          
          { bool knownBlessed=false, knownCursed=false, knownUncursed=false, unpaid=false, unknown=false;
            foreach(Item item in Inv)
            { if(item.Shop!=null) unpaid = true;
              if(!item.KnowCB) unknown = true;
              else if(item.Blessed) knownBlessed = true;
              else if(item.Cursed) knownCursed = true;
              else knownUncursed = true;
            }
          
            if(knownBlessed) list.Add(new MenuItem("Items known to be blessed", 'B'));
            if(knownCursed) list.Add(new MenuItem("Items known to be cursed", 'C'));
            if(unpaid) list.Add(new MenuItem("Unpaid items", 'P'));
            if(knownUncursed) list.Add(new MenuItem("Items known to be uncursed", 'U'));
            if(unknown) list.Add(new MenuItem("Items of unknown B/C/U status", 'X'));
          }

          items = App.IO.Menu((MenuItem[])list.ToArray(typeof(MenuItem)), MenuFlag.Multi);
          if(items.Length==0) goto nevermind;
          
          bool autoSelect = false;
          foreach(MenuItem mi in items) if(mi.Char=='A') { autoSelect = true; break; }

          if(autoSelect && items.Length==1 && items[0].Char=='A')
          { DropItemsIn(Inv);
            goto doneDropping;
          }

          Inventory inv = new Inventory(); // create a temporary inventory to hold the items selected by the type menu

          list.Clear();
          foreach(MenuItem item in items)
            if(char.IsLower(item.Char)) // lowercase letters indicate 
              if(item.Char!='a') list.Add((ItemType)(item.Char-'a'-1)); // convert letters back to item classes
              else // otherwise, "all types" was selected, so we add every item and go directly to the menu
              { foreach(Item i in Inv) inv.Add(i);
                goto menu;
              }

          if(list.Count>0) // if some item classes were selected, add those items to 'inv'
            foreach(Item i in Inv.GetItems((ItemType[])list.ToArray(typeof(ItemType)))) inv.Add(i);

          // now do the meta-types, making sure not to add them twice
          foreach(MenuItem item in items)
            switch(item.Char)
            { case 'B': foreach(Item i in Inv) if(i.KnownBlessed  && !inv.Contains(i)) inv.Add(i); break;
              case 'C': foreach(Item i in Inv) if(i.KnownCursed   && !inv.Contains(i)) inv.Add(i); break;
              case 'P': foreach(Item i in Inv) if(i.Shop!=null    && !inv.Contains(i)) inv.Add(i); break;
              case 'U': foreach(Item i in Inv) if(i.KnownUncursed && !inv.Contains(i)) inv.Add(i); break;
              case 'X': foreach(Item i in Inv) if(!i.KnowCB       && !inv.Contains(i)) inv.Add(i); break;
            }

          if(autoSelect) { DropItemsIn(inv); goto doneDropping; }
          menu: items = App.IO.Menu(inv, MenuFlag.Multi|MenuFlag.AllowNum);
        }

        // now we have a list of MenuItems containing items to be dropped
        if(items.Length==0) goto nevermind;
        bool warnedAboutWearing = false;
        foreach(MenuItem mi in items)
          if(Wearing(mi.Item))
          { if(!warnedAboutWearing)
            { App.IO.Print("You cannot drop something you're wearing.");
              warnedAboutWearing = true;
            }
          }
          else if(!Equipped(mi.Item) || TryUnequip(mi.Item)) Drop(mi.Item, mi.Count);
      
        doneDropping: // after the items have been dropped
        newstress = CarryStress;
        if(newstress<stress)
        { if(newstress==CarryStress.Normal) App.IO.Print("Your actions are no longer burdened.");
          else App.IO.Print("You are still {0}.", newstress.ToString().ToLower());
        }
        break;
      }
      #endregion

      #region Eat
      case Action.Eat:
      { if(CarryStress>=CarryStress.Stressed) goto carrytoomuch;
        
        HungerLevel oldhl=HungerLevel, newhl;
        Item toEat = null;
        IInventory inv = null;
        if(!GroundPackUse("Eat", out toEat, out inv, ItemType.Food)) goto next;

        Food ic = (Food)toEat.Class;

        if(toEat.Count>1) toEat = toEat.Split(1);
        if(ic.EatTime==1) App.IO.Print("You eat {0}.", toEat.GetAName());
        else App.IO.Print("You {0} eating {1}.",
                          ic.Nutrition==ic.GetNutrition(toEat) ? "begin" : "continue", toEat.GetAName());
        bool warned=false, stoppedVoluntarily=false, interrupted=false;

        do
        { Map.MakeNoise(Pos, this, Noise.Item, NoiseLevel(ic.Noise));
          newhl = GainNutrition(ic.RemoveChunk(toEat));
          if(newhl==HungerLevel.Stuffed && !warned)
          { if(App.IO.YN("You're having a hard time getting it all down. Stop eating?", true))
            { stoppedVoluntarily = true;
              break;
            }
            warned = true;
          }
        } while(ic.GetNutrition(toEat)!=0 && (!NextTurn() || !(interrupted=true)));

        if(ic.GetNutrition(toEat)==0) // if we ate it all
        { if(ic.EatTime>1)
            App.IO.Print(newhl>=HungerLevel.Stuffed ? "You're finally finished." : "You finish eating.");
          else if(newhl==HungerLevel.Satiated && oldhl<newhl) App.IO.Print("That satiated your stomach!");
        }
        else // still some left
        { if(!stoppedVoluntarily) App.IO.Print("You stop eating.");
          if(inv==Inv && Pickup(toEat)==null) // if we got it from our pack but the remainder doesn't fit
          { App.IO.Print("You can't fit the remainder in your pack, so you set it on the ground.");
            Map.AddItem(Pos, toEat);
          }
        }

        OnUse(toEat);
        if(interrupted) goto renderNext;
        break;
      }
      #endregion
      
      #region ExamineTile
      case Action.ExamineTile:
        App.IO.ExamineTile(this, Pos);
        goto next;
      #endregion

      // TODO: case Action.Fire:
      
      #region GoDown and GoUp
      case Action.GoDown: case Action.GoUp:
      { TileType type = Map[Pos].Type;
        bool down = inp.Action==Action.GoDown;
        if(!Map.IsLink(type) || Map.IsDownLink(type)!=down)
        { App.IO.Print("You can't go {0} here!", down ? "down" : "up");
          goto next;
        }
        
        CarryStress stress = CarryStress;
        if(!down && stress>CarryStress.Burdened) goto carrytoomuch;

        Move(Map.GetLink(Pos));

        // 50% chance if burdened, otherwise 100% chance
        if(down && (stress>CarryStress.Burdened || stress==CarryStress.Burdened && Global.Coinflip()))
        { App.IO.Print("You fall down the stairs!");
          // TODO: make this have negative consequences
        }
        break;
      }
      #endregion

      #region Inventory
      case Action.Inventory:
        App.IO.DisplayInventory(Inv);
        goto next;
      #endregion
      
      #region Invoke
      case Action.Invoke:
      { if(CarryStress>CarryStress.Stressed) goto carrytoomuch;
        // TODO: make it only show the invokable items by default. this requires a change to InputOutput...
        MenuItem[] items = App.IO.ChooseItem("Invoke which item?", (Inventory)Inv, MenuFlag.None, ItemType.Any);
        if(items.Length==0) goto nevermind;
        Invoke(items[0].Item);
        break;
      }
      #endregion

      #region ManageSkills
      case Action.ManageSkills:
        App.IO.ManageSkills(this);
        goto next;
      #endregion

      #region Move
      case Action.Move:
      { Point np = Global.Move(Pos, inp.Direction); // np = our destination
        Entity c = Map.GetEntity(np); // see if there's a creature there

        if(c!=null) // if there's a monster there, attack it
        { throw new NotImplementedException();
        }
        else if(ManualMove(np))
        { // TODO: describe the tile we stop on
        }
        else goto next;
        break;
      }
      #endregion

      // TODO: case Action.MoveAFAP: case Action.MoveToDanger: case Action.MoveToInteresting:

      #region NameItem
      case Action.NameItem:
      { char c = App.IO.CharChoice("Name a specific item?", "ynQ", 'Q', true);
        if(c=='q') goto nevermind;
        MenuItem[] items = App.IO.ChooseItem(c=='y' ? "Name which item?" : "Name which class of items?",
                                             (Inventory)Inv, MenuFlag.None, ItemType.Any);
        if(items.Length==0) goto nevermind;
        string name = App.IO.Ask((c=='y' ? "Name" : "Call")+" "+items[0].Item.GetAName(true)+"?", true);
        if(c=='y') items[0].Item.Named = (name=="" ? null : name);
        else items[0].Item.Class.Called = (name=="" ? null : name);
        goto next;
      }
      #endregion
      
      #region OpenDoor
      case Action.OpenDoor:
      { if(CarryStress>=CarryStress.Strained) goto carrytoomuch;
        Direction dir = FindAdjacent(TileType.ClosedDoor);
        if(dir!=Direction.Invalid)
        { Point pt  = Global.Move(Pos, dir);
          Tile tile = Map[pt];
          if(tile.Type==TileType.OpenDoor) App.IO.Print("That door is already open.");
          else if(tile.Type!=TileType.ClosedDoor) App.IO.Print("There is no door there.");
          else if(tile.Is(TileFlag.Locked)) { App.IO.Print("That door is locked."); break; }
          else
          { Map.MakeNoise(pt, this, Noise.Walking, NoiseLevel(40));
            Map.SetType(pt, TileType.OpenDoor);
            break;
          }
        }
        goto next;
      }
      #endregion

      #region Quit
      case Action.Quit:
        if(App.IO.YesNo(Color.Warning, "Do you really want to quit?", false))
        { if(App.IO.YN("Do you want to save?", true)) Save();
          else Quit();
        }
        break;
      #endregion
      
      default: throw new NotImplementedException("Unhandled action: "+inp.Action);
    }
    
    App.IO.Render(this);
    return;

    renderNext: App.IO.Render(this); goto next;
    nevermind: App.IO.Print("Never mind."); goto next;
    carrytoomuch: App.IO.Print("You're carrying too much!"); goto next;
  }

  public bool Training(Skill skill) { return skillEnable==null || skillEnable[(int)skill]; }

  public void SetTraining(Skill skill, bool training)
  { if(skillEnable==null)
    { if(training) return;
      skillEnable = new bool[(int)Skill.NumSkills];
      for(int i=0; i<(int)Skill.NumSkills; i++) skillEnable[i] = true;
    }
    skillEnable[(int)skill] = training;
  }

  public Map Memory; // the player's memory of the current map
  public ArrayList Knowledge = new ArrayList(); // indices of known item classes
  public int ExpPool; // our points for exercising skills and attributes come from here
  public Race OriginalRace; // the race we chose at the beginning (our current race may be different due to polymorph, etc)

  void DropItemsIn(IInventory inv) // called interactively
  { bool warnedAboutWearing = false;
    for(int i=0; i<inv.Count; i++)
      if(Wearing(inv[i]))
      { if(!warnedAboutWearing)
        { App.IO.Print("You cannot drop something you're wearing.");
          warnedAboutWearing = true;
        }
      }
      else if(!Equipped(inv[i]) || TryUnequip(inv[i]))
      { Drop(inv[i]);
        i--;
      }
  }

  Direction FindAdjacent(TileType type)
  { Point pt = new Point(-1, -1);
    int dir=0;
    for(int d=0; d<8; d++)
      if(Map[Global.Move(Pos, d)].Type==type)
      { if(pt.X!=-1) { pt.X=-1; break; }
        pt  = Global.Move(Pos, d);
        dir = d;
      }
    if(pt.X!=-1) return (Direction)dir;
    return App.IO.ChooseDirection(false, false);
  }

  bool GroundPackUse(string verb, out Item item, out IInventory inv, params ItemType[] types)
  { item = null; inv = null;

    if(Map.HasItems(Pos)) // see if we have one on the ground first
    { Item[] items = Map[Pos].Items.GetItems(types);
      foreach(Item i in items)
      { char c = App.IO.CharChoice(string.Format("There {0} {1} here. {2} {3}?",
                                                 i.AreIs, i.GetFullName(), verb, i.ItOne), "ynq", 'q', true);
        if(c=='y') { item=i; inv=Map[Pos].Items; break; }
        else if(c=='q') { App.IO.Print("Never mind."); return false; }
      }
    }

    if(item==null) // we didn't get one from the ground
    { if(!Inv.Has(types)) { App.IO.Print("You have nothing to {0}!", verb.ToLower()); return false; }
      MenuItem[] items = App.IO.ChooseItem(verb+" what?", (Inventory)Inv, MenuFlag.None, types);
      if(items.Length==0) { App.IO.Print("Never mind."); return false; }
      if(Array.IndexOf(types, items[0].Item.Type)==-1)
      { App.IO.Print("You can't {0} that!", verb.ToLower());
        return false;
      }

      item = items[0].Item;
      inv  = Inv;
    }
    return true;
  }

  bool ManualMove(Point pt)
  { if(CarryStress>=CarryStress.Overtaxed) { App.IO.Print("You can't move while carrying so much!"); return false; }
    TileType type = Map[pt].Type;
    if(CanPass(type)) // TODO: using type.ToString() will be insufficient when we make IsDangerous() check for known traps
    { if(IsDangerous(pt) && Map[Pos].Type!=type &&
         !App.IO.YesNo(string.Format("Are you sure you want to move into {0}?", type.ToString().ToLower()), false))
        return false;

      if(Map.Type==MapType.Overworld)
      { if(Map.HasItems(Pos))
        { if(!App.IO.YN("Items left on the ground will surely disappear when you leave. Leave anyway?", false))
            return false;
          Map[Pos].Items.Clear();
        }
        // TODO: pass extra time while on the overworld (get extra hungry, process effects more, etc)
      }

      Pos = pt;
      Map.MakeNoise(pt, this, Noise.Walking, NoiseLevel(100));
      return true;
    }
    else if(type==TileType.Border && Map.Type==MapType.Town && App.IO.YN("Leave the town?", false))
    { for(int i=0; i<Map.Links.Length; i++)
      { Link link = Map.Links[i];
        if(link.ToSection[link.ToLevel].Type==MapType.Overworld) // find the link to the overworld
        { Move(Map.GetLink(i));
          return true;
        }
      }
      throw new ApplicationException("Unable to link the town with the overworld");
    }
    else return false;
  }

  void Move(Link link)
  { Map map = link.ToSection[link.ToLevel];
    Map.Entities.Remove(this);
    map.Entities.Add(this);
    Pos = link.ToPoint;
  }

  // waits until Player's next turn. returns true if player was interrupted during that turn
  bool NextTurn()
  { Map.Simulate(this); // this should return when it's our turn again
    base.Think();
    // check for visible enemy
    if(interrupt) { interrupt = false; return true; }
    return false;
  }

  byte NoiseLevel(byte amount) { return (byte)Math.Round((100-Stealth)*amount/100f); }

  void Quit() { HP=0; App.IsQuitting=true; Die(Death.Quit); }
  void Save() { App.IsQuitting = true; }

  Input inp;
  int NextSkillLevel(Skill skill) { throw new NotImplementedException(); }

  int[] attrExp = new int[(int)Attr.NumBasics];
  int[] skills = new int[(int)Skill.NumSkills], skillExp = new int[(int)Skill.NumSkills];
  bool[] skillEnable;
  bool interrupt;
}

} // namespace Chrono