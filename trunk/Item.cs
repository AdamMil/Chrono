using System;

namespace Chrono
{

public enum ItemClass
{ Invalid, Amulet, Weapon, Armor, Food, Corpse, Ring, Potion, Wand, Tool, Container, Treasure, NumClasses
}

public abstract class Item : ICloneable
{ public Item()
  { Prefix="a "; PluralPrefix=string.Empty; PluralSuffix="s"; Count=1;
  }
  protected Item(Item item)
  { Name=item.Name; Title=item.Title;
    Prefix=item.Prefix; PluralPrefix=item.PluralPrefix; PluralSuffix=item.PluralSuffix;
    Age=item.Age; Count=item.Count; Weight=item.Weight; Color=item.Color; Char=item.Char;
  }

  public abstract bool Use(Creature user);

  public string AreIs { get { return Count>1 ? "are" : "is"; } }

  public string FullName
  { get
    { string ret = Count>1 ? Count+" "+PluralPrefix+Name+PluralSuffix : Prefix+Name;
      if(Title!=null && Title!="") ret += " named "+Title;
      return ret;
    }
  }

  public string ItOne { get { return Count>1 ? "one" : "it"; } }
  public string ItThem { get { return Count>1 ? "them" : "it"; } }

  #region ICloneable Members
  public virtual object Clone() { return GetType().GetConstructor(copyCons).Invoke(new object[] { this }); }
  #endregion

  public virtual bool CanStackWith(Item item)
  { if(item.GetType() != GetType()) return false;
    if(Class==ItemClass.Food || Class==ItemClass.Potion || Class==ItemClass.Treasure) return true;
    return false;
  }

  public Item Split(int toRemove)
  { if(toRemove>=Count) throw new ArgumentOutOfRangeException("toRemove", toRemove, "is >= than Count");
    Item newItem = (Item)Clone();
    newItem.Count = toRemove;
    Count -= toRemove;
    return newItem;
  }

  public string Name, Title, Prefix, PluralPrefix, PluralSuffix;
  public int Age, Count, Weight;
  public ItemClass Class;
  public Color Color;
  public char Char;

  static readonly Type[] copyCons = new Type[] { typeof(Item) };
}

} // namespace Chrono