using System;
using System.Collections;
using System.Runtime.Serialization;

namespace Chrono
{

#region Ring
public abstract class Ring : Wearable
{ public Ring()
  { Class=ItemClass.Ring; Slot=Slot.Ring; Prefix="ring of "; PluralSuffix=""; PluralPrefix="rings of "; Weight=1;
    Durability=95;
  }
  protected Ring(SerializationInfo info, StreamingContext context) : base(info, context) { }
  static Ring() { Global.RandomizeNames(names); }

  public override string GetFullName(Entity e)
  { if(e==null || e.KnowsAbout(this)) return FullName;
    string tn = GetType().ToString(), rn = (string)namemap[tn];
    if(rn==null) namemap[tn] = rn = names[namei++];
    rn += " ring";
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
  static string[] names = new string[] { "gold", "silver", "brass", "iron" };
  static int namei;
}
#endregion

[Serializable]
public class InvisibilityRing : Ring
{ public InvisibilityRing() { name="invisibility"; Color=Color.DarkGrey; FlagMods=Entity.Flag.Invisible; }
  public InvisibilityRing(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder==App.Player) holder.Hunger += 2;
    return false;
  }
}

[Serializable]
public class SeeInvisibleRing : Ring
{ public SeeInvisibleRing() { name="see invisible"; Color=Color.LightCyan; FlagMods=Entity.Flag.SeeInvisible; }
  public SeeInvisibleRing(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

} // namespace Chrono