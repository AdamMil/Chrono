using System;
using System.Xml;

namespace Chrono
{

#region Tool
public abstract class Tool : ItemClass
{
  protected Tool() { Type=ItemType.Tool; }
}
#endregion

#region XmlTool
public sealed class XmlTool : Tool
{
  public XmlTool(XmlNode node)
  {
    ItemClass.Init(this, node);
    Spell = Spell.Get(Xml.Attr(node, "spell"));

    if(!Xml.IsEmpty(node, "usability"))
    {
      Usability = (ItemUse)Enum.Parse(typeof(ItemUse), Xml.Attr(node, "usability"));
    }
    else
    {
      switch(Spell.GetSpellTarget(null, null))
      {
        case SpellTarget.Self: Usability = ItemUse.Self; break;
        case SpellTarget.Item: Usability = ItemUse.Item; break;
        case SpellTarget.Tile: Usability = ItemUse.Tile; break;
      }
    }
  }

  public override bool Use(Item item, Entity user, Item usedOn)
  {
    if(TryUse(item, user))
    {
      if(user==App.Player) App.IO.Print("You use {0} on {1}.", item.GetAName(true), usedOn.GetAName());
      else if(App.Player.CanSee(user))
        App.IO.Print("{0} uses {1} on {2}.", user.TheName, item.GetAName(true), usedOn.GetAName());
      Spell.Cast(user, item, usedOn);
    }
    return false;
  }

  public override bool Use(Item item, Entity user, Direction dir)
  {
    if(TryUse(item, user))
    {
      if(user==App.Player) App.IO.Print("You use {0}.", item.GetAName(true));
      else if(App.Player.CanSee(user)) App.IO.Print("{0} uses {1}.", user.TheName, item.GetAName(true));
      Spell.Cast(user, item, dir);
    }
    return false;
  }

  public override bool Use(Item item, Entity user, System.Drawing.Point pt)
  {
    if(TryUse(item, user))
    {
      if(user==App.Player) App.IO.Print("You use {0}.", item.GetAName(true));
      else if(App.Player.CanSee(user)) App.IO.Print("{0} uses {1}.", user.TheName, item.GetAName(true));
      Spell.Cast(user, item, pt);
    }
    return false;
  }

  public readonly Spell Spell;

  bool TryUse(Item item, Entity user)
  {
    if(item.Is(ItemStatus.HasCharges))
    {
      if(item.Charges==0)
      {
        if(user==App.Player) App.IO.Print("Nothing happens.");
        return false;
      }
      else item.Charges--;
    }
    return true;
  }
}
#endregion

} // namespace Chrono