using System;
using System.Xml;

namespace Chrono
{

public enum DamageType : byte
{
  Acid, Blind, Cold, Direct, DrainDex, DrainInt, DrainStr, Electricity, Heat, Magic, Paralyse, Petrify, Physical,
  Poison, Sicken, Slow, StealGold, StealItem, Stun, Teleport,
}

public enum WeaponClass
{
  Dagger, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown, NumClasses
}

#region Damage
public struct Damage
{
  public Damage(int physical) { Physical=physical; Heat=Cold=Electricity=Poison=numExtras=0; extras=null; }

  public struct Extra
  {
    public Extra(DamageType type, int amount) { Type=type; Amount=amount; }
    public DamageType Type;
    public int Amount;
  }

  public void AddExtra(DamageType type, int amount) { AddExtra(new Extra(type, amount)); }
  public void AddExtra(Extra extra)
  {
    if(extras==null || numExtras==extras.Length)
    {
      Extra[] narr = new Extra[numExtras==0 ? 4 : numExtras*2];
      if(numExtras!=0) extras.CopyTo(narr, 0);
      extras = narr;
    }
    extras[numExtras++] = extra;
  }

  public Extra[] GetExtras()
  {
    if(numExtras==extras.Length) return extras;
    Extra[] narr = new Extra[numExtras];
    Array.Copy(extras, narr, numExtras);
    return narr;
  }

  public int GetTotal(DamageType type)
  {
    int total = 0;
    for(int i=0; i<numExtras; i++) if(extras[i].Type==type) total += extras[i].Amount;

    switch(type)
    {
      case DamageType.Physical: total += Physical; break;
      case DamageType.Heat: total += Heat; break;
      case DamageType.Cold: total += Cold; break;
      case DamageType.Electricity: total += Electricity; break;
      case DamageType.Poison: total += Poison; break;
    }

    return total;
  }

  public int Physical, Heat, Cold, Electricity, Poison;

  Extra[] extras;
  int numExtras;
}
#endregion

#region Weapon
public abstract class Weapon : ItemClass
{
  protected Weapon() { Type=ItemType.Weapon; MainAttr=Attr.Str; ShowEnchantment=true; }

  public virtual void GetDamage(Item item, Item ammo, Entity user, ref Damage damage)
  {
    if(ammo!=null) ((Ammo)ammo.Class).ModDamage(ammo, ref damage);
    if(item.Damage!=0)
    {
      int phys=damage.GetTotal(DamageType.Physical);
      damage.Physical -= Math.Min(phys, item.Damage);
    }
  }

  public override string GetInvName(Item item) { return GetAName(item) + (item.IsEquipped ? " (equipped)" : null); }

  public virtual int GetRange(Item item, Entity user, bool thrown)
  {
    return thrown ? 10 : 1; // TODO: revise this
  }

  public virtual int GetToHit(Item item, Item ammo, Entity user)
  {
    return ammo==null ? 100 : ((Ammo)ammo.Class).ModToHit(ammo, 100); // TODO: revise taking user's skill into account
  }

  public Attr MainAttr;
  public WeaponClass Class;
  public int Delay;
  public bool Ranged;
}
#endregion

#region XmlWeapon
public sealed class XmlWeapon : Weapon
{
  public XmlWeapon(XmlNode node) { throw new NotImplementedException(); }
}
#endregion

#region FiringWeapon
public abstract class FiringWeapon : Weapon
{
  public abstract bool Uses(Ammo ammo);
}
#endregion

#region Ammo
public abstract class Ammo : ItemClass
{
  public Ammo() { Type=ItemType.Ammo; }

  public virtual void ModDamage(Item item, ref Damage damage) { }
  public virtual int ModToHit(Item item, int toHit) { return toHit; }
}
#endregion

#region Arrows
public abstract class Arrow : Ammo
{
  public Arrow() { weight=45; }

  // blessed arrows are 10% more likely to hit. cursed arrows are 10% less likely to hit
  public override int ModToHit(Item item, int toHit) { return toHit + item.BlessedSign*10; }
}

public sealed class BasicArrow : Arrow
{
  public BasicArrow() { name="arrow"; Price=8; SpawnChance=300; SpawnCount=new Range(4, 12); }
}

public sealed class FlamingArrow : Arrow
{
  public FlamingArrow() { name="flaming arrow"; Price=12; SpawnChance=100; SpawnCount=new Range(3, 8); }
  public override void ModDamage(Item item, ref Damage damage) { damage.Heat += Global.NdN(1, 4); }
}
#endregion

#region Darts
public class Dart : Weapon
{
  public Dart()
  {
    weight=28; Class=WeaponClass.Thrown; name="dart"; Price=8; SpawnChance=300; SpawnCount=new Range(3, 10);
  }

  public override void GetDamage(Item item, Item ammo, Entity user, ref Damage damage)
  {
    damage.Physical += Global.NdN(1, 4) + item.BlessedSign;
    base.GetDamage(item, ammo, user, ref damage);
  }
}

public sealed class PoisonDart : Dart
{
  public PoisonDart() { idName="poisoned dart"; Price=12; SpawnChance=150; SpawnCount=new Range(3, 8); }

  public override void GetDamage(Item item, Item ammo, Entity user, ref Damage damage)
  {
    base.GetDamage(item, ammo, user, ref damage);
    damage.Poison += item.Blessed && Global.Coinflip() ? 2 : 1;
  }
}
#endregion

#region Bow
public class Bow : FiringWeapon
{
  public Bow()
  {
    name="recurved bow"; NumHands=2; Price=225; Color=Color.LightCyan; weight=2250; Delay=25; Class=WeaponClass.Bow;
    Noise=2; SpawnChance=200; Material=Wood.Instance;
  }

  public override void GetDamage(Item item, Item ammo, Entity user, ref Damage damage)
  {
    damage.Physical += ammo==null ? Global.NdN(1, 3) : Global.NdN(2, 6);
    base.GetDamage(item, ammo, user, ref damage);
  }

  public override bool Uses(Ammo ammo) { return ammo is Arrow; }
}
#endregion

#region ShortSword
public sealed class ShortSword : Weapon
{
  public ShortSword()
  {
    name="short sword"; Color=Color.Purple; weight=4500; Delay=5; Class=WeaponClass.ShortBlade; Noise=5; Price=130;
    SpawnChance=400; Material=Metal.Instance;
  }

  public override void GetDamage(Item item, Item ammo, Entity user, ref Damage damage)
  {
    damage.Physical += Global.NdN(1, 6);
    base.GetDamage(item, ammo, user, ref damage);
  }
}
#endregion

} // namespace Chrono