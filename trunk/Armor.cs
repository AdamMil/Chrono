using System;

namespace Chrono
{

public class Armor : Wearable
{ public Armor() { Class=ItemClass.Armor; }
}

public class PaperBag : Armor
{ public PaperBag()
  { Slot=Slot.Head; Name="paper bag"; Color=Color.Brown; Weight=2; SetAttr(Attr.AC, 2); SetAttr(Attr.EV, -2);
  }
}

} // namespace Chrono