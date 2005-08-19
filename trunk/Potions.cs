using System;
using System.Collections;
using System.Drawing;
using System.Xml;

namespace Chrono
{

#region Potion
[NoClone]
public abstract class Potion : Item
{ public Potion()
  { Class=ItemClass.Potion; Prefix="potion of "; PluralPrefix="potions of "; PluralSuffix=""; Weight=5;
  }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Potion)item).Name==Name;
  }

  public abstract void Drink(Entity user);

  public override string GetFullName(Entity e, bool forceSingular)
  { if(e==null || e.KnowsAbout(this)) return base.GetFullName(e, forceSingular);
    string rn = !forceSingular && Count>1 ? Count.ToString() + ' ' + names[NameIndex] + " potions"
                                          : names[NameIndex] + " potion";
    if(Title!=null) rn += " named "+Title;
    return rn;
  }
  
  public int NameIndex;
}
#endregion

#region XmlPotion
public sealed class XmlPotion : Potion
{ public XmlPotion(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);
  }

  public override void Drink(Entity user)
  { user.OnDrink(this);
    Spell.Cast(user, Status);
  }

  public Spell Spell;
}
#endregion

} // namespace Chrono