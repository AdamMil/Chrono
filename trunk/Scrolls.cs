using System;

namespace Chrono
{

public abstract class Scroll : Readable
{ public Scroll()
  { Class=ItemClass.Scroll; Prefix="a scroll of "; PluralSuffix=""; PluralPrefix="scrolls of "; Weight=1;
    Durability=75;
  }
  protected Scroll(Item item) : base(item) { Spell=((Scroll)item).Spell; }

  public override string Name { get { return Spell.Name; } }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Scroll)item).Name==Name;
  }

  public virtual void Read(Entity user)
  { user.OnReadScroll(this);
    Spell.Cast(user);
  }

  public Spell Spell;
}

public class TeleportScroll : Scroll
{ public TeleportScroll() { name="teleport"; Color=Color.White; Spell=TeleportSpell.Default; }
  public TeleportScroll(Item item) : base(item) { }
}

} // namespace Chrono