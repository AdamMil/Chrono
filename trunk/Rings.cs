using System;

namespace Chrono
{

public abstract class Ring : Wearable
{ public Ring()
  { Class=ItemClass.Ring; Slot=Slot.Ring; Prefix="ring of "; PluralSuffix=""; PluralPrefix="rings of "; Weight=1;
    Durability=95;
  }
  static Ring() { Global.RandomizeNames(names); }

  public override string GetFullName(Entity e)
  { if(e==null || e.KnowsAbout(this)) return FullName;
    string tn = GetType().ToString(), rn = (string)namemap[tn];
    if(rn==null)
    { namemap[tn] = rn = Global.AorAn(names[namei]) + ' ' + names[namei];
      namei++;
    }
    rn += " ring";
    if(Title!=null) rn += " named "+Title;
    return rn;
  }
  
  static System.Collections.Hashtable namemap = new System.Collections.Hashtable();
  static string[] names = new string[] { "gold", "silver", "brass", "iron" };
  static int namei;
}

public class InvisibilityRing : Ring
{ public InvisibilityRing()
  { name="invisibility"; Color=Color.DarkGrey; FlagMods=Entity.Flag.Invisible;
  }
  
  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder==App.Player) holder.Hunger += 2;
    return false;
  }
}

public class SeeInvisibleRing : Ring
{ public SeeInvisibleRing()
  { name="see invisible"; Color=Color.LightCyan; FlagMods=Entity.Flag.SeeInvisible;
  }
}

} // namespace Chrono