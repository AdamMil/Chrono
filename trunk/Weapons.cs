using System;

namespace Chrono
{

public enum Compatibility { None=-2, Poor=-1, Okay=0, Perfect=1 };

public enum WeaponClass
{ Dagger, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown, NumClasses
}

public abstract class Weapon : Wieldable
{ public Weapon() { Class=ItemClass.Weapon; }
  protected Weapon(Item item) : base(item)
  { Weapon ow = (Weapon)item;
    wClass=ow.wClass; Delay=ow.Delay; Noise=ow.Noise; ToHitBonus=ow.ToHitBonus; Ranged=ow.Ranged;
  }

  public abstract int CalculateDamage(Entity user, Ammo ammo, Entity target); // ammo and target can be null

  public override bool CanStackWith(Item item)
  { return wClass==WeaponClass.Thrown && item.Title==null && Title==null && item.GetType()==GetType() &&
           ((Weapon)item).wClass==wClass;
  }
  
  public virtual Compatibility CompatibleWith(Entity user) { return Compatibility.Okay; }

  public WeaponClass wClass;
  public int  Delay;  // delay (percentage of speed)
  public int  Noise;  // noise this weapon makes (0-10)
  public int  ToHitBonus;
  public bool Ranged;
}

public abstract class FiringWeapon : Weapon
{ public FiringWeapon() { Ranged=true; }
  public abstract Compatibility CompatibleWith(Ammo ammo);
  
  public string AmmoName;
}

public abstract class Ammo : Item
{ public Ammo() { Class=ItemClass.Ammo; }
  public Ammo(Item item) : base(item) { }
}

public abstract class Arrow : Ammo
{ public Arrow() { Weight=3; }
  public Arrow(Item item) : base(item) { }
}

public class GoodArrow : Arrow
{ public GoodArrow() { name="good arrow"; }
  public GoodArrow(Item item) : base(item) { }
}

public class BadArrow : Arrow
{ public BadArrow() { name="bad arrow"; }
  public BadArrow(Item item) : base(item) { }
}

public class Dart : Weapon
{ public Dart() { wClass=WeaponClass.Thrown; Ranged=true; name="poisoned dart"; Weight=2; }
  public Dart(Item item) : base(item) { }

  public override bool Hit(Entity user, Entity hit)
  { if(Global.Rand(100)<100/(hit.Poison+1)) hit.AddEffect(new Effect(user, Attr.Poison, 1, -1));
    return false;
  }

  public override int CalculateDamage(Entity user, Ammo ammo, Entity target)
  { return Global.NdN(1, 3+(int)CompatibleWith(user)) + user.StrBonus/2;
  }
}

public class Bow : FiringWeapon
{ public Bow()
  { name="recurved bow"; AmmoName="arrows";
    Color=Color.LightCyan; Weight=20; Delay=25; wClass=WeaponClass.Bow; Exercises=Attr.Dex; Noise=2;
  }

  public override Compatibility CompatibleWith(Ammo ammo)
  { return ammo is GoodArrow ? Compatibility.Perfect : ammo is Arrow ? Compatibility.Okay : Compatibility.None;
  }

  public override int CalculateDamage(Entity user, Ammo ammo, Entity target)
  { return ammo==null ? 1 : Global.NdN(1, 5+(int)CompatibleWith(user)+(int)CompatibleWith(ammo)) + user.DexBonus;
  }
}

public class ShortSword : Weapon
{ public ShortSword()
  { name="short sword"; Color=Color.Purple; Weight=35; Delay=5;
    wClass=WeaponClass.ShortBlade; Exercises=Attr.Str; Noise=5;
  }

  public override int CalculateDamage(Entity user, Ammo ammo, Entity target)
  { return Global.NdN(1, 6+(int)CompatibleWith(user)) + user.StrBonus;
  }
}

} // namespace Chrono