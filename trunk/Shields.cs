using System;

namespace Chrono
{

public abstract class Shield : Wieldable
{ public Shield() { Class=ItemClass.Shield; }

  public override string FullName
  { get
    { string ret = "a " + (AC<0 ? "" : "+") + AC + ' ' + Name;
      if(Title!=null) ret += " named "+Title;
      return ret;
    }
  }

  public int BlockChance; // base percentage chance that this shield will block a blow
}

public class Buckler : Shield
{ public Buckler()
  { name="buckler"; BlockChance=15; Weight=40; Color=Color.Grey; AC=0; EV=-1;
    ShortDesc="A small, round shield.";
  }
}

} // namespace Chrono