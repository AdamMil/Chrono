using System;
using System.Drawing;

namespace Chrono
{

public enum SpellTarget { Item, Tile };
public enum SpellClass
{ Summoning, Enchantment, Translocation, Transformation, Divination, Channeling, Necromancy, Elemental, Poison
}

public abstract class Spell
{ public virtual void Cast(Entity user, Item item) { }
  public virtual void Cast(Entity user, Point tile, Direction dir) { }

  public string Name;
  public SpellClass  Class;
  public SpellTarget Target;
  public int Level;  // level at which spell can be cast with 0% chance of failure
  public int Memory; // memory of this spell, decreased every turn that the spell isn't cast, forgotten at zero
}

public abstract class BeamSpell : Spell
{ protected BeamSpell() { Target=SpellTarget.Tile; }
  
  public override void Cast(Entity user, Point tile, Direction dir)
  { if((dir==Direction.Above || dir==Direction.Below) && !Affect(user, dir)) return;
    else if(user.Position==tile) Affect(user, user);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, 15, false, new LinePoint(ZapPoint), user);
    }
  }

  protected abstract object Hit(Entity user, Point pt);
  protected abstract void Affect(Entity user, object obj);
  protected abstract bool Affect(Entity user, Direction dir); // returns if execution should continue (in Cast)

  TraceAction ZapPoint(Point pt, object context)
  { Entity user = (Entity)context;
    if(!Map.IsPassable(user.Map[pt].Type))
    { if(++bounces==3) return TraceAction.Stop;
      if(user==App.Player) App.IO.Print("The spell bounces!");
      TraceAction ret = (TraceAction)0;
      if(!Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret |= TraceAction.VBounce;
      if(!Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret |= TraceAction.HBounce;
      return ret;
    }
    object affected = Hit(user, pt);
    if(affected!=null) Affect(user, affected);
    return TraceAction.Go;
  }
  
  Point oldPt;
  int bounces;
}

public class FireSpell : BeamSpell
{ public FireSpell() { Name="fire"; }

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
  { if(i.Class==ItemClass.Scroll || i.Class==ItemClass.Potion)
    { if(print)
      { string plural = i.Count>1 ? "s" : "";
        App.IO.Print(i.Class==ItemClass.Scroll ? "{0} burn{1} up!" : "{0} heat{1} up and burst{1}!",
                     Global.Cap1(i.ToString()), plural);
      }
      inv.Remove(i);
      return true;
    }
    return false;
  }
}

public class TeleportSpell : Spell
{ public TeleportSpell() { Name="teleport"; }

  public override void Cast(Entity user, Point tile, Direction dir)
  { user.Position = user.Map.FreeSpace();
    if(user!=App.Player) App.IO.Print("{0} disappears.", user.TheName);
  }

  public static TeleportSpell Default = new TeleportSpell();
}

} // namespace Chrono