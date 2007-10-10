using System;
using System.Xml;

namespace Chrono
{

#region Ring
public abstract class Ring : Wearable
{
  protected Ring()
  {
    Type=ItemType.Ring; Slot=Slot.Ring; prefix="ring of "; pluralPrefix="rings of "; pluralSuffix=""; weight=30;
    ExtraHunger=1; Material=Metal.Instance;
  }

  public int ExtraHunger;
}
#endregion

#region XmlRing
public sealed class XmlRing : Ring
{
  public XmlRing(XmlNode node)
  {
    ItemClass.Init(this, node);
    ExtraHunger = Xml.Int(node, "extraHunger", ExtraHunger);
  }
}
#endregion

} // namespace Chrono