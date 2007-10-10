using System;
using System.Xml;

namespace Chrono
{

#region Scroll
public abstract class Scroll : ItemClass
{
  protected Scroll()
  {
    Type=ItemType.Scroll; prefix="scroll of "; pluralPrefix="scrolls of "; pluralSuffix=""; weight=110;
    Color=Color.White; Material=Paper.Instance;
  }

  public override string GetBaseName(Item item) { return Spell.Name; }

  public virtual bool PromptCast(Item item) // called interactively
  {
    throw new NotImplementedException();
  }

  public virtual void Read(Item item) // called interactively
  {
    AutoIdentify(item);
    if(!PromptCast(item)) App.IO.Print("The scroll crumbles into dust.");
  }

  public Spell Spell;

  protected void AutoIdentify(Item item)
  {
    if(Spell.AutoIdentify && !App.Player.KnowsAbout(this))
    {
      App.Player.AddKnowledge(this);
      App.IO.Print("This is {0}.", GetAName(item));
    }
  }

  protected string Prompt;
}
#endregion

#region IdentifyScroll
public sealed class IdentifyScroll : Scroll
{
  public IdentifyScroll() { Spell=IdentifySpell.Instance; Price=40; }

  public override void Read(Item item)
  {
    if(item.Cursed && Global.Coinflip())
    {
      App.IO.Print("The scroll crumbles into dust.");
      return;
    }

    AutoIdentify(item);

    bool idAll = Global.OneIn(item.Blessed ? 3 : 20);
    if(idAll)
    {
      foreach(Item i in App.Player.Inv) if(!i.Class.Identified) Spell.Cast(App.Player, item, i);
    }
    else
    {
      int n = item.Blessed ? Global.Rand(4)+1 : 1;
      while(n--!=0) PromptCast(item);
    }
  }
}
#endregion

#region XmlScroll
public sealed class XmlScroll : Scroll
{
  public XmlScroll(XmlNode node)
  {
    ItemClass.Init(this, node);
    Spell  = Spell.Get(Xml.Attr(node, "spell"));
    Prompt = Xml.Attr(node, "prompt");
  }
}
#endregion

} // namespace Chrono