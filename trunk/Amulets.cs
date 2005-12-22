using System;
using System.Xml;

namespace Chrono
{

#region Amulet
public abstract class Amulet : Wearable
{ protected Amulet() { Type=ItemType.Amulet; Slot=Slot.Neck; Material=Metal.Instance; }
}
#endregion

public sealed class LevitationAmulet : Amulet
{ public LevitationAmulet()
  { name="blue amulet"; idName="amulet of levitation"; Color=Color.LightBlue; weight=1360;
    Price=200; SpawnChance=75;
  }
  
  public override Ability Modify(Item item, Ability flags) { return flags|Ability.Levitating; }
}

#region XmlAmulet
public sealed class XmlAmulet : Amulet
{ public XmlAmulet(XmlNode node) { ItemClass.InitWearable(this, node); }
}
#endregion

} // namespace Chrono