using System;

namespace Chrono
{

public class Armor : Wearable
{ public Armor() { Class=ItemClass.Armor; }

  public override string FullName
  { get
    { if(!Identified) return base.FullName;
      string status = StatusString;
      if(status!="") status += ' ';
      string ret = status + (AC<baseAC ? '-' : '+') + (AC-baseAC) + ' ' + Name;
      if(Title!=null) ret += " named "+Title;
      return Global.AorAn(ret) + ' ' + ret;
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