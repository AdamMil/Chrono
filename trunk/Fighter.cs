using System;
using System.Drawing;

namespace Chrono
{

public abstract class Fighter : AI
{ public override void Generate(int level, EntityClass myClass)
  { if(myClass==EntityClass.RandomClass) // choose random fighting class
    { myClass = EntityClass.Fighter;
    }
    base.Generate(level, myClass);
  }

  public override void OnNoise(Entity source, Noise type, int volume)
  { if(!HasEars) return;
    if(!alerted) switch(type)
    { case Noise.Alert: case Noise.NeedHelp: if(volume>Map.MaxSound*8/100) alerted=true; break;
      case Noise.Bang:  case Noise.Combat:   if(volume>Map.MaxSound*10/100) alerted=true; break;
      case Noise.Walking: if(volume>Map.MaxSound*15/100) alerted=true; break;
    }
    if(alerted)
    { for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(t.Sound>maxNoise) { maxNoise=t.Sound; noiseDir=(Direction)i; }
      }
    }
  }

  public override void Think()
  { base.Think();
    Direction dir = HasEyes ? LookAt(App.Player) : Direction.Invalid; // first try vision

    bool saw = dir!=Direction.Invalid;
    if(saw)
    { if(alerted) tries=0;
      if(!shouted) { Map.MakeNoise(Position, this, Noise.Alert, 150); shouted=true; }
    }
    else if(noiseDir!=Direction.Invalid) { tries=0; dir=noiseDir; } // then try sound

    if(dir==Direction.Invalid && HasNose && lastDir==Direction.Invalid) // then try scent
    { int maxScent=0;
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(Map.IsPassable(t.Type) && t.Scent>maxScent) { maxScent=t.Scent; dir=(Direction)i; }
      }
      if(!alerted && maxScent<(Map.MaxScent/20)) dir=Direction.Invalid; // ignore light scents (<5%) when not alerted
    }

    if(dir==Direction.Invalid) dir = lastDir; // then try memory

    if(dir!=Direction.Invalid)
    { bool giveup = false;
      lastDir = dir;
      if(!alerted) { alerted=true; goto done; } // take a turn to wake up
      for(int i=-1; i<=1; i++) // if player is in front, attack
      { Point pp = Global.Move(Position, dir+i);
        if(Map.GetEntity(pp)==App.Player) { Attack(App.Player); lastDir=dir; tries=0; goto done; }
      }
      Point np = Global.Move(Position, dir);
      if(tries>5) giveup=true; // if we've tried walking 5 tiles without detecting anything, give up
      else if(TryMove(np)) goto done; // otherwise, try moving forward
      else if(TryMove(dir-1)) { lastDir--; goto done; }
      else if(TryMove(dir+1)) { lastDir++; goto done; }
      if(giveup) { tries=0; lastDir=Direction.Invalid; alerted=shouted=false; } // else give up, reset state
      else if(!saw && noiseDir==Direction.Invalid) tries++; // we're not giving up yet.. keep searching
    }
    done:
    noiseDir=Direction.Invalid; maxNoise=0;
  }

  Direction lastDir=Direction.Invalid, noiseDir=Direction.Invalid;
  int tries=3;
  byte maxNoise;
  bool alerted, shouted;
}

public class Orc : Fighter
{ public Orc() { Race=Race.Orc; Color=Color.Yellow; baseKillExp=10; }
}

} // namespace Chrono