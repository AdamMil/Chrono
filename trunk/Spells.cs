using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public enum SpellTarget { Self, Item, Tile };
public enum SpellClass // remember to add these to the Skill enum as well
{ Summoning, Enchantment, Telekinesis, Translocation, Transformation, Divination, Channeling, Necromancy, Elemental,
  Poison
}

public abstract class Spell
{ public void Cast(Entity user) { Cast(user, user.Position, Direction.Self); }
  public void Cast(Entity user, RangeTarget rt)
  { if(rt.Dir!=Direction.Invalid)
    { Point np = rt.Dir>=Direction.Above ? user.Position : Global.Move(user.Position, rt.Dir);
      Cast(user, np, rt.Dir);
    }
    else if(rt.Point.X!=-1) Cast(user, rt.Point, rt.Dir);
  }
  public virtual void Cast(Entity user, Item item) { }
  public virtual void Cast(Entity user, Point tile, Direction dir) { }

  public Skill Exercises { get { return (Skill)((int)Class+(int)Skill.WeaponSkills); } }
  public int Level { get { return (Difficulty+1)/2; } }

  // (Int-7) * 100 * (1.2 - 1/(skill^1.175)) / Difficulty - 10
  public int CastChance(Entity user) // assuming the user knows it
  { int skill = (user.GetSkill(Skill.Casting)+GetSpellSkill(user)+1)/2;
    double smul = 1.2-1/Math.Pow(1.1746189430880190059144636656919, skill);
    int chance = (int)Math.Round((user.Int-7)*100*smul/Difficulty) - 10;
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }
  public bool CastTest(Entity user) { return Global.Rand(100)<CastChance(user); }

  // (Int-8) * 100 * (1.1 - 1/(skill^1.126)) / Difficulty - 20
  public int LearnChance(Entity user)
  { double smul = 1.1-1/Math.Pow(1.2589254117941672104239541063958, GetSpellSkill(user));
    int chance = (int)Math.Round((user.Int-8)*100*smul/Difficulty) - 20;
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }

  public virtual ICollection TracePath(Entity user, Point pt) { return null; }

  public string Name, Description;
  public SpellClass  Class;
  public SpellTarget Target;
  public int Difficulty; // 1-18, 1,2=level 1, 3,4=level 2, etc
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
    { if(++bounces==3) return TraceAction.Stop;
      ret = TraceAction.Bounce;
      if(!Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret &= ~TraceAction.HBounce;
      if(!Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret &= ~TraceAction.VBounce;
      if(ret==0) ret=TraceAction.Bounce;
    }
    else ret = TraceAction.Go;
    oldPt=pt;
    return ret;
  }

  TraceAction ZapPoint(Point pt, object context)
  { Entity user = (Entity)context;
    TraceAction ret;
    if(!Map.IsPassable(user.Map[pt].Type))
    { if(++bounces==3) return TraceAction.Stop;
      ret = TraceAction.Bounce;
      if(!Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret &= ~TraceAction.HBounce;
      if(!Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret &= ~TraceAction.VBounce;
      if(user==App.Player) App.IO.Print("The spell bounces!");
    }
    else ret=TraceAction.Go;
    oldPt=pt;
    object affected = Hit(user, pt);
    if(affected!=null) Affect(user, affected);
    return ret;
  }
  
  Point oldPt;
  int bounces;
}

public class ForceBolt : BeamSpell
{ public ForceBolt()
  { Name="force bolt"; Class=SpellClass.Telekinesis; Difficulty=1; Power=2;
    Description = "The spell forces stuff. Yeah.";
  }

  protected override object Hit(Entity user, Point pt) { return user.Map.GetEntity(pt); }

  protected override bool Affect(Entity user, Direction dir)
  { if(dir==Direction.Above)
    { if(user==App.Player) App.IO.Print("Bits of stone rain down on you as the spell slams into the ceiling.");
      return false;
    }
    else if(dir==Direction.Below)
    { if(user==App.Player) App.IO.Print("The bugs on the ground are crushed!");
    }
    return false;
  }

  protected override void Affect(Entity user, object obj)
  { Entity e = obj as Entity;
    if(e!=null)
    { Damage damage = new Damage(Global.NdN(1, 6));
      damage.Direct = 2;
      e.OnHitBy(user, this, damage);
      user.OnHit(e, this, damage);
      e.DoDamage(user, Death.Combat, damage);
    }
  }

  public static ForceBolt Default = new ForceBolt();
}

public class FireSpell : BeamSpell
{ public FireSpell()
  { Name="fire"; Class=SpellClass.Elemental; Difficulty=10; Power=12;
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
    { Damage damage = new Damage();
      damage.Heat = (ushort)Global.NdN(4, 10);
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
      { string plural = i.Count>1 ? "" : "s";
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
  { Name="teleport"; Class=SpellClass.Translocation; Difficulty=6; Power=9; Target=SpellTarget.Self;
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
  { Name="amnesia"; Class=SpellClass.Divination; Difficulty=2; Power=2; Target=SpellTarget.Self;
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