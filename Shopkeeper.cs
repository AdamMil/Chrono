using System;

namespace Chrono
{

public sealed class ShopkeeperClass : EntityClass
{
  public ShopkeeperClass() { race = Race.Human; }
}

public sealed class Shopkeeper : Entity
{
  public Shopkeeper() : base("builtin/Shopkeeper") { }
}

} // namespace Chrono