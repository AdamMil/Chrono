using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

[Serializable]
public class Orc : AI
{ public Orc() { Race=Race.Orc; Color=Color.Yellow; baseKillExp=10; }
  public Orc(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);

    SetSkill(Skill.Fighting, 1);

    if(Global.Rand(100)<10)
    { SetSkill(Skill.Bow, 1);
      AddItem(new Bow());
      AddItem(Global.Coinflip() ? (Arrow)new BasicArrow() : (Arrow)new FlamingArrow()).Count = Global.Rand(5) + 5;
    }
    else if(Global.Rand(100)<10) { AddItem(new ShortSword()); SetSkill(Skill.ShortBlade, 1); }
    else if(Global.Rand(100)<10) { AddItem(new PoisonDart()).Count = 5; SetSkill(Skill.Throwing, 1); }
    else SetSkill(Skill.UnarmedCombat, 1);
    if(Global.Rand(100)<10) AddItem(new PaperBag());
    if(Global.Rand(100)<10) AddItem(new TeleportScroll());
    if(Global.Rand(100)<10) AddItem(new HealPotion());
  }

}

} // namespace Chrono
