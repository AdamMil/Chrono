using System;
using System.Xml;

namespace Chrono
{

#region Potion
public abstract class Potion : ItemClass
{
  protected Potion()
  {
    Type=ItemType.Potion; prefix="potion of "; pluralPrefix="potions of "; pluralSuffix=""; weight=450;
  }

  public abstract void OnDrink(Entity user, Item item);
}
#endregion

#region XmlPotion
public sealed class XmlPotion : Potion
{
  public XmlPotion(XmlNode node)
  {
    ItemClass.Init(this, node);
    Spell = Spell.Get(Xml.Attr(node, "spell"));
  }

  public override void OnDrink(Entity user, Item item)
  {
    user.OnDrink(item);
    Spell.Cast(user, item);
  }

  public Spell Spell;
}
#endregion

} // namespace Chrono