using System;

namespace Chrono
{

public class Deodorant : Chargeable
{ public Deodorant()
  { Class=ItemClass.Tool; name="deodorant"; PluralPrefix="sticks of "; Usability=ItemUse.Self;
    Charges=Global.NdN(3, 3);
    ShortDesc = "A travel-size stick of deodorant.";
    LongDesc = "When applied to strategic locations on your body, this item considerably reduces the strength "+
               "of your scent. This deodorant goes on clear -- no icky white stuff! Fresh spring scent.";
  }
  
  public override bool Use(Entity user, Direction dir)
  { if(Charges>0)
    { user.Smell = 0; // smell drops to 5% of maximum
      Charges--;
      if(user==App.Player) App.IO.Print("You smell much better!");
      return false;
    }
    else return base.Use(user, dir);
  }
}

} // namespace Chrono