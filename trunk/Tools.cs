using System;
using System.Runtime.Serialization;

namespace Chrono
{

[Serializable]
public class Deodorant : Chargeable
{ public Deodorant()
  { Class=ItemClass.Tool; name="deodorant"; Prefix="stick of "; PluralPrefix="sticks of ";
    Usability=ItemUse.Self; Weight=2; Charges=Global.NdN(3, 3);
  }
  public Deodorant(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override bool Use(Entity user, Direction dir)
  { if(Charges>0)
    { user.Smell = 0;
      Charges--;
      if(user==App.Player) App.IO.Print("You smell much better!");
      return false;
    }
    else return base.Use(user, dir);
  }

  public static readonly int SpawnChance=50; // 0.5% chance
  public static readonly int ShopValue=100;
  public static readonly string ShortDesc = "A travel-size stick of deodorant.";
  public static readonly string LongDesc =
    "When applied to strategic locations on your body, this item considerably reduces the strength "+
    "of your scent. This deodorant goes on clear -- no icky white stuff! Fresh spring scent.";
}

} // namespace Chrono