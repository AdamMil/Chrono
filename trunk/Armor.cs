using System;
using System.Runtime.Serialization;

namespace Chrono
{

public class Armor : Wearable
{ public Armor() { Class=ItemClass.Armor; }
  protected Armor(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public override string GetFullName(Entity e, bool forceSingular)
  { if(!Identified) return base.GetFullName(e, forceSingular);
    string status = StatusString;
    if(status!="") status += ' ';
    string ret = status + (AC<baseAC ? "" : "+") + (AC-baseAC) + ' ' + Name;
    if(Title!=null) ret += " named "+Title;
    return ret;
  }

  protected int baseAC;
}

[Serializable]
public class PaperBag : Armor
{ public PaperBag()
  { Slot=Slot.Head; name="paper bag"; Color=Color.Brown; Weight=2; Material=Material.Paper;
    SetAttr(Attr.AC, baseAC=3); SetAttr(Attr.EV, -2);
  }
  public PaperBag(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public static readonly int SpawnChance=100; // 1% chance
  public static readonly int ShopValue=75;
}

} // namespace Chrono