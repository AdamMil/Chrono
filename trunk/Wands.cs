using System;
using System.Collections;
using System.Drawing;
using System.Xml;

namespace Chrono
{

#region Wand
public abstract class Wand : Chargeable
{ public Wand() { Class=ItemClass.Wand; Weight=15; }

  public override string Name { get { return "wand of "+Spell.Name; } }

  public override string GetFullName(Entity e, bool forceSingular)
  { string suffix = Identified ? string.Format(" ({0}:{1})", Charges, Recharged) : "";
    if(e==null || e.KnowsAbout(this)) return base.GetFullName(e, forceSingular) + suffix;
    string status = status = StatusString;
    if(status!="") status += ' ';
    string rn = status + names[NameIndex] + " wand" + suffix;
    if(Title!=null) rn += " named "+Title;
    return rn;
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
  public string Effect; // the message shown on the first use
  public int NameIndex;

  protected virtual void Cast(Entity user, Point target, Direction dir)
  { if(Spell.AutoIdentify && !App.Player.KnowsAbout(this) && (user==App.Player || App.Player.CanSee(user)))
    { App.Player.AddKnowledge(this);
      if(Effect!=null) App.IO.Print(Effect);
      App.IO.Print("{0} is {1}.", user==App.Player ? "This" : "That", GetAName(user));
    }
    Spell.Cast(user, Status, target, dir);
  }
}
#endregion

#region XmlWand
public sealed class XmlWand : Wand
{ public XmlWand(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);
    Charges = Xml.RangeInt(node, "charges");
    if(!Xml.IsEmpty("effectDesc")) Effect = Xml.String(node, "effectDesc");
  }
}
#endregion

} // namespace Chrono