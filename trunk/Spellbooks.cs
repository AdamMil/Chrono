using System;
using System.Drawing;

namespace Chrono
{

[NoClone]
public abstract class Spellbook : Readable
{ public Spellbook()
  { Class=ItemClass.Spellbook; Weight=35; Prefix="book of ";
    Reads=Global.NdN(4, 5);
  }

  public Spell[] Spells;
  public int Reads;
}

public class FoolsBook : Spellbook
{ public FoolsBook()
  { name="tinker toys";
    Spells = new Spell[] { AmnesiaSpell.Default, ForceBolt.Default, TeleportSpell.Default, FireSpell.Default, };
  }

  public static readonly int SpawnChance = 10; // 0.1% chance
  public static readonly int ShopValue = 250;
}

} // namespace Chrono