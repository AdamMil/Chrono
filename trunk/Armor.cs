using System;

namespace Chrono
{

public class Armor : Wearable
{ public Armor() { Class=ItemClass.Armor; }

  public override string FullName
  { get
    { string ret = "a " + (AC<baseAC ? '-' : '+') + (AC-baseAC) + ' ' + Name;
      if(Title!=null) ret += " named "+Title;
      return ret;
    }
  }

  protected int baseAC;
}

public class PaperBag : Armor
{ public PaperBag()
  { Slot=Slot.Head; name="paper bag"; Color=Color.Brown; Weight=2;
    SetAttr(Attr.AC, baseAC=3); SetAttr(Attr.EV, -2);
  }
}

} // namespace Chrono