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
  }

  public override string GetFullName(bool forceSingular)
  { if(App.Player.KnowsAbout(this)) return base.GetFullName(forceSingular);
    string rn = Global.RingNames[NameIndex] + " ring";
    if(Title!=null) rn += " named "+Title;
    return rn;
  }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder==App.Player && ExtraHunger!=0) holder.Hunger += ExtraHunger;
    return false;
  }

  public int ExtraHunger, NameIndex;
}
#endregion

#region XmlRing
public sealed class XmlRing : Ring
{ public XmlRing() { }
  public XmlRing(XmlNode node)
  { XmlItem.InitModifying(this, node);
    if(!Xml.IsEmpty(node, "extraHunger")) ExtraHunger = Xml.Int(node, "extraHunger");
  }
}
#endregion

} // namespace Chrono