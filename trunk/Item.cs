using System;

namespace Chrono
{

public class Item
{ public enum ItemClass
  { Amulet, Weapon, Armor, Comestible, Corpse, Ring, Wand, Tool, Treasure
  }

  public string Name, Prefix, PluralPrefix, PluralSuffix, Title;
  public int Weight;
  public ItemClass Class;
}

} // namespace Chrono