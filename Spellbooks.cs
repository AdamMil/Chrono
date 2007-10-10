using System;
using System.Xml;

namespace Chrono
{

#region Spellbook
public abstract class Spellbook : ItemClass
{
  protected Spellbook() { Type=ItemType.Spellbook; weight=1500; prefix="book of "; }

  public override object InitializeData(Item item) { return Global.NdN(4, 5); } // number of reads

  public Spell Spell;
}
#endregion

#region XmlSpellbook
public sealed class XmlSpellbook : Spellbook
{
  public XmlSpellbook(XmlNode node)
  {
    ItemClass.Init(this, node);
    Spell = Spell.Get(Xml.Attr(node, "spell"));
  }
}
#endregion

} // namespace Chrono