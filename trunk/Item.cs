using System;

namespace Chrono
{

public enum ItemClass
{ Invalid, Any=Invalid,
  Amulet, Weapon, Armor, Missile, Food, Corpse, Ring, Potion, Wand, Tool, Container, Treasure,
  NumClasses
}

#region Item
public abstract class Item : ICloneable
{ public Item()
  { Prefix="a "; PluralPrefix=string.Empty; PluralSuffix="s"; Count=1;
  }
  protected Item(Item item)
  { Name=item.Name; Title=item.Title;
    Prefix=item.Prefix; PluralPrefix=item.PluralPrefix; PluralSuffix=item.PluralSuffix;
    Age=item.Age; Count=item.Count; Weight=item.Weight; Color=item.Color; Char=item.Char;
    AllHandWield=item.AllHandWield;
  }

  public virtual void Think(Creature holder) { Age++; }

  public virtual bool Apply(Creature user, Direction direction) { return false; }
  public virtual bool Invoke(Creature user) { return false; }

  public string AreIs { get { return Count>1 ? "are" : "is"; } }

  public virtual string FullName
  { get
    { string ret = Count>1 ? Count+" "+PluralPrefix+Name+PluralSuffix : Prefix+Name;
      if(Title!=null) ret += " named "+Title;
      return ret;
    }
  }

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

  public virtual void OnPickup(Creature holder) { }
  public virtual void OnDrop  (Creature holder) { }

  public Item Split(int toRemove)
  { if(toRemove>=Count) throw new ArgumentOutOfRangeException("toRemove", toRemove, "is >= than Count");
    Item newItem = (Item)Clone();
    newItem.Count = toRemove;
    Count -= toRemove;
    return newItem;
  }

  public override string ToString() { return FullName; }

  public string Name, Title, Prefix, PluralPrefix, PluralSuffix;
  public int Age, Count, Weight;
  public ItemClass Class;
  public Color Color;
  public char Char;
  public bool AllHandWield;

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

  public override string FullName
  { get
    { string ret = base.FullName;
      if(worn) ret += " (worn)";
      return ret;
    }
  }

  public virtual void OnRemove(Creature equipper) { worn=false;  }
  public virtual void OnWear  (Creature equipper) { worn=true; }

  public string EquipText;
  public Slot Slot;
  bool worn;
}

} // namespace Chrono