using System;

namespace Chrono
{

public class Corpse : Item
{ public Corpse(Entity of)
  { Class = ItemClass.Corpse; Color = of.Color; Weight=raceWeight[(int)of.Race];
    name  = of.Race.ToString().ToLower() + " corpse"; Prefix = Global.AorAn(name)+' ';
    CorpseOf = of;
  }

  public Entity CorpseOf;
  
  static readonly int[] raceWeight = new int[(int)Race.NumRaces]
  { 750, 1000
  };
}

} // namespace Chrono