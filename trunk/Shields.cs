using System;
using System.Runtime.Serialization;

namespace Chrono
{

public abstract class Shield : Wieldable
{ public Shield() { Class=ItemClass.Shield; }
  protected Shield(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override string FullName
  { get
    { if(!Identified) return base.FullName;
      string status = StatusString;
      if(status!="") status += ' ';
      string ret = status + (AC<0 ? "" : "+") + AC + ' ' + Name;
      if(Title!=null) ret += " named "+Title;
      return ret;
    }
  }

  public int BlockChance; // base percentage chance that this shield will block a blow
}

[Serializable]
public class Buckler : Shield
{ public Buckler()
  { name="buckler"; BlockChance=15; Weight=40; Color=Color.Grey; AC=0; EV=-1;
    ShortDesc="A small, round shield.";
  }
  public Buckler(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

} // namespace Chrono