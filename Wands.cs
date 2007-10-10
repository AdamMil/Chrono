using System;
using System.Xml;

namespace Chrono
{

#region Wand
public abstract class Wand : ItemClass
{
  protected Wand() { Type=ItemType.Wand; weight=3400; }

  public override string GetBaseName(Item item) { return Identified ? "wand of "+Spell.Name : "wand"; }

  public Spell Spell;
  public string EffectMessage; // message shown on first use
}
#endregion

#region XmlWand
public sealed class XmlWand : Wand
{
  public XmlWand(XmlNode node)
  {
    ItemClass.Init(this, node);
    Spell         = Spell.Get(Xml.Attr(node, "spell"));
    Charges       = new Range(node.Attributes["charges"]);
    EffectMessage = Xml.Attr(node, "effectMsg");
  }
}
#endregion

} // namespace Chrono