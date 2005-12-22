using System;
using System.Xml;

namespace Chrono
{

#region Treasure
public abstract class Treasure : ItemClass
{ protected Treasure() { Type=ItemType.Treasure; }
}
#endregion

#region XmlTreasure
public sealed class XmlTreasure : Treasure
{ public XmlTreasure(XmlNode node) { ItemClass.Init(this, node); }
}
#endregion

} // namespace Chrono