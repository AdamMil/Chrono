using System;

namespace Chrono
{

public class Food : Item
{ public Food() { Class=ItemClass.Food; }
  public Food(Item item) : base(item) { Flags=((Food)item).Flags; }

  public const int FoodPerWeight=50, MaxFoodPerTurn=100;

  public sealed class Flag
  { private Flag() { }
    public const uint None=0, Partial=1, NextFlag=2;
  };

  public int FoodLeft { get { return Weight*FoodPerWeight; } }

  public override string Name
  { get
    { string name = base.Name;
      return (Flags&Flag.Partial)==0 ? name : "partially eaten "+name;
    }
  }

  public override bool CanStackWith(Item item) // TODO: don't let items stack if they rot at different times
  { return base.CanStackWith(item) && (Flags&Flag.Partial)==0 && (((Food)item).Flags&Flag.Partial)==0;
  }

  public virtual bool Eat(Entity user)
  { 
    int eaten = Math.Min(user.Hunger, Math.Min(FoodLeft, MaxFoodPerTurn));
    user.Hunger -= eaten;
    Weight -= (eaten+(FoodPerWeight-1))/FoodPerWeight;
    Flags |= Flag.Partial;
    return Weight<=0;
  }

  public uint Flags;
}

public class FortuneCookie : Food
{ public FortuneCookie() { name="fortune cookie"; Color=Color.Brown; Weight=1; }
  public FortuneCookie(Item item) : base(item) { }

  public override bool Eat(Entity user)
  { if((Flags&Read)==0)
    { App.IO.Print("The fortune cookie says: {0}",
                   "A starship ride has been promised to you by the galactic wizard.");
      Flags |= Read;
    }
    return base.Eat(user);
  }

  const uint Read = Food.Flag.NextFlag;
}

public class Hamburger : Food
{ public Hamburger() { name="hamburger"; Color=Color.Red; Weight=5; }
  public Hamburger(Item item) : base(item) { }
}

} // namespace Chrono