using System;
using System.Drawing;

namespace Chrono
{

public abstract class Wand : Chargeable
{ public Wand() { Class=ItemClass.Wand; Weight=15; }

  public override string InvName { get { return FullName+string.Format(" ({0}:{1})", Charges, Recharged); } }
  public override string Name { get { return "wand of "+Spell.Name; } }
  
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
}

public class WandOfFire : Wand
{ public WandOfFire() { Spell=FireSpell.Default; Charges=Global.Rand(3, 7); }
}

} // namespace Chrono