using System;

namespace Chrono
{

public class Armor : Wearable
{ public Armor() { Class=ItemClass.Armor; }
}

public class PaperBag : Armor
{ public PaperBag()
  { Slot=Slot.Head; name="paper bag"; Color=Color.Brown; Weight=2; SetAttr(Attr.AC, 3); SetAttr(Attr.EV, -2);
  }
}

} // namespace Chrono