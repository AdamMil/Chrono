using System;

namespace Chrono
{

public class Food : Item
{ public Food() { Class=ItemClass.Food; }
  protected Food(Item item) : base(item) { Flags=((Food)item).Flags; }

  public sealed class Flag
  { private Flag() { }
    public const uint None=0, Partial=1, NextFlag=2;
  };

  public override bool CanStackWith(Item item) // TODO: don't let items stack if they rot at different times
  { return base.CanStackWith(item) && (Flags&Flag.Partial)==0 && (((Food)item).Flags&Flag.Partial)==0;
  }

  public virtual bool Eat(Creature user)
  { int eaten = Math.Min(user.Hunger, Math.Min(Weight*50, 100));
    user.Hunger -= eaten;
    Weight -= (eaten+9)/10;
    Flags |= Flag.Partial;
    return Weight<=0;
  }
  
  public uint Flags;
}

public class FortuneCookie : Food
{ public FortuneCookie() { Name="fortune cookie"; Color=Color.Brown; Weight=1; }
  public FortuneCookie(Item item) : base(item) { }
  
  public override bool Eat(Creature user)
  { if((Flags&Read)==0)
    { App.IO.Print("The fortune cookie says: {0}",
                   "A starship ride has been promised to you by the galactic wizard.");
      Flags |= Read;
    }
    return base.Eat(user);
  }

  const uint Read = Food.Flag.NextFlag;
}

} // namespace Chrono