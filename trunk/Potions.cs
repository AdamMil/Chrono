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
    object i = namemap[GetType().ToString()];
    if(i==null) { Color = colors[namei]; namemap[GetType().ToString()] = namei++; }
    else Color = colors[(int)i];
  }
  static Potion() { Global.Randomize(names, colors); }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Potion)item).Name==Name;
  }

  public abstract void Drink(Entity user);

  public override string GetFullName(Entity e, bool forceSingular)
  { if(e==null || e.KnowsAbout(this)) return base.GetFullName(e, forceSingular);
    int i = (int)namemap[GetType().ToString()];
    string rn = !forceSingular && Count>1 ? Count.ToString() + ' ' + names[i] + " potions" : names[i] + " potion";
    if(Title!=null) rn += " named "+Title;
    return rn;
  }

  static Hashtable namemap = new Hashtable();
  static readonly string[] names;
  static readonly Color[]  colors;
  static int namei;
}
#endregion

public class HealPotion : Potion
{ public HealPotion() { name="healing"; }

  public override void Drink(Entity user)
  { user.OnDrink(this);
    if(Cursed)
    { Damage d = new Damage();
      d.Poison = 1;
      user.DoDamage(this, Death.Sickness, d);
      if(user==App.Player) App.IO.Print("Eww, this tastes putrid!");
    }
    else if(user.MaxHP-user.HP>0)
    { user.HP += Global.NdN(4, 6) * (Blessed ? 2 : 1);
      if(user==App.Player) App.IO.Print("You feel better.");
      else if(App.Player.CanSee(user)) App.IO.Print("{0} looks better.", user.TheName);
    }
    else if(user==App.Player) App.IO.Print("Nothing seems to happen.");
  }

  public override bool Hit(Entity user, Point pos) { return true; }
  public override bool Hit(Entity user, Entity hit) { return true; }

  public static readonly int SpawnChance=200; // 2% chance
  public static readonly int ShopValue=50;
}

#region XmlPotion
public sealed class XmlPotion : Potion
{ public XmlPotion(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);
  }

  public override void Drink(Entity user) { Spell.Cast(user, Status); }

  public Spell Spell;
}
#endregion

} // namespace Chrono