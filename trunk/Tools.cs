using System;
using System.Xml;

namespace Chrono
{

public abstract class Tool : Item
{ public Tool() { Class=ItemClass.Tool; }
}

public abstract class ChargedTool : Chargeable
{ public ChargedTool() { Class=ItemClass.Tool; }
}

#region XmlTool
public sealed class XmlTool : Tool
{ public XmlTool() { }
  public XmlTool(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);

    switch(Spell.GetSpellTarget(App.Player))
    { case SpellTarget.Self: Usability=ItemUse.Self; break;
      case SpellTarget.Tile: Usability=ItemUse.Both; break;
      default: throw new NotImplementedException("unhandled spell target");
    }
  }

  public override bool CanStackWith(Item item) { return base.CanStackWith(item) && item.Name==Name; }

  public override bool Use(Entity user, Direction dir)
  { user.OnUse(this);
    Spell.Cast(user, Status, dir);
    return false;
  }

  public override bool Use(Entity user, System.Drawing.Point target)
  { user.OnUse(this);
    Spell.Cast(user, Status, target);
    return false;
  }

  public Spell Spell;
}
#endregion

#region XmlChargeTool
public sealed class XmlChargedTool : ChargedTool
{ public XmlChargedTool() { }
  public XmlChargedTool(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);
    Charges = Xml.RangeInt(node, "charges");

    switch(Spell.GetSpellTarget(App.Player))
    { case SpellTarget.Self: Usability=ItemUse.Self; break;
      case SpellTarget.Tile: Usability=ItemUse.Both; break;
      default: throw new NotImplementedException("unhandled spell target");
    }
  }
  
  public override bool CanStackWith(Item item) { return base.CanStackWith(item) && item.Name==Name; }

  public override bool Use(Entity user, Direction dir)
  { user.OnUse(this);
    if(Charges>0)
    { Spell.Cast(user, Status, dir);
      Charges--;
      return false;
    }
    else return base.Use(user, dir);
  }

  public override bool Use(Entity user, System.Drawing.Point target)
  { user.OnUse(this);
    if(Charges>0)
    { Spell.Cast(user, Status, target);
      Charges--;
      return false;
    }
    else return base.Use(user, target);
  }

  public Spell Spell;
}
#endregion

} // namespace Chrono