using System;

namespace Chrono
{

public enum ItemClass
{ Invalid=-1, Any=-1,
  Amulet, Weapon, Armor, Ammo, Food, Corpse, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container, Treasure,
  NumClasses
}

#region Item
public abstract class Item : ICloneable
{ public Item()
  { Prefix="a "; PluralPrefix=string.Empty; PluralSuffix="s"; Count=1; Color=Color.White;
  }
  public Item(Item item)
  { name=item.name; Title=item.Title; Class=item.Class;
    Prefix=item.Prefix; PluralPrefix=item.PluralPrefix; PluralSuffix=item.PluralSuffix;
    Age=item.Age; Count=item.Count; Weight=item.Weight; Color=item.Color; Char=item.Char;
  }

  public virtual void Think(Entity holder) { Age++; }

  public string AreIs { get { return Count>1 ? "are" : "is"; } }

  public virtual string FullName
  { get
    { string ret = Count>1 ? Count+" "+PluralPrefix+Name+PluralSuffix : Prefix+Name;
      if(Title!=null) ret += " named "+Title;
      return ret;
    }
  }
  public virtual string InvName { get { return FullName; } }
  public virtual string Name { get { return name; } }

  public string ItOne { get { return Count>1 ? "one" : "it"; } }
  public string ItThem { get { return Count>1 ? "them" : "it"; } }

  #region ICloneable Members
  public virtual object Clone() { return GetType().GetConstructor(copyCons).Invoke(new object[] { this }); }
  #endregion

  public virtual bool CanStackWith(Item item)
  { if(item.Title!=null || Title!=null || item.GetType() != GetType()) return false;
    if(Class==ItemClass.Food || Class==ItemClass.Potion || Class==ItemClass.Treasure) return true;
    return false;
  }

  public virtual void Invoke(Entity user) { if(user==App.Player) App.IO.Print("Nothing seems to happen."); }

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
  public int Age, Count, Weight;
  public ItemClass Class;
  public Color Color;
  public char Char;

  protected string name;

  static readonly Type[] copyCons = new Type[] { typeof(Item) };
}
#endregion

public abstract class Modifying : Item
{ public override bool CanStackWith(Item item) { return false; }

  public int AC { get { return Mods[(int)Attr.AC]; } set { SetAttr(Attr.AC, value); } }
  public int Dex { get { return Mods[(int)Attr.Dex]; } set { SetAttr(Attr.Dex, value); } }
  public int EV { get { return Mods[(int)Attr.EV]; } set { SetAttr(Attr.EV, value); } }
  public int Int { get { return Mods[(int)Attr.Int]; } set { SetAttr(Attr.Int, value); } }
  public int MaxHP { get { return Mods[(int)Attr.MaxHP]; } set { SetAttr(Attr.MaxHP, value); } }
  public int MaxMP { get { return Mods[(int)Attr.MaxMP]; } set { SetAttr(Attr.MaxMP, value); } }
  public int Speed { get { return Mods[(int)Attr.Speed]; } set { SetAttr(Attr.Speed, value); } }
  public int Str { get { return Mods[(int)Attr.Str]; } set { SetAttr(Attr.Str, value); } }

  public int GetAttr(Attr attribute) { return Mods[(int)attribute]; }
  public int SetAttr(Attr attribute, int val) { return Mods[(int)attribute]=val; }

  public int[] Mods = new int[(int)Attr.NumModifiable];
}

public abstract class Wearable : Modifying
{ public Wearable() { Slot=Slot.Invalid; }

  public override string InvName
  { get
    { string ret = FullName;
      if(worn) ret += " (worn)";
      return ret;
    }
  }

  public virtual void OnRemove(Entity equipper) { worn=false;  }
  public virtual void OnWear  (Entity equipper) { worn=true; }

  public string EquipText;
  public Slot Slot;
  bool worn;
}

public abstract class Wieldable : Modifying
{ public override string InvName
  { get
    { string ret = FullName;
      if(equipped) ret += " (equipped)";
      return ret;
    }
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
  public abstract void Read(Entity user);
}

} // namespace Chrono