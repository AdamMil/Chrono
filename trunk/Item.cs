using System;
using System.Drawing;

namespace Chrono
{

public enum ItemClass
{ Invalid=-1, Any=-1,
  Gold, Amulet, Weapon, Shield, Armor, Ammo, Food, Corpse, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container,
  Treasure, NumClasses
}
[Flags] public enum ItemUse : byte { NoUse=0, Self=1, UseTarget=2, UseDirection=4, UseBoth=UseTarget|UseDirection };

[Flags] public enum ItemStatus : byte { Identified=1, Burnt=2, Rotted=4, Rusted=8, Cursed=16, Blessed=32 };

#region Item
public abstract class Item : ICloneable
{ public Item()
  { Prefix="a "; PluralPrefix=string.Empty; PluralSuffix="s"; Count=1; Color=Color.White; Durability=-1;
  }
  protected Item(Item item)
  { name=item.name; Title=item.Title; Class=item.Class;
    Prefix=item.Prefix; PluralPrefix=item.PluralPrefix; PluralSuffix=item.PluralSuffix;
    Age=item.Age; Count=item.Count; Weight=item.Weight; Color=item.Color; Char=item.Char;
    Usability=item.Usability; Durability=item.Durability; Status=item.Status;
  }

  public virtual bool Think(Entity holder) { Age++; return false; }

  public string AreIs { get { return Count>1 ? "are" : "is"; } }

  public virtual string FullName
  { get
    { string ret = Count>1 ? Count+" "+PluralPrefix+Name+PluralSuffix : Prefix+Name;
      if(Title!=null) ret += " named "+Title;
      return ret;
    }
  }
  public virtual string Name { get { return name; } }

  public string ItOne { get { return Count>1 ? "one" : "it"; } }
  public string ItThem { get { return Count>1 ? "them" : "it"; } }

  #region ICloneable Members
  public virtual object Clone() { return GetType().GetConstructor(copyCons).Invoke(new object[] { this }); }
  #endregion

  public virtual bool CanStackWith(Item item)
  { if(item.Title!=null || Title!=null || item.GetType() != GetType()) return false;
    if(Class==ItemClass.Food || Class==ItemClass.Potion || Class==ItemClass.Scroll || Class==ItemClass.Ammo ||
       Class==ItemClass.Weapon && Class==ItemClass.Treasure)
      return true;
    return false;
  }

  public virtual string GetFullName(Entity e) { return FullName; }
  public virtual string GetInvName(Entity e) { return GetFullName(e); }

  // item is thrown at or bashed on an entity. returns true if item should be destroyed
  public virtual bool Hit(Entity user, Point pos) { return false; }
  public virtual bool Hit(Entity user, Entity hit) { return false; }

  public virtual bool Invoke(Entity user)
  { if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return false;
  }

  public virtual bool Use(Entity user, Direction dir) // returns true if item should be consumed
  { if((Usability&ItemUse.UseDirection)==0 && (int)dir<8) return Use(user, Global.Move(user.Position, dir));
    if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return false;
  }
  public virtual bool Use(Entity user, System.Drawing.Point target) // returns true item should be consumed
  { if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return false;
  }

  public virtual void OnPickup(Entity holder) { }
  public virtual void OnDrop  (Entity holder) { }

  public Item Split(int toRemove)
  { if(toRemove>=Count) throw new ArgumentOutOfRangeException("toRemove", toRemove, "is >= than Count");
    Item newItem = (Item)Clone();
    newItem.Count = toRemove;
    Count -= toRemove;
    return newItem;
  }

  public override string ToString() { return FullName; }

  public string Title, Prefix, PluralPrefix, PluralSuffix, ShortDesc, LongDesc;
  public int Age, Count, Weight; // age in map ticks, number in the stack, weight (5 = approx. 1 pound)
  public int Durability;         // 0 - 100 (chance of breaking if thrown), or -1, which uses the default
  public ItemClass Class;
  public Color Color;
  public char Char;
  public ItemUse Usability;
  public ItemStatus Status;

  protected string name;

  static readonly Type[] copyCons = new Type[] { typeof(Item) };
}
#endregion

public abstract class Modifying : Item
{ protected Modifying() { }
  protected Modifying(Item item) : base(item)
  { Modifying mi = (Modifying)item; Mods=(int[])mi.Mods.Clone(); FlagMods=mi.FlagMods;
  }

  public override bool CanStackWith(Item item) { return false; }

  public int AC { get { return Mods[(int)Attr.AC]; } set { Mods[(int)Attr.AC]=value; } }
  public int Dex { get { return Mods[(int)Attr.Dex]; } set { Mods[(int)Attr.Dex]=value; } }
  public int EV { get { return Mods[(int)Attr.EV]; } set { Mods[(int)Attr.EV]=value; } }
  public int Int { get { return Mods[(int)Attr.Int]; } set { Mods[(int)Attr.Int]=value; } }
  public int MaxHP { get { return Mods[(int)Attr.MaxHP]; } set { Mods[(int)Attr.MaxHP]=value; } }
  public int MaxMP { get { return Mods[(int)Attr.MaxMP]; } set { Mods[(int)Attr.MaxMP]=value; } }
  public int Speed { get { return Mods[(int)Attr.Speed]; } set { Mods[(int)Attr.Speed]=value; } }
  public int Stealth { get { return Mods[(int)Attr.Stealth]; } set { Mods[(int)Attr.Stealth]=value; } }
  public int Str { get { return Mods[(int)Attr.Str]; } set { Mods[(int)Attr.Str]=value; } }

  public int  GetAttr(Attr attribute) { return Mods[(int)attribute]; }
  public void SetAttr(Attr attribute, int val) { Mods[(int)attribute]=val; }

  public bool GetFlag(Entity.Flag flag) { return (FlagMods&flag)!=0; }
  public void SetFlag(Entity.Flag flag, bool on) { if(on) FlagMods |= flag; else FlagMods &= ~flag; }

  public int[] Mods = new int[(int)Attr.NumModifiable];
  public Entity.Flag FlagMods;
}

public abstract class Wearable : Modifying
{ public Wearable() { Slot=Slot.Invalid; }

  public override string GetInvName(Entity e)
  { string ret = GetFullName(e);
    if(worn) ret += " (worn)";
    return ret;
  }

  public virtual void OnRemove(Entity equipper) { worn=false;  }
  public virtual void OnWear  (Entity equipper) { worn=true; }

  public Slot Slot;
  bool worn;
}

public abstract class Wieldable : Modifying
{ protected Wieldable() { }
  protected Wieldable(Item item) : base(item)
  { Wieldable wi = (Wieldable)item; Exercises=wi.Exercises; AllHandWield=wi.AllHandWield;
  }

  public override string GetInvName(Entity e)
  { string ret = GetFullName(e);
    if(equipped) ret += " (equipped)";
    return ret;
  }

  public virtual void OnEquip  (Entity equipper) { equipped=true;  }
  public virtual void OnUnequip(Entity equipper) { equipped=false; }

  public Attr Exercises;
  public bool AllHandWield;
  bool equipped;
}

public abstract class Readable : Item
{ protected Readable() { }
  protected Readable(Item item) : base(item) { }
}

public abstract class Chargeable : Item
{ public int Charges, Recharged;
}

public class Gold : Item
{ public Gold() { name="gold piece"; Color=Color.Yellow; Class=ItemClass.Gold; }
  public Gold(Item item) : base(item) { }
  
  public override bool CanStackWith(Item item) { return item.Class==ItemClass.Gold; }
}

} // namespace Chrono