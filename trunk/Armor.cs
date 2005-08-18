using System;
using System.Xml;

namespace Chrono
{

#region Armor
public class Armor : Wearable
{ public Armor() { Class=ItemClass.Armor; }
  
  public override string GetFullName(Entity e, bool forceSingular)
  { if(!Identified) return base.GetFullName(e, forceSingular);
    string status = StatusString;
    if(status!="") status += ' ';
    string ret = status + (AC<baseAC ? "" : "+") + (AC-baseAC) + ' ' + Name;
    if(Title!=null) ret += " named "+Title;
    return ret;
  }

  protected int baseAC;
}
#endregion

#region XmlArmor
public sealed class XmlArmor : Armor
{ public XmlArmor(XmlNode node)
  { XmlItem.Init(this, node);

    Slot = XmlItem.GetSlot(node);
    if(!Xml.IsEmpty(node, "ac")) SetAttr(Attr.AC, baseAC=Xml.IntValue(node, "ac"));
    if(!Xml.IsEmpty(node, "ev")) SetAttr(Attr.EV, Xml.IntValue(node, "ev"));
  }
}
#endregion

} // namespace Chrono