using System;

namespace Chrono
{

public abstract class Scroll : Readable
{ public Scroll()
  { Class=ItemClass.Scroll; Prefix="a scroll of "; PluralSuffix=""; PluralPrefix="scrolls of "; Weight=1;
  }
  protected Scroll(Item item) : base(item) { }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Scroll)item).Name==Name;
  }
}

public class TeleportScroll : Scroll
{ public TeleportScroll()
  { name="teleport"; Color=Color.White; UseTarget=true;
  }
  public TeleportScroll(Item item) : base(item) { }
  
  public override void Read(Entity user)
  { user.OnReadScroll(this);
    user.Position = user.Map.FreeSpace();
    if(user!=App.Player) App.IO.Print("{0} disappears.", user.TheName);
  }

  public override bool Use(Entity user, System.Drawing.Point target)
  { Entity e = user.Map.GetEntity(target);
    if(e!=null)
    { if(user==App.Player) Read(e);
      else App.IO.Print("{0} disappears.", e.TheName);
    }
    else if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return true;
  }
}

} // namespace Chrono