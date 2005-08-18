using System;
using System.Collections;
using System.Xml;

namespace Chrono
{

#region Ring
[NoClone]
public abstract class Ring : Wearable
{ public Ring()
  { Class=ItemClass.Ring; Slot=Slot.Ring; Prefix="ring of "; PluralSuffix=""; PluralPrefix="rings of "; Weight=1;
    Durability=95; ExtraHunger=1;

    object i = namemap[GetType().ToString()];
    if(i==null) { Color = colors[namei]; namemap[GetType().ToString()] = namei++; }
    else Color = colors[(int)i];
  }
  static Ring() { Global.Randomize(names, colors); }

  public override string GetFullName(Entity e, bool forceSingular)
  { if(e==null || e.KnowsAbout(this)) return base.GetFullName(e, forceSingular);
    int i = (int)namemap[GetType().ToString()];
    string rn = names[i] + " ring";
    if(Title!=null) rn += " named "+Title;
    return rn;
  }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder==App.Player && ExtraHunger!=0) holder.Hunger += ExtraHunger;
    return false;
  }

  public int ExtraHunger;

  static Hashtable namemap = new Hashtable();
  static readonly string[] names;
  static readonly Color[] colors;
  static int namei;
}
#endregion

#region XmlRing
public sealed class XmlRing : Ring
{ public XmlRing(XmlNode node)
  { XmlItem.Init(this, node);
    FlagMods = XmlItem.GetEffect(node);
    if(!Xml.IsEmpty(node, "extraHunger")) ExtraHunger = Xml.IntValue(node, "extraHunger");
  }
}
#endregion

} // namespace Chrono