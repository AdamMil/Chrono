using System;
using System.Drawing;

namespace Chrono
{

public abstract class Fighter : AI
{ public override void Generate(int level, CreatureClass myClass)
  { if(myClass==CreatureClass.RandomClass) // choose random fighting class
    { myClass = CreatureClass.Fighter;
    }
    base.Generate(level, myClass);
  }

  public override void Think()
  { base.Think();
    Direction dir = CanSee(App.Player);
    if(dir==Direction.Invalid)
    { int maxScent=0;
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(Map.IsPassable(t.Type) && t.Scent>maxScent) { maxScent=t.Scent; dir=(Direction)i; }
      }
    }
    if(dir!=Direction.Invalid)
    { Point np = Global.Move(Position, dir);
      if(Map.GetCreature(np)==App.Player) Attack(App.Player);
      else Position = np;
    }
  }
}

public class Orc : Fighter
{ public Orc() { Race=Race.Orc; Color=Color.Yellow; baseKillExp=10; }
}

} // namespace Chrono