using System;
using System.Drawing;

namespace Chrono
{

public abstract class AI : Entity
{
  public override void Die(string cause)
  { for(int i=0; i<Inv.Count; i++) Map.AddItem(Position, Inv[i]);
    Map.Creatures.Remove(this);
  }

}

} // namespace Chrono