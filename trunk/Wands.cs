using System;
using System.Drawing;

namespace Chrono
{

public abstract class Wand : Chargeable
{ public Wand() { Class=ItemClass.Wand; Weight=15; }
  static Wand() { Global.RandomizeNames(names); }

  public override string Name { get { return "wand of "+Spell.Name; } }

  public override string GetFullName(Entity e)
  { if(e==null || e.KnowsAbout(this)) return FullName;
    string tn = GetType().ToString(), rn = (string)namemap[tn];
    if(rn==null)
    { namemap[tn] = rn = Global.AorAn(names[namei]) + ' ' + names[namei];
      namei++;
    }
    rn += " wand";
    if(Title!=null) rn += " (called "+Title+')';
    return rn;
  }
  
  public override string GetInvName(Entity e)
  { return GetFullName(e)+string.Format(" ({0}:{1})", Charges, Recharged);
  }

  public bool Zap(Entity user, Point target) { return Zap(user, target, Direction.Invalid); }
  public virtual bool Zap(Entity user, Point target, Direction dir)
  { if(Charges==0)
    { if(Global.Rand(100)<10)
      { if(user==App.Player) App.IO.Print("You wrest one last charge out of the wand, and it disintegrates.");
        Spell.Cast(user, target, dir);
        return true;
      }
      if(user==App.Player) App.IO.Print("Nothing seems to happen.");
      return false;
    }
    Spell.Cast(user, target, dir);
    Charges--;
    return false;
  }

  public Spell Spell;
  
  static System.Collections.Hashtable namemap = new System.Collections.Hashtable();
  static string[] names = new string[] { "gold", "forked", "lead", "pointy" };
  static int namei;
}

public class WandOfFire : Wand
{ public WandOfFire() { Spell=FireSpell.Default; Charges=Global.Rand(3, 7); }
}

} // namespace Chrono