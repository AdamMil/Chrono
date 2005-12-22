using System;
using System.Xml;

namespace Chrono
{

#region Armor
public abstract class Armor : Wearable
{ protected Armor() { Type=ItemType.Armor; ShowEnchantment=true; }

  public override float Modify(Item item, Attr attr, float value)
  { if(attr==Attr.AC) return value + Math.Max(0, AC-item.Damage) + item.Enchantment;
    else if(attr==Attr.EV) return value + EV;
    else return value;
  }

  public int AC, EV;
}
#endregion

#region XmlArmor
public sealed class XmlArmor : Armor
{ public XmlArmor(XmlNode node) { ItemClass.InitWearable(this, node); }
}
#endregion

} // namespace Chrono