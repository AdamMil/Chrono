using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public enum SpellTarget { Item, Tile };
public enum SpellClass
{ Summoning, Enchantment, Translocation, Transformation, Divination, Channeling, Necromancy, Elemental, Poison
}

public abstract class Spell
{ public void Cast(Entity user) { Cast(user, user.Position, Direction.Self); }
  public virtual void Cast(Entity user, Item item) { }
  public virtual void Cast(Entity user, Point tile, Direction dir) { }

  public int Level { get { return Difficulty/100+1; } }

  public int CastChance(Entity user) // assuming the user knows it
  { double div = Math.Pow(1.25, user.Int)/8;
    int skill = (user.GetSkill(Skill.Casting)+GetSpellSkill(user)+1)/2;
    if(skill>0) div += skill*Math.Pow(1.02, skill)*16*div/100;
    int chance = 100-(int)Math.Round((Difficulty+50)/div);
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }
  public bool CastTest(Entity user) { return Global.Rand(100)<CastChance(user); }

  public int LearnChance(Entity user)
  { double div = Math.Pow(1.25, user.Int)/8;                // 1.25 ** INT
    div += GetSpellSkill(user)*25*div/100;                  // + 25% per skill level
    int chance = 100-(int)Math.Round((Difficulty+100)/div); // 100 - (Difficulty+100)/that value
    chance += chance*20/100;                                // + 20%
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }

  public virtual ICollection TracePath(Entity user, Point pt) { return null; }

  public string Name, Description;
  public SpellClass  Class;
  public SpellTarget Target;
  public int Difficulty; // 0-99=level 1, 100-199=level 2, 200-299=level 3, etc
  public int Memory; // memory of this spell, decreased every turn that the spell isn't cast, forgotten at zero
  public int Power;  // MP usage
  
  protected int GetSpellSkill(Entity user) { return user.GetSkill((Skill)((int)Class+(int)Skill.WeaponSkills)); }

  protected static ArrayList path = new ArrayList();
}

public abstract class BeamSpell : Spell
{ protected BeamSpell() { Target=SpellTarget.Tile; }
  
