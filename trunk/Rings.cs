using System;

namespace Chrono
{

public abstract class Ring : Wearable
{ public Ring()
  { Class=ItemClass.Ring; Slot=Slot.Ring; Prefix="a ring of "; PluralSuffix=""; PluralPrefix="rings of "; Weight=1;
  }
}

public class InvisibilityRing : Ring
{ public InvisibilityRing()
  { name="invisibility"; Color=Color.DarkGrey; FlagMods=Entity.Flag.Invisible;
  }
  
  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder==App.Player) holder.Hunger += 2;
    return false;
  }
}

public class SeeInvisibleRing : Ring
{ public SeeInvisibleRing()
  { name="see invisible"; Color=Color.LightCyan; FlagMods=Entity.Flag.SeeInvisible;
  }
}

} // namespace Chrono