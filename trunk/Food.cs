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

  public override bool Use(Creature user)
  { int eaten = Math.Min(user.Hunger, Math.Min(Weight*10, 100));
    user.Hunger -= eaten;
    Weight -= (eaten+9)/10;
    Flags |= Flag.Partial;
    return Weight<=0;
  }
  
  public uint Flags;
}

public class FortuneCookie : Food
{ public FortuneCookie() { Name="fortune cookie"; Color=Color.Brown; }
  
  public override bool Use(Creature user)
  { if((Flags&Read)==0)
    { App.IO.Print("The fortune cookie says: {0}",
                   "A starship ride has been promised to you by the galactic wizard.");
      Flags |= Read;
    }
    return base.Use(user);
  }

  const uint Read = Food.Flag.NextFlag;
}

} // namespace Chrono