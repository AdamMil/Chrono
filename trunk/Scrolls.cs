using System;

namespace Chrono
{

public abstract class Scroll : Readable
{ public Scroll()
  { Class=ItemClass.Scroll; Prefix="a scroll of "; PluralSuffix=""; PluralPrefix="scrolls of "; Weight=1;
    Durability=75;
  }
  protected Scroll(Item item) : base(item) { }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Scroll)item).Name==Name;
  }
}

public class TeleportScroll : Scroll
{ public TeleportScroll()
  { name="teleport"; Color=Color.White;
  }
  public TeleportScroll(Item item) : base(item) { }
  
  public override void Read(Entity user)
  { user.OnReadScroll(this);
    user.Position = user.Map.FreeSpace();
    if(user!=App.Player) App.IO.Print("{0} disappears.", user.TheName);
  }
}

} // namespace Chrono