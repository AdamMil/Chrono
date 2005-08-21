using System;

namespace Chrono
{

public enum Compatibility { None=-2, Poor=-1, Okay=0, Perfect=1 };

public enum WeaponClass
{ Dagger, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown, NumClasses
}

#region Weapon, FiringWeapon, Ammo
public abstract class Weapon : Wieldable
{ public Weapon() { Class=ItemClass.Weapon; }

  public int DamageBonus { get { return BaseDamage+DamageMod; } }
  public int ToHitBonus  { get { return BaseToHit+ToHitMod; } }

  public abstract Damage CalculateDamage(Entity user, Ammo ammo, Entity target); // ammo and target can be null

  public override bool CanStackWith(Item item)
  { return Status==item.Status && wClass==WeaponClass.Thrown && item.Title==null && Title==null &&
           item.GetType()==GetType() && ((Weapon)item).wClass==wClass;
  }

  public virtual Compatibility CompatibleWith(Entity user) { return Compatibility.Okay; }

  public override string GetFullName(bool forceSingular)
  { if(!Identified) return base.GetFullName(forceSingular);
    string status = StatusString;
    if(status!="") status += ' ';
    string ret = (!forceSingular && Count>1 ? Count.ToString()+' ' : "") + status +
                  (DamageMod<0 ? "" : "+") + DamageMod + ',' +
                  (ToHitMod <0 ? "" : "+") + ToHitMod  + ' ' + Name;
    if(Count>1) ret += PluralSuffix;
    if(Title!=null) ret += " named "+Title;
    return ret;
  }

  public WeaponClass wClass;
  public int  Delay;  // delay (percentage of speed)
  public int  BaseDamage, BaseToHit; // base bonuses
  public int  DamageMod, ToHitMod;   // modifiers to the base bonuses
  public bool Ranged;
}

public abstract class FiringWeapon : Weapon
{ public FiringWeapon() { Ranged=true; }
  public abstract Compatibility CompatibleWith(Ammo ammo);
  
  public string AmmoName;
}

[NoClone]
public abstract class Ammo : Item
{ public Ammo() { Class=ItemClass.Ammo; }
  
  public virtual Damage ModDamage(Damage damage) { return damage; }
}
#endregion

#region Arrows
public abstract class Arrow : Ammo
{ public Arrow() { Weight=3; }
}

public class BasicArrow : Arrow
{ public BasicArrow() { name="arrow"; ShopValue=8; }
  
  public static readonly int SpawnChance=300, SpawnMin=4, SpawnMax=12; // 3% chance, 4-12 arrows
}

public class FlamingArrow : Arrow
{ public FlamingArrow() { name="flaming arrow"; ShopValue=12; }

  public override Damage ModDamage(Damage damage)
  { damage.Heat += (ushort)Global.NdN(1, 4);
    return damage;
  }

  public static readonly int SpawnChance=300, SpawnMin=3, SpawnMax=8; // 1% chance, 3-8 arrows
}
#endregion

public class PoisonDart : Weapon
{ public PoisonDart() { wClass=WeaponClass.Thrown; Ranged=true; name="poisoned dart"; Weight=2; ShopValue=12; }

  public override Damage CalculateDamage(Entity user, Ammo ammo, Entity target)
  { Damage d = new Damage(Global.NdN(1, 3+(int)CompatibleWith(user)) + user.StrBonus/3 + DamageBonus);
    if(target==null || Global.Rand(100)<100/(target.Poison+1)) d.Poison = 1;
    return d;
  }

  public static readonly int SpawnChance=300; // 3% chance
  public static readonly int SpawnMin=3, SpawnMax=10;
}

public class Bow : FiringWeapon
{ public Bow()
  { name="recurved bow"; AmmoName="arrows"; AllHandWield=true; ShopValue=225;
    Color=Color.LightCyan; Weight=20; Delay=25; wClass=WeaponClass.Bow; Exercises=Attr.Dex; Noise=2;
  }

  public override Compatibility CompatibleWith(Ammo ammo)
  { return ammo is Arrow ? Compatibility.Okay : Compatibility.None;
  }

  public override Damage CalculateDamage(Entity user, Ammo ammo, Entity target)
  { if(ammo==null) return new Damage(1 + Math.Max(0, user.StrBonus/5));
    Damage dam = new Damage(ammo==null ? 1 : Global.NdN(1, 5+(int)CompatibleWith(user)+(int)CompatibleWith(ammo))
                            + user.DexBonus + DamageBonus);
    return ammo.ModDamage(dam);
  }

  public static readonly int SpawnChance=200; // 2% chance
}

public class ShortSword : Weapon
{ public ShortSword()
  { name="short sword"; Color=Color.Purple; Weight=35; Delay=5;
    wClass=WeaponClass.ShortBlade; Exercises=Attr.Str; Noise=5; ShopValue=130;
  }

  public override Damage CalculateDamage(Entity user, Ammo ammo, Entity target)
  { return new Damage(Global.NdN(1, 6+(int)CompatibleWith(user)) + user.StrBonus + DamageBonus);
  }

  public static readonly int SpawnChance=400; // 4% chance
}

} // namespace Chrono