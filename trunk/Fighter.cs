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

  public override void OnHitBy(Entity attacker, object item, Damage damage)
  { base.OnHitBy(attacker, item, damage);
    Alerted = true; Shout();
  }

  public override void OnMissBy(Entity attacker, object item)
  { base.OnMissBy(attacker, item);
    if(Global.Coinflip()) { Alerted = true; Shout(); }
  }

  public override void OnNoise(Entity source, Noise type, int volume)
  { if(!HasEars) return;
    if(!Alerted) switch(type)
    { case Noise.Alert: case Noise.NeedHelp: if(volume>Map.MaxSound*8/100)  Alerted=true; break;
      case Noise.Bang:  case Noise.Combat:   if(volume>Map.MaxSound*10/100) Alerted=true; break;
      case Noise.Walking: if(volume>Map.MaxSound*15/100) Alerted=true; break;
    }
    if(Alerted)
    { for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(t.Sound>maxNoise) { maxNoise=t.Sound; noiseDir=(Direction)i; }
      }
    }
  }

  public override void Think()
  { base.Think();
    if(HP<=0) return;
    bool dontign = Alerted || Global.Rand(100)>=App.Player.Stealth*10-3;
    Direction dir = dontign && HasEyes ? LookAt(App.Player) : Direction.Invalid; // first try vision

    bool saw = dir!=Direction.Invalid;
    if(saw) 
    { if(Alerted) tries=0;
      Shout();
    }
    // then try sound (not affected by stealth because sound is already dampened by stealth)
    else if(noiseDir!=Direction.Invalid) { tries=0; dir=noiseDir; }

    if(dir==Direction.Invalid && HasNose && lastDir==Direction.Invalid) // then try scent (not dampened by stealth)
    { int maxScent=0;
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(Map.IsPassable(t.Type) && t.Scent>maxScent) { maxScent=t.Scent; dir=(Direction)i; }
      }
      if(!Alerted && maxScent<(Map.MaxScent/10)) dir=Direction.Invalid; // ignore light scents (<10%) when not Alerted
    }

    if(dir==Direction.Invalid) dir = lastDir; // then try memory

    if(dir!=Direction.Invalid)
    { bool giveup=false, usedNoise = noiseDir!=Direction.Invalid;
      Direction pdir=lastDir; // pdir==lastDir, before lastDir is changed on the next line
      lastDir=dir; // lastDir is no longer the last direction. now it's the new one
      if(!Alerted) { Alerted=true; goto done; } // take a turn to wake up
      for(int i=-1; i<=1; i++) // if player is in front, attack
        if(Map.GetEntity(Global.Move(Position, dir+i))==App.Player)
        { if(!saw || pdir==dir+i || Global.Rand(100)<(usedNoise?75:50)) // if we can't see the target, and it moved,
          { Attack(null, null, lastDir=dir+i);                          // we have a 75% chance of finding it if we
          }                                                             // sensed it with sound and 50% otherwise
          else if(!saw && pdir!=dir+i && Map.GetEntity(Global.Move(Position, pdir))==null)
          { Attack(null, null, pdir); // otherwise we attack where we thought it was
            App.IO.Print(TheName+" attacks empty space."); 
          }
          tries=0;
          goto done;
        }
      Point np = Global.Move(Position, dir);
      if(tries>5) giveup=true; // if we've tried walking 5 tiles without detecting anything, give up
      else if(TryMove(np)) goto done; // otherwise, try moving forward
      else if(TryMove(dir-1)) { lastDir--; goto done; }
      else if(TryMove(dir+1)) { lastDir++; goto done; }
      if(giveup) { tries=0; lastDir=Direction.Invalid; Alerted=shouted=false; } // else give up, reset state
      else if(!saw && noiseDir==Direction.Invalid) tries++; // we're not giving up yet.. keep searching
    }
    done:
    noiseDir=Direction.Invalid; maxNoise=0;
  }

  bool Alerted { get { return state==AIState.Alerted; } set { state = value ? AIState.Alerted : AIState.Idle; } }
  void Shout() { if(!shouted) { Map.MakeNoise(Position, this, Noise.Alert, 150); shouted=true; } }

  Direction lastDir=Direction.Invalid, noiseDir=Direction.Invalid;
  int tries=3;
  byte maxNoise;
  bool shouted;
}

public class Orc : Fighter
{ public Orc() { Race=Race.Orc; Color=Color.Yellow; baseKillExp=10; }
}

} // namespace Chrono
