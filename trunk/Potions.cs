using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

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
  protected Potion(SerializationInfo info, StreamingContext context) : base(info, context) { }
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

  public static void Deserialize(System.IO.Stream stream, IFormatter formatter)
  { namemap = (Hashtable)formatter.Deserialize(stream);
    names   = (string[])formatter.Deserialize(stream);
    colors  = (Color[])formatter.Deserialize(stream);
    namei   = (int)formatter.Deserialize(stream);
  }
  public static void Serialize(System.IO.Stream stream, IFormatter formatter)
  { formatter.Serialize(stream, namemap);
    formatter.Serialize(stream, names);
    formatter.Serialize(stream, colors);
    formatter.Serialize(stream, namei);
  }

  static Hashtable namemap = new Hashtable();
  static string[] names = new string[] { "green", "purple", "bubbly", "fizzy" };
  static Color[]  colors = new Color[] { Color.Green, Color.Purple, Color.Grey, Color.Brown };
  static int namei;
}
#endregion

[Serializable]
public class HealPotion : Potion
{ public HealPotion() { name="healing"; }
  public HealPotion(SerializationInfo info, StreamingContext context) : base(info, context) { }

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

} // namespace Chrono