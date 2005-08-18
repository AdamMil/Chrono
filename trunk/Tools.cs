using System;
using System.Xml;

namespace Chrono
{

#region XmlTool
public sealed class XmlTool : Item
{ public XmlTool(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);

    switch(Spell.GetSpellTarget(App.Player))
    { case SpellTarget.Self: Usability=ItemUse.Self; break;
      case SpellTarget.Tile: Usability=ItemUse.Both; break;
      default: throw new NotImplementedException("unhandled spell target");
    }
  }
  
  public override bool Use(Entity user, Direction dir)
  { Spell.Cast(user, Status, dir);
    return false;
  }

  public override bool Use(Entity user, System.Drawing.Point target)
  { Spell.Cast(user, Status, target);
    return false;
  }

  public Spell Spell;
}
#endregion

#region XmlChargeTool
public sealed class XmlChargedTool : Chargeable
{ public XmlChargedTool(XmlNode node)
  { XmlItem.Init(this, node);
    Spell = XmlItem.GetSpell(node);
    Charges = Xml.RangeInt(node, "charges");

    switch(Spell.GetSpellTarget(App.Player))
    { case SpellTarget.Self: Usability=ItemUse.Self; break;
      case SpellTarget.Tile: Usability=ItemUse.Both; break;
      default: throw new NotImplementedException("unhandled spell target");
    }
  }
  
  public override bool Use(Entity user, Direction dir)
  { if(Charges>0)
    { Spell.Cast(user, Status, dir);
      Charges--;
      return false;
    }
    else return base.Use(user, dir);
  }

  public override bool Use(Entity user, System.Drawing.Point target)
  { if(Charges>0)
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