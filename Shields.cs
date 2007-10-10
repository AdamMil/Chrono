using System;
using System.Xml;

namespace Chrono
{

#region Shield
public abstract class Shield : ItemClass
{
  protected Shield() { Type = ItemType.Shield; }

  public int BlockChance;
}
#endregion

#region XmlShield
public sealed class XmlShield : Shield
{
  public XmlShield(XmlNode node)
  {
    ItemClass.Init(this, node);
    BlockChance = Xml.Int(node, "blockChance");
  }
}
#endregion

} // namespace Chrono