using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

#region Potion
public abstract class Potion : Item
{ public Potion()
  { Class=ItemClass.Potion; Prefix="potion of "; PluralPrefix="potions of "; PluralSuffix=""; Weight=5;
  }
  protected Potion(SerializationInfo info, StreamingContext context) : base(info, context) { }
  static Potion() { Global.RandomizeNames(names); }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Potion)item).Name==Name;
  }

  public abstract void Drink(Entity user);

  public override string GetFullName(Entity e)
  { if(e==null || e.KnowsAbout(this)) return FullName;
    string tn = GetType().ToString(), rn = (string)namemap[tn];
    if(rn==null) namemap[tn] = rn = names[namei++];
    rn = Count>1 ? Count.ToString() + ' ' + rn + " potions" : Global.AorAn(rn) + ' ' + rn + " potion";
    if(Title!=null) rn += " named "+Title;
    return rn;
  }

  public static void Deserialize(System.IO.Stream stream, IFormatter formatter)
  { namemap = (Hashtable)formatter.Deserialize(stream);
    names   = (string[])formatter.Deserialize(stream);
    namei   = (int)formatter.Deserialize(stream);
  }
  public static void Serialize(System.IO.Stream stream, IFormatter formatter)
  { formatter.Serialize(stream, namemap);
    formatter.Serialize(stream, names);
    formatter.Serialize(stream, namei);
  }

  static Hashtable namemap = new Hashtable();
  static string[] names = new string[] { "green", "purple", "bubbly", "fizzy" };
  static int namei;
}
#endregion

[Serializable]
public class HealPotion : Potion
{ public HealPotion() { name="healing"; Color=Color.LightGreen; }
  public HealPotion(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public override void Drink(Entity user)
  { user.OnDrink(this);
    if(user.MaxHP-user.HP>0)
    { user.HP += Global.NdN(4, 6);
      if(user==App.Player) App.IO.Print("You feel better.");
      else if(App.Player.CanSee(user)) App.IO.Print("{0} looks better.");
    }
    else if(user==App.Player) App.IO.Print("Nothing seems to happen.");
  }

  public override bool Hit(Entity user, Point pos) { return true; }
  public override bool Hit(Entity user, Entity hit) { return true; }
}

} // namespace Chrono