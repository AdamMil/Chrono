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
    Direction dir = LookAt(App.Player);
    bool saw = dir!=Direction.Invalid;
    if(saw && alerted) tries=0;
    if(lastDir==Direction.Invalid && dir==Direction.Invalid)
    { int maxScent=0;
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(Map.IsPassable(t.Type) && t.Scent>maxScent) { maxScent=t.Scent; dir=(Direction)i; }
      }
    }
    if(dir==Direction.Invalid) dir = lastDir;
    if(dir!=Direction.Invalid)
    { bool giveup=false;
      lastDir = dir;
      if(!alerted)
      { alerted=true;
        if(!shouted) { App.IO.Print("You hear a shout!"); shouted=true; }
        return;
      }
      Point np = Global.Move(Position, dir);
      if(Map.GetCreature(np)==App.Player) Attack(App.Player);
      else
      
      if(tries>3) giveup=true;
      else if(TryMove(np)) return;
      else if(TryMove(dir-1)) lastDir--;
      else if(TryMove(dir+1)) lastDir++;
      if(giveup) { tries=0; lastDir=Direction.Invalid; alerted=false; }
      else if(!saw) tries++;
    }
  }
  
  Direction lastDir=Direction.Invalid;
  int tries=3;
  bool alerted, shouted;
}

public class Orc : Fighter
{ public Orc() { Race=Race.Orc; Color=Color.Yellow; baseKillExp=10; }
}

} // namespace Chrono