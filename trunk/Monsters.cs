using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

#region Orc
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
#endregion

#region Townsperson
[Serializable]
public class Townsperson : AI
{ public Townsperson()
  { switch(Global.Rand(4))
    { case 0: case 1: case 2: Race=Race.Human; break;
      case 3: Race=Race.Orc; break;
    }
    Color = Global.Coinflip() ? Color.Cyan : Color.LightCyan; // light cyan is a child, cyan is an adult
    defaultState = Global.Coinflip() ? AIState.Idle : AIState.Wandering;
    baseKillExp = 3;
    
    if(IsAdult) name = jobs[Global.Rand(jobs.Length)];
    else name += Race.ToString().ToLower() + (Global.Coinflip() ? " boy" : " girl");
  }
  public Townsperson(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public override string BaseName { get { return name; } }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);

    if(IsAdult)
    { SetSkill(Skill.Fighting, 1);
      SetSkill(Skill.UnarmedCombat, 1);
      
      switch(name)
      { case "hunter":
          SetSkill(Skill.Fighting, 2);
          SetSkill(Skill.Bow, 3);
          SetSkill(Skill.Dagger, 1);
          AlterBaseAttr(Attr.Str, 1);
          AlterBaseAttr(Attr.Dex, 2);
          AddItem(new Bow());
          AddItem(new BasicArrow()).Count = 10;
          // TODO: give dagger and leather armor
          break;
        case "blacksmith":
          SetSkill(Skill.ShortBlade, 1);
          SetSkill(Skill.LongBlade, 1);
          AlterBaseAttr(Attr.Str, 3);
          AddItem(new ShortSword());
          // TODO: give armor
          break;
        case "tailor": case "tinkerer":
          SetSkill(Skill.Dagger, 1);
          AlterBaseAttr(Attr.Dex, 2);
          // TODO: give weapon
          break;
        case "cleric": case "priestess": case "priest":
          SetSkill(Skill.Casting, 3);
          SetSkill(Skill.Channeling, 3);
          AlterBaseAttr(Attr.Int, 5);
          // TODO: give spells
          if(name=="cleric")
          { SetSkill(Skill.Fighting, 2);
            SetSkill(Skill.MaceFlail, 1);
            AlterBaseAttr(Attr.Str, 1);
            // TODO: give weapon
          }
          break;
        case "carpenter": case "farmer": AlterBaseAttr(Attr.Str, 2); break;
        case "hobo": case "prostitute": case "housewife":
          AlterBaseAttr(Attr.Str, -1);
          if(name!="hobo" && Global.Coinflip())
          { // TODO: give dagger
          }
          break;
        case "shepherd":
          SetSkill(Skill.Staff, 2);
          // TODO: give weapon
          break;
      }
    }
    else
    { AlterBaseAttr(Attr.Str, -1);    // children are weak,
      AlterBaseAttr(Attr.Dex, -1);    // clumsy,
      AlterBaseAttr(Attr.MaxHP, -5);  // frail,
      AlterBaseAttr(Attr.Speed, 10);  // and slow
    }
  }

  protected bool IsAdult { get { return Color==Color.Cyan; } }
  
  protected string name;
  
  static readonly string[] jobs = new string[]
  { // female jobs
    "housewife", "housewife", "housewife", "prostitute", "priestess", // "housewife"s get selected more often
    // male jobs
    "farmer", "blacksmith", "tanner", "tinkerer", "carpenter", "hunter", "shepherd", "priest",
    // either sex
    "clerk", "hobo", "tailor", "cleric",
  };
}
#endregion

} // namespace Chrono
