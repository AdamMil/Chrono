using System;
using System.Drawing;

namespace Chrono
{

public abstract class Potion : Item
{ public Potion()
  { Class=ItemClass.Potion; Prefix="a potion of "; PluralPrefix="potions of "; PluralSuffix=""; Weight=5;
  }
  protected Potion(Item item) : base(item) { }

  public override bool CanStackWith(Item item)
  { return base.CanStackWith(item) && ((Potion)item).Name==Name;
  }

  public abstract void Drink(Entity user);
}

public class HealPotion : Potion
{ public HealPotion() { name="healing"; Color=Color.LightGreen; }
  public HealPotion(Item item) : base(item) { }
  
  public override void Drink(Entity user)
  { user.OnDrink(this);
    if(user.MaxHP-user.HP>0)
    { user.HP += Global.NdN(4, 6);
      if(user==App.Player) App.IO.Print("You feel better.");
      else if(App.Player.CanSee(user)) App.IO.Print("{0} looks better.");
    }
    else if(user==App.Player) App.IO.Print("Nothing seems to happen.");
  }

  public override bool Hit(Entity user, Point pos) { return true; }
  public override bool Hit(Entity user, Entity hit) { return true; }
}

} // namespace Chrono