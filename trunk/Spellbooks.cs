using System;
using System.Drawing;

namespace Chrono
{

public abstract class Spellbook : Readable
{ public Spellbook()
  { Class=ItemClass.Spellbook; Weight=35; Prefix="book of ";
    Reads=Global.NdN(4, 4);
  }

  public Spell[] Spells;
  public int Reads;
}

public class FoolsBook : Spellbook
{ public FoolsBook()
  { name="tinker toys";
    Spells = new Spell[] { TeleportSpell.Default, FireSpell.Default, AmnesiaSpell.Default };
  }
}

} // namespace Chrono