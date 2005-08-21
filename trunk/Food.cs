using System;
using System.Xml;

namespace Chrono
{

#region Food
public abstract class Food : Item
{ public Food() { Class=ItemClass.Food; }

  public const int FoodPerWeight=75, MaxFoodPerTurn=150;

  [Flags] public enum Flag { None=0, Partial=1, Rotten=2, Tainted=4 };

  public int FoodLeft { get { return Weight*FoodPerWeight; } }

  public override string Name
  { get
    { string name = base.Name;
      return ((Flags&Flag.Partial)==0 ? "" : "partially eaten ") + ((Flags&Flag.Rotten)==0 ? name : "rotting "+name);
    }
  }

  public override bool CanStackWith(Item item)
  { if(!base.CanStackWith(item)) return false;
    Food food = (Food)item;
    return (Flags&Flag.Partial)==(food.Flags&Flag.Partial) && DecayTime==food.DecayTime &&
           (DecayTime==0 || Age==food.Age);
  }

  public virtual bool Eat(Entity user)
  { int eaten = Math.Min(user.Hunger, Math.Min(FoodLeft, MaxFoodPerTurn));
    user.Hunger -= eaten;
    Weight -= (eaten+(FoodPerWeight-1))/FoodPerWeight;
    Flags |= Flag.Partial;
    
    if((Flags&(Flag.Rotten|Flag.Tainted))!=0)
    { if(user==App.Player) App.IO.Print("Ulch! There is something wrong with this food.");
      user.AddEffect(Clone(), Attr.Sickness, 1, -1);
    }

    return Weight<=0;
  }
  
  public override bool Think(Entity holder)
  { base.Think(holder);
    if(DecayTime>0)
    { if(Age>=DecayTime*2)
      { if(holder==App.Player) App.IO.Print("Your {0} rot{1} away.", GetFullName(), VerbS);
        return true;
      }
      else if(Age>=DecayTime && (Flags&Flag.Rotten)==0) Rot(holder);
    }
    return false;
  }

  public Flag Flags;
  public int DecayTime;
  
  protected void Rot(Entity holder)
  { Flags |= Flag.Rotten;
    if(holder==App.Player)
      App.IO.Print(Global.Coinflip() ? "Eww! There's something really disgusting in your pack!"
                                      : "You smell the putrid stench of decay.");
  }
}
#endregion

public class FortuneCookie : Food
{ public FortuneCookie() { name="fortune cookie"; Color=Color.Brown; Weight=1; ShopValue=2; }

  public override bool Eat(Entity user)
  { if((Flags&Flag.Partial)==0) // use Partial to indicate whether or not it's been opened
    { App.IO.Print("The fortune cookie says: {0}",
                   "A starship ride has been promised to you by the galactic wizard.");
      Flags |= Flag.Partial;
    }
    return base.Eat(user);
  }

  public static readonly int SpawnChance=50; // 0.5% chance
}

#region Flesh
public sealed class Flesh : Food
{ public Flesh() { }
  public Flesh(Corpse from)
  { Color=from.Color; Weight=2; Prefix="chunk of "; PluralPrefix="chunks of "; PluralSuffix="";
    name=from.CorpseOf.Race.ToString().ToLower()+" flesh"; FromCorpse=from; DecayTime=60;
    Age=from.Age/2; // meat from older corpses decays quicker
    if((from.Flags&Corpse.Flag.Rotting)!=0) { Rot(null); Age=DecayTime; }
    if((from.Flags&Corpse.Flag.Tainted)!=0) Flags |= Flag.Tainted;
  }

  public Corpse FromCorpse;
}
#endregion

#region XmlFood
public sealed class XmlFood : Food
{ public XmlFood() { }
  public XmlFood(XmlNode node)
  { XmlItem.Init(this, node);
    if(!Xml.IsEmpty(node, "decayTime")) DecayTime = Xml.Int(node, "decayTime");
  }
}
#endregion

} // namespace Chrono