using System;

namespace Chrono
{

public enum WeaponClass
{ Dagger, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown
}

public abstract class Weapon : Wieldable
{ public Weapon() { Class=ItemClass.Weapon; }

  public virtual int ToHitBonus { get { return 0; } }

  public abstract int CalculateDamage(Entity user);

  public WeaponClass wClass;
  public int Delay; // delay (percentage of speed)
}

public class CueStick : Weapon
{ public CueStick()
  { name="cue stick"; Weight=5; Delay=10; Color=Color.Brown;
    wClass=WeaponClass.Staff; Mods[(int)Attr.EV]=1; AllHandWield=true;
    ShortDesc="A long, tapered staff with a white tip.";
    LongDesc="You don't quite remember how you happened upon this cue stick. The last thing you remember, you were in a bar drinking with some buddies...";
  }
  
  public override int CalculateDamage(Entity user) { return Global.NdN(1, 5) + user.StrBonus; }
}

public class TwigOfDeath : Weapon
{ public TwigOfDeath()
  { name="twig of death"; Color=Color.LightCyan; Weight=2; Delay=100;
    wClass=WeaponClass.Dagger; Exercises=Attr.Dex;
  }
  
  public override int ToHitBonus { get { return -4; } }

  public override void Invoke(Entity user)
  { if(user==App.Player)
      if(Global.Coinflip()) App.IO.Print("You hold the stick steady, but nothing happens.");
      else App.IO.Print("The stick pitches downward sharply, as you sense the presence of nearby water.");
  }

  public override void Think(Entity holder)
  { base.Think(holder);
    if(holder!=null && Age%30==0)
    { App.IO.Print("Your twig is making a run for it!");
      holder.Interrupt();
      if(holder.Equipped(this)) holder.Unequip(this);
      holder.Drop(this);
    }
  }

  public override int CalculateDamage(Entity user) { return Global.NdN(8, 8) - user.StrBonus; }
}

} // namespace Chrono