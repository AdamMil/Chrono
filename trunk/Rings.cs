using System;
using System.Collections;
using System.Runtime.Serialization;

namespace Chrono
{

#region Ring
[NoClone]
public abstract class Ring : Wearable
{ public Ring()
  { Class=ItemClass.Ring; Slot=Slot.Ring; Prefix="ring of "; PluralSuffix=""; PluralPrefix="rings of "; Weight=1;
    Durability=95;

    object i = namemap[GetType().ToString()];
    if(i==null) { Color = colors[namei]; namemap[GetType().ToString()] = namei++; }
    else Color = colors[(int)i];
  }
  protected Ring(SerializationInfo info, StreamingContext context) : base(info, context) { }
  static Ring() { Global.Randomize(names, colors); }

  public override string GetFullName(Entity e, bool forceSingular)
  { if(e==null || e.KnowsAbout(this)) return base.GetFullName(e, forceSingular);
    int i = (int)namemap[GetType().ToString()];
    string rn = names[i] + " ring";
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
  static string[] names = new string[] { "gold", "silver", "brass", "iron" };
  static Color[] colors = new Color[] { Color.Yellow, Color.Grey, Color.Yellow, Color.DarkGrey };
  static int namei;
}
#endregion

[Serializable]
public class InvisibilityRing : Ring
{ public InvisibilityRing() { name="invisibility"; FlagMods=Entity.Flag.Invisible; }
  public InvisibilityRing(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder==App.Player) holder.Hunger += 2;
    return false;
  }

  public static readonly int SpawnChance=50; // 0.5% chance
}

[Serializable]
public class SeeInvisibleRing : Ring
{ public SeeInvisibleRing() { name="see invisible"; FlagMods=Entity.Flag.SeeInvisible; }
  public SeeInvisibleRing(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public static readonly int SpawnChance=50; // 0.5% chance
}

} // namespace Chrono