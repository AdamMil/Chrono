using System;
using System.Collections;
using System.Runtime.Serialization;

namespace Chrono
{

#region Scroll
public abstract class Scroll : Readable
{ public Scroll()
  { Class=ItemClass.Scroll; Prefix="scroll of "; PluralSuffix=""; PluralPrefix="scrolls of "; Weight=1;
    Durability=75;
  }
  protected Scroll(SerializationInfo info, StreamingContext context) : base(info, context) { }
  static Scroll() { Global.RandomizeNames(names); }

  public override string Name { get { return Spell.Name; } }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Scroll)item).Name==Name;
  }

  public override string GetFullName(Entity e)
  { if(e==null || e.KnowsAbout(this)) return FullName;
    string tn = GetType().ToString(), rn = (string)namemap[tn];
    if(rn==null) namemap[tn] = rn = names[namei++];
    rn = (Count>1 ? Count+" scrolls" : "scroll") + " labeled "+rn;
    if(Title!=null) rn += " named "+Title;
    return rn;
  }

  public virtual void Read(Entity user) // only called interactively
  { if(Spell.AutoIdentify && user==App.Player && !user.KnowsAbout(this))
    { user.AddKnowledge(this);
      App.IO.Print("This is {0}.", GetAName(user));
    }
    if(!Cast(user)) App.IO.Print("The scroll crumbles into dust.");
  }

  public Spell Spell;

  protected bool Cast(Entity user)
  { switch(Spell.Target)
    { case SpellTarget.Self: Spell.Cast(user, Status, user.Position, Direction.Self); break;
      case SpellTarget.Item:
        MenuItem[] items = App.IO.ChooseItem(Prompt==null ? "Cast on which item?" : Prompt,
                                             user, MenuFlag.None, ItemClass.Any);
        if(items.Length==0) return false;
        else Spell.Cast(user, Status, items[0].Item);
        break;
      case SpellTarget.Tile:
        RangeTarget rt = App.IO.ChooseTarget(user, Spell, true);
        if(rt.Dir!=Direction.Invalid || rt.Point.X!=-1) Spell.Cast(user, Status, rt);
        else return false;
        break;
    }
    return true;
  }

  protected string Prompt;

  public static void Deserialize(System.IO.Stream stream, IFormatter formatter)
  { namemap = (Hashtable)formatter.Deserialize(stream);
    names   = (string[])formatter.Deserialize(stream);
    namei   = (int)formatter.Deserialize(stream);
  }
  public static void Serialize(System.IO.Stream stream, IFormatter formatter)
  { formatter.Serialize(stream, namemap);
    formatter.Serialize(stream, names);
    formatter.Serialize(stream, namei);
  }

  static System.Collections.Hashtable namemap = new System.Collections.Hashtable();
  static string[] names = new string[] { "READ ME", "XGOCL APLFLCH", "DROWSSAP", "EUREKA!" };
  static int namei;
}
#endregion

[Serializable]
public class TeleportScroll : Scroll
{ public TeleportScroll() { name="teleport"; Color=Color.White; Spell=TeleportSpell.Default; }
  public TeleportScroll(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

[Serializable]
public class IdentifyScroll : Scroll
{ public IdentifyScroll()
  { name="identify"; Color=Color.White; Spell=IdentifySpell.Default; Prompt="Identify which item?";
  }
  public IdentifyScroll(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override void Read(Entity user)
  { if(Cursed && Global.Coinflip()) App.IO.Print("Nothing seems to happen.");
    else
    { if(Spell.AutoIdentify && user==App.Player && !user.KnowsAbout(this))
      { user.AddKnowledge(this);
        App.IO.Print("This is {0}.", GetAName(user));
      }
      if(!Blessed || Global.Rand(100)<80)
      { int n = Blessed ? Global.Rand(3) + 2 : 1;
        while(n-->0) Cast(user);
      }
      else foreach(Item i in user.Inv) if(!i.Identified) Spell.Cast(user, i);
    }
  }
}

} // namespace Chrono