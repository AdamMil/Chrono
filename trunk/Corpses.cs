using System;

namespace Chrono
{

public class Corpse : Item
{ public Corpse(Entity of)
  { Class = ItemClass.Corpse; Color = of.Color; Weight=raceWeight[(int)of.Race];
    name  = of.Race.ToString().ToLower() + " corpse"; Prefix = Global.AorAn(name)+' ';
    CorpseOf = of;
    if(of.Sickness>1) Tainted=true;
  }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(Age==100) { Skeleton=true; name=CorpseOf.Race.ToString().ToLower()+" skeleton"; }
    else if(Age==200)
    { if(holder==App.Player) App.IO.Print("Your {0} rots away.", Name);
      return true;
    }
    return false;
  }

  public Entity CorpseOf;
  public int CarveTurns; // number of turns spent carving this corpse so far
  public bool Skeleton, Tainted;
  
  static readonly int[] raceWeight = new int[(int)Race.NumRaces]
  { 750, 1000
  };
}

} // namespace Chrono