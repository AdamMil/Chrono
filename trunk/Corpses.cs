using System;
using System.Runtime.Serialization;

namespace Chrono
{

[Serializable]
public class Corpse : Item
{ [Flags] public enum Flag { Rotting=1, Skeleton=2, Tainted=4 };

  public Corpse(Entity of)
  { Class = ItemClass.Corpse; Color = of.Color; Weight=raceWeight[(int)of.Race];
    name  = of.Race.ToString().ToLower() + " corpse"; Prefix = Global.AorAn(name)+' ';
    CorpseOf = of;
    if(of.Sickness+of.Poison>1) Flags |= Flag.Tainted;
  }
  public Corpse(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(Age==75)
    { Flags |= Flag.Rotting;
      name="rotting "+CorpseOf.Race.ToString().ToLower()+" corpse"; Prefix="a ";
      if(holder==App.Player) App.IO.Print(Global.Coinflip() ? "Eww! There's something really disgusting in your pack!"
                                                            : "You smell the putrid stench of decay.");
    }
    else if(Age==150)
    { Flags = Flags & ~Flag.Rotting | Flag.Skeleton;
      name=CorpseOf.Race.ToString().ToLower()+" skeleton"; Prefix = Global.AorAn(name)+' ';
    }
    else if(Age==200)
    { if(holder==App.Player) App.IO.Print("Your {0} rots away.", Name);
      return true;
    }
    return false;
  }

  public Entity CorpseOf;
  public int CarveTurns; // number of turns spent carving this corpse so far
  public Flag Flags;
  
  static readonly int[] raceWeight = new int[(int)Race.NumRaces]
  { 750, 1000
  };
}

} // namespace Chrono