using System;

namespace Chrono
{

public class Deodorant : Chargeable
{ public Deodorant()
  { Class=ItemClass.Tool; name="deodorant"; Prefix="a stick of "; PluralPrefix="sticks of ";
    Usability=ItemUse.Self; Weight=2; Charges=Global.NdN(3, 3);
    ShortDesc = "A travel-size stick of deodorant.";
    LongDesc = "When applied to strategic locations on your body, this item considerably reduces the strength "+
               "of your scent. This deodorant goes on clear -- no icky white stuff! Fresh spring scent.";
  }
  
  public override bool Use(Entity user, Direction dir)
  { if(Charges>0)
    { user.Smell = 0;
      Charges--;
      if(user==App.Player) App.IO.Print("You smell much better!");
      return false;
    }
    else return base.Use(user, dir);
  }
}

} // namespace Chrono