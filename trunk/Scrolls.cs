using System;
using System.Collections;
using System.Xml;

namespace Chrono
{

#region Scroll
[NoClone]
public abstract class Scroll : Readable
{ public Scroll()
  { Class=ItemClass.Scroll; Prefix="scroll of "; PluralSuffix=""; PluralPrefix="scrolls of "; Weight=1;
    Durability=75; Color=Color.White;
  }

  public override string Name { get { return Spell.Name; } }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Scroll)item).Name==Name;
  }

  public override string GetFullName(Entity e, bool forceSingular)
  { if(e==null || e.KnowsAbout(this)) return base.GetFullName(e, forceSingular);
    string rn = names[NameIndex];
    rn = (!forceSingular && Count>1 ? Count+" scrolls" : "scroll") + " labeled "+rn;
    if(Title!=null) rn += " named "+Title;
    return rn;
  }

  public virtual void Read(Entity user) // only called interactively
  { if(user==App.Player) AutoIdentify();
    if(!Cast(user)) App.IO.Print("The scroll crumbles into dust.");
  }

  public Spell Spell;
  public int NameIndex;

  protected void AutoIdentify()
  { if(Spell.AutoIdentify && !App.Player.KnowsAbout(this))
    { App.Player.AddKnowledge(this);
      App.IO.Print("This is {0}.", GetAName(App.Player));
    }
  }

  protected bool Cast(Entity user)
  { switch(Spell.GetSpellTarget(user))
    { case SpellTarget.Self: Spell.Cast(user, Status, user.Position, Direction.Self); break;
      case SpellTarget.Item:
        MenuItem[] items = App.IO.ChooseItem(Prompt==null ? "Cast on which item?" : Prompt,
                                             user, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) return false;
        else Spell.Cast(user, Status, items[0].Item);
        break;
      case SpellTarget.Tile:
        RangeTarget rt = App.IO.ChooseTarget(user, Spell, true);
        if(rt.Dir!=Direction.Invalid || rt.Point.X!=-1) Spell.Cast(user, Status, rt);
        else return false;
        break;
    }
    return true;
  }

  protected string Prompt;
}
#endregion

public class IdentifyScroll : Scroll
{ public IdentifyScroll() { Spell=IdentifySpell.Default; Prompt="Identify which item?"; ShopValue=40; }

  public override void Read(Entity user)
  { if(user==App.Player) AutoIdentify();

    if(Blessed && Global.Coinflip())
    { foreach(Item i in user.Inv) if(!i.Identified) Spell.Cast(user, Status, i);
    }
    else
    { int n = Blessed ? Global.Rand(3) + 2 : 1;
      while(n-->0) Cast(user);
    }
  }

  public static readonly int SpawnChance=250; // 2.5% chance
}

#region XmlScroll
public sealed class XmlScroll : Scroll
{ public XmlScroll(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);
    if(!Xml.IsEmpty(node, "prompt")) Prompt = Xml.String(node, "prompt");
  }
}
#endregion

} // namespace Chrono