  public override void Cast(Entity user, Point tile, Direction dir)
  { if((dir==Direction.Above || dir==Direction.Below) && !Affect(user, dir)) return;
    else if(user.Position==tile) Affect(user, user);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, 10, false, new LinePoint(ZapPoint), user);
    }
  }

  public override ICollection TracePath(Entity user, Point tile)
  { path.Clear();
    if(user.Position==tile) path.Add(tile);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, 10, false, new LinePoint(TracePoint), user);
    }
    return path;
  }

  protected abstract object Hit(Entity user, Point pt);
  protected abstract void Affect(Entity user, object obj);
  protected abstract bool Affect(Entity user, Direction dir); // returns if execution should continue (in Cast)

  TraceAction TracePoint(Point pt, object context)
  { Entity user = (Entity)context;
    path.Add(pt);
    TraceAction ret;
    if(!Map.IsPassable(user.Map[pt].Type))
    { ret = (TraceAction)0;
      if(Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret |= TraceAction.HBounce;
      if(Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret |= TraceAction.VBounce;
      if(ret>0)
      { if(++bounces==3) return TraceAction.Stop;
      }
      else ret = TraceAction.Go;
    }
    else ret = TraceAction.Go;
    oldPt=pt;
    path.Add(pt);
    return ret;
  }

  TraceAction ZapPoint(Point pt, object context)
  { Entity user = (Entity)context;
    TraceAction ret;
    if(!Map.IsPassable(user.Map[pt].Type))
    { ret = (TraceAction)0;
      if(Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret |= TraceAction.HBounce;
      if(Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret |= TraceAction.VBounce;
      if(ret>0)
      { if(++bounces==3) return TraceAction.Stop;
        if(user==App.Player) App.IO.Print("The spell bounces!");
      }
      else ret=TraceAction.Go;
    }
    else ret=TraceAction.Go;
    oldPt=pt;
    object affected = Hit(user, pt);
    if(affected!=null) Affect(user, affected);
    return TraceAction.Go;
  }
  
  Point oldPt;
  int bounces;
}

public class FireSpell : BeamSpell
{ public FireSpell()
  { Name="fire"; Class=SpellClass.Elemental; Difficulty=550; Power=12;
    Description = "The fire spell hurls a great bolt of flames.";
  }

  public static FireSpell Default = new FireSpell();

  protected override object Hit(Entity user, Point pt)
  { Entity e = user.Map.GetEntity(pt);
    if(e==null) return pt;
    else if(Global.Coinflip()) return e;
    else
    { e.OnMissBy(user, this);
      user.OnMiss(e, this);
      return null;
    }
  }

  protected override bool Affect(Entity user, Direction dir)
  { if(dir==Direction.Above)
    { if(user==App.Player) App.IO.Print("The spell bounces back down onto your head!");
      return true;
    }
    else if(dir==Direction.Below)
    { if(user==App.Player) App.IO.Print("The bugs on the ground are incinerated!");
      IInventory inv = user.Map[user.Position].Items;
      if(inv!=null)
      { bool print = App.Player.CanSee(user.Position);
        for(int i=0; i<inv.Count; i++) if(AffectItem(inv, inv[i], user==App.Player)) i--;
      }
    }
    return false;
  }

  protected override void Affect(Entity user, object obj)
  { Entity e = obj as Entity;
    bool print;
    if(e!=null)
    { int damage = Global.NdN(4, 10);
      e.OnHitBy(user, this, damage);
      user.OnHit(e, this, damage);
      print = App.Player.CanSee(e);
      for(int i=0; i<e.Inv.Count; i++) if(Global.Rand(100)<30 && AffectItem(e.Inv, e.Inv[i], print)) i--;
      e.DoDamage(user, Death.Combat, damage);
    }
    else
    { Point pt = (Point)obj;
      IInventory inv = user.Map[pt].Items;
      if(inv!=null)
      { print = App.Player.CanSee(pt);
        for(int i=0; i<inv.Count; i++) if(AffectItem(inv, inv[i], print)) i--;
      }
    }
  }

  bool AffectItem(IInventory inv, Item i, bool print)
  { if(i.Class==ItemClass.Scroll || i.Class==ItemClass.Potion || i.Class==ItemClass.Spellbook)
    { if(print)
      { string plural = i.Count>1 ? "s" : "";
        App.IO.Print(i.Class==ItemClass.Potion ? "{0} heat{1} up and burst{1}!" : "{0} burn{1} up!",
                     Global.Cap1(i.ToString()), plural);
      }
      inv.Remove(i);
      return true;
    }
    return false;
  }
}

public class TeleportSpell : Spell
{ public TeleportSpell()
  { Name="teleport"; Class=SpellClass.Translocation; Difficulty=450; Power=9;
    Description = "This spell will teleport the caster to a random location.";
  }

  public override void Cast(Entity user, Point tile, Direction dir)
  { user.Position = user.Map.FreeSpace();
    if(user!=App.Player) App.IO.Print("{0} disappears.", user.TheName);
  }

  public static TeleportSpell Default = new TeleportSpell();
}

public class AmnesiaSpell : Spell
{ public AmnesiaSpell()
  { Name="amnesia"; Class=SpellClass.Divination; Difficulty=10; Power=2;
    Description = "This spell scrambles the caster's memory.";
  }
  
  public override void Cast(Entity user, Point tile, Direction dir)
  { if(user.Memory!=null)
    { Map old = user.Memory;
      user.Memory = new Map(user.Map.Width, user.Map.Height, TileType.Border, false); // wipe the memory
      int count = user.Map.Width*user.Map.Height/20;
      for(int i=0; i<count; i++) // put some of the old tiles in there
      { int x = Global.Rand(user.Map.Width), y = Global.Rand(user.Map.Height);
        if(old[x, y].Type!=TileType.Border)
        { user.Memory.SetType(x, y, user.Map[x, y].Type);
          user.Memory.SetFlag(x, y, Tile.Flag.Seen, true);
        }
      }
      for(int i=0; i<count; i++) // put some random tiles in there
      { int x = Global.Rand(user.Map.Width), y = Global.Rand(user.Map.Height);
        user.Memory.SetType(x, y, (TileType)Global.Rand((int)TileType.NumTypes));
      }
      if(user==App.Player) App.IO.Print("You feel your mind being twisted!");
    }
  }
  
  public static AmnesiaSpell Default = new AmnesiaSpell();
}

} // namespace Chrono