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
    wClass=ow.wClass; Delay=ow.Delay; Noise=ow.Noise; ToHitBonus=ow.ToHitBonus; baseToHit=ow.baseToHit;
    baseDamage=ow.baseDamage; damageBonus=ow.damageBonus; Ranged=ow.Ranged;
  }

  public override string FullName
  { get
    { if(!Identified) return base.FullName;
      string status = StatusString;
      if(status!="") status += ' ';
      string ret = (Count>1 ? Count.ToString()+' ' : "") + status +
                   (damageBonus<baseDamage ? "" : "+") + (damageBonus-baseDamage) + ',' +
                   (ToHitBonus<baseToHit ? "" : "+") + (ToHitBonus-baseToHit) + ' ' + Name;
      if(Count>1) ret += PluralSuffix;
      if(Title!=null) ret += " named "+Title;
      return Count>1 ? ret : Global.AorAn(ret) + ' ' + ret;
    }
  }

  public abstract Damage CalculateDamage(Entity user, Ammo ammo, Entity target); // ammo and target can be null

  public override bool CanStackWith(Item item)
  { return Status==item.Status && wClass==WeaponClass.Thrown && item.Title==null && Title==null &&
           item.GetType()==GetType() && ((Weapon)item).wClass==wClass;
  }

  public virtual Compatibility CompatibleWith(Entity user) { return Compatibility.Okay; }

  public WeaponClass wClass;
  public int  Delay;  // delay (percentage of speed)
  public int  Noise;  // noise this weapon makes (0-10)
  public int  ToHitBonus;
  public bool Ranged;
  
  protected int baseToHit, baseDamage, damageBonus; // base bonuses
}

public abstract class FiringWeapon : Weapon
{ public FiringWeapon() { Ranged=true; }
  public abstract Compatibility CompatibleWith(Ammo ammo);
  
  public string AmmoName;
}

public abstract class Ammo : Item
{ public Ammo() { Class=ItemClass.Ammo; }
  public Ammo(Item item) : base(item) { }
  
  public virtual Damage ModDamage(Damage damage) { return damage; }
}

public abstract class Arrow : Ammo
{ public Arrow() { Weight=3; }
  public Arrow(Item item) : base(item) { }
}

public class BasicArrow : Arrow
{ public BasicArrow() { name="arrow"; }
  public BasicArrow(Item item) : base(item) { }
}

public class FlamingArrow : Arrow
{ public FlamingArrow() { name="flaming arrow"; }
  public FlamingArrow(Item item) : base(item) { }
  
  public override Damage ModDamage(Damage damage)
  { damage.Heat += (ushort)Global.NdN(1, 4);
    return damage;
  }
}

public class Dart : Weapon
{ public Dart() { wClass=WeaponClass.Thrown; Ranged=true; name="poisoned dart"; Weight=2; }
  public Dart(Item item) : base(item) { }

  public override Damage CalculateDamage(Entity user, Ammo ammo, Entity target)
  { Damage d = new Damage(Global.NdN(1, 3+(int)CompatibleWith(user)) + user.StrBonus/2 + damageBonus);
    if(Global.Rand(100)<100/(target.Poison+1)) d.Poison = 1;
    return d;
  }
}

public class Bow : FiringWeapon
{ public Bow()
  { name="recurved bow"; AmmoName="arrows";
    Color=Color.LightCyan; Weight=20; Delay=25; wClass=WeaponClass.Bow; Exercises=Attr.Dex; Noise=2;
  }

  public override Compatibility CompatibleWith(Ammo ammo)
  { return ammo is Arrow ? Compatibility.Okay : Compatibility.None;
  }

  public override Damage CalculateDamage(Entity user, Ammo ammo, Entity target)
  { Damage dam = new Damage(ammo==null ? 1 : Global.NdN(1, 5+(int)CompatibleWith(user)+(int)CompatibleWith(ammo))
                            + user.DexBonus + damageBonus);
    return ammo.ModDamage(dam);
  }
}

public class ShortSword : Weapon
{ public ShortSword()
  { name="short sword"; Color=Color.Purple; Weight=35; Delay=5;
    wClass=WeaponClass.ShortBlade; Exercises=Attr.Str; Noise=5;
  }

  public override Damage CalculateDamage(Entity user, Ammo ammo, Entity target)
  { return new Damage(Global.NdN(1, 6+(int)CompatibleWith(user)) + user.StrBonus + damageBonus);
  }
}

} // namespace Chrono