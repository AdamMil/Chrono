using System;

namespace Chrono
{

public abstract class Scroll : Readable
{ public Scroll()
  { Class=ItemClass.Scroll; Prefix="a scroll of "; PluralSuffix=""; PluralPrefix="scrolls of "; Weight=1;
    Durability=75;
  }
  protected Scroll(Item item) : base(item) { Spell=((Scroll)item).Spell; }
  static Scroll() { Global.RandomizeNames(names); }

  public override string Name { get { return Spell.Name; } }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Scroll)item).Name==Name;
  }

  public override string GetFullName(Entity e)
  { if(e==null || e.KnowsAbout(this)) return FullName;
    string tn = GetType().ToString(), rn = (string)namemap[tn];
    if(rn==null) namemap[tn] = rn = names[namei++];
    rn = "a scroll named "+rn;
    if(Title!=null) rn += " (called "+Title+')';
    return rn;
  }

  public virtual void Read(Entity user)
  { user.OnReadScroll(this);
    Spell.Cast(user);
  }

  public Spell Spell;

  static System.Collections.Hashtable namemap = new System.Collections.Hashtable();
  static string[] names = new string[] { "READ ME", "XGOCL APLFLCH", "DROWSSAP", "EUREKA!" };
  static int namei;
}

public class TeleportScroll : Scroll
{ public TeleportScroll() { name="teleport"; Color=Color.White; Spell=TeleportSpell.Default; }
  public TeleportScroll(Item item) : base(item) { }
}

} // namespace Chrono