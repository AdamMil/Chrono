using System;
using System.Runtime.Serialization;

namespace Chrono
{

[NoClone]
public class Amulet : Wearable
{ public Amulet() { Class=ItemClass.Amulet; }
  protected Amulet(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

} // namespace Chrono