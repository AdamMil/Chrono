using System;
using System.Drawing;
using System.Xml;

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

public sealed class XmlSpellbook : Spellbook
{ public XmlSpellbook(XmlNode node)
  { XmlItem.Init(this, node);
    if(!Xml.IsEmpty(node, "reads")) Reads = Xml.RangeInt(node, "reads");

    string[] spells = Xml.List(node, "spells");
    Spells = new Spell[spells.Length];
    for(int i=0; i<spells.Length; i++) Spells[i] = Spell.Get(spells[i]);
  }
}

} // namespace Chrono