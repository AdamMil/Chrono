using System;

namespace Chrono
{

public enum WeaponClass
{ ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown
}

public class Weapon : Wieldable
{ public Weapon() { Class=ItemClass.Weapon; }

  public WeaponClass wClass;
}

public class CueStick : Weapon
{ public CueStick()
  { Name="cue stick"; Color=Color.White; Weight=5;
    wClass=WeaponClass.Staff;
  }
}

} // namespace Chrono