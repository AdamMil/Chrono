using System;

namespace Chrono
{

public enum WeaponClass
{ Dagger, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown, NumClasses
}

public abstract class Weapon : Wieldable
{ public Weapon() { Class=ItemClass.Weapon; }

  public abstract int CalculateDamage(Entity user);

  public WeaponClass wClass;
  public int Delay; // delay (percentage of speed)
  public int Noise; // noise this weapon makes (0-10)
  public int ToHitBonus;
}

public class CueStick : Weapon
{ public CueStick()
  { name="cue stick"; Weight=5; Delay=10; Color=Color.Brown; Noise=4;
    wClass=WeaponClass.Staff; Mods[(int)Attr.EV]=1; AllHandWield=true;
    ShortDesc="A long, tapered staff with a white tip.";
    LongDesc="You don't quite remember how you happened upon this cue stick. The last thing you remember, you were in a bar drinking with some buddies...";
  }
  
  public override int CalculateDamage(Entity user) { return Global.NdN(1, 5) + user.StrBonus; }
}

public class ShortSword : Weapon
{ public ShortSword()
  { name="short sword"; Color=Color.Purple; Weight=35; Delay=5;
    wClass=WeaponClass.ShortBlade; Exercises=Attr.Str; Noise=5;
  }

  public override int CalculateDamage(Entity user) { return Global.NdN(1, 6) + user.StrBonus; }
}

public class TwigOfDeath : Weapon
{ public TwigOfDeath()
  { name="twig of death"; Color=Color.LightCyan; Weight=2; Delay=100;
    wClass=WeaponClass.Dagger; Exercises=Attr.Dex; Noise=1; ToHitBonus=-4;
  }
  
  public override bool Invoke(Entity user)
  { bool success = Global.Coinflip();
    if(user==App.Player)
      App.IO.Print(success ? "The stick pitches downward sharply, as you sense the presence of nearby water."
                           : "You hold the stick steady, but nothing happens.");
    return success;
  }

  public override bool Think(Entity holder)
  { base.Think(holder);
    if(holder!=null && Age%30==0)
    { App.IO.Print("Your twig is making a run for it!");
      holder.Interrupt();
      if(holder.Equipped(this)) holder.Unequip(this);
      holder.Drop(this);
    }
    return false;
  }

  public override int CalculateDamage(Entity user) { return Global.NdN(8, 8) - user.StrBonus; }
}

} // namespace Chrono