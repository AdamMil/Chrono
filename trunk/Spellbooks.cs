using System;
using System.Drawing;

namespace Chrono
{

public abstract class Spellbook : Readable
{ public Spellbook()
  { Class=ItemClass.Spellbook; Weight=35; Prefix="book of "; PluralPrefix="books of "; PluralSuffix=string.Empty;
  }

  public override void Read(Entity user) // only called interactively
  { 
  }

  public Spell[] Spells;
}

} // namespace Chrono