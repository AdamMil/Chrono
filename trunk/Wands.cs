using System;
using System.Drawing;

namespace Chrono
{

public abstract class Wand : Chargeable
{ public Wand() { Class=ItemClass.Wand; Weight=15; }
  static Wand() { Global.RandomizeNames(names); }

  public override string Name { get { return "wand of "+Spell.Name; } }

  public override string GetFullName(Entity e)
  { string suffix = Identified ? string.Format(" ({0}:{1})", Charges, Recharged) : "";
    if(e==null || e.KnowsAbout(this)) return FullName + suffix;
    string tn = GetType().ToString(), rn = (string)namemap[tn], status = StatusString;
    if(status!="") status += ' ';
    if(rn==null) namemap[tn] = rn = names[namei++];

    rn = status + rn + " wand" + suffix;
    if(Title!=null) rn += " named "+Title;
    return Global.AorAn(rn) + ' ' + rn;
  }

  public bool Zap(Entity user, Point target) { return Zap(user, target, Direction.Invalid); }
  public virtual bool Zap(Entity user, Point target, Direction dir)
  { if(Charges==0)
    { if(Global.Rand(100)<10)
      { if(user==App.Player) App.IO.Print("You wrest one last charge out of the wand, and it disintegrates.");
        Cast(user, target, dir);
        return true;
      }
      if(user==App.Player) App.IO.Print("Nothing seems to happen.");
      return false;
    }
    Cast(user, target, dir);
    Charges--;
    return false;
  }

  public Spell Spell;

  protected virtual void Cast(Entity user, Point target, Direction dir)
  { if(AutoIdentify && user==App.Player) user.AddKnowledge(this);
    Spell.Cast(user, Status, target, dir);
  }

  protected bool AutoIdentify;
  
  static System.Collections.Hashtable namemap = new System.Collections.Hashtable();
  static string[] names = new string[] { "gold", "forked", "lead", "pointy" };
  static int namei;
}

public class WandOfFire : Wand
{ public WandOfFire() { Spell=FireSpell.Default; Charges=Global.Rand(3, 7); AutoIdentify=true; }
}

} // namespace Chrono