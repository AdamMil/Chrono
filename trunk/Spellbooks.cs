using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

public abstract class Spellbook : Readable
{ public Spellbook()
  { Class=ItemClass.Spellbook; Weight=35; Prefix="book of ";
    Reads=Global.NdN(4, 5);
  }
  protected Spellbook(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public Spell[] Spells;
  public int Reads;
}

[Serializable]
public class FoolsBook : Spellbook
{ public FoolsBook()
  { name="tinker toys";
    Spells = new Spell[] { AmnesiaSpell.Default, ForceBolt.Default, TeleportSpell.Default, FireSpell.Default, };
  }
  public FoolsBook(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public static readonly int SpawnChance = 10; // 0.1% chance
}

} // namespace Chrono