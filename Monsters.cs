using System;
using System.Collections.Generic;
using System.Xml;

namespace Chrono
{

#region Townsperson
public struct TownspersonData
{
  public int Job;
  public Race Race;
  public bool Male, IsAdult;
}

public class Townsperson : XmlEntityClass
{
  public Townsperson() { randomize = true; }
  public Townsperson(XmlNode node, Dictionary<string, XmlNode> idcache) : base(node, idcache) { }

  public override string GetBaseName(Entity e)
  {
    TownspersonData td = (TownspersonData)e.Data;
    return td.IsAdult ? jobs[td.Job] : td.Race.ToString().ToLower()+(td.Male ? " boy" : " girl");
  }

  public override Color GetColor(Entity e) { return ((TownspersonData)e.Data).IsAdult ? Color.Cyan : Color.LightCyan; }
  public override Gender GetGender(Entity e) { return ((TownspersonData)e.Data).Male ? Gender.Male : Gender.Female; }
  public override Race GetRace(Entity e) { return ((TownspersonData)e.Data).Race; }

  public override void Initialize(Entity e)
  {
    TownspersonData td = new TownspersonData();

    if(randomize)
    {
      td.Race = Race.Human;
      td.Job  = Global.Rand(jobs.Length);
      race = td.Race; // this is a hack
      base.Initialize(e);
    }
    else
    {
      base.Initialize(e);
      td.Race = race;
      td.Male = e.Gender==Gender.Male;
      td.Job  = Array.IndexOf(jobs, baseName);
      if(td.Job==-1) throw new NotImplementedException("no such job: "+baseName);
    }

    td.IsAdult = randomize && Global.Coinflip() || Xml.IsTrue(GetExtraAttr("IsAdult"));

    if(!td.IsAdult)
    {
      if(randomize) td.Male = Global.Coinflip();
      e.AlterBaseAttr(Attr.Str, Global.Rand(3));
      e.AlterBaseAttr(Attr.Int, Global.Rand(e.GetBaseAttr(Attr.Int)+1)); // potentially double base intelligence
      e.AlterBaseAttr(Attr.Dex, Global.Rand(4));
      e.SetBaseAttr(Attr.Speed, Speed.Normal-Speed.OneFourth);

      e.SetBaseAttr(Attr.MaxHP, 6);
      e.SetBaseAttr(Attr.MaxMP, e.GetBaseAttr(Attr.Int)/2);

      if(td.Male || Global.OneIn(5)) // boys and tomgirls are stronger and carry weapons
      {
        e.AlterBaseAttr(Attr.Str, Global.Rand(2, 4));
        e.AlterBaseAttr(Attr.MaxHP, Global.Rand(15));
        e.Pickup(new Item("builtin/ShortSword")); // TODO: make this a knife
      }
    }
    else
    {
      string name = jobs[td.Job];
      td.Male = name!="housewife" && name!="prostitute";

      e.SetBaseAttr(Attr.Speed, Speed.Normal);
      e.AlterBaseAttr(Attr.Str, Global.Rand(6));
      e.AlterBaseAttr(Attr.Int, Global.Rand(6));
      e.AlterBaseAttr(Attr.Dex, Global.Rand(6));

      switch(name)
      {
        case "hunter":
          e.AlterBaseAttr(Attr.Str, 3);
          e.AlterBaseAttr(Attr.Dex, 3);
          e.AlterBaseAttr(Attr.Speed, Speed.Quarter);
          e.Pickup(new Item("builtin/Bow"));
          e.Pickup(new Item(Global.OneIn(3) ? "builtin/FlamingArrow" : "builtin/BasicArrow", Global.Rand(10, 20)));
          // TODO: give knife and leather armor
          break;
        case "blacksmith":
          e.AlterBaseAttr(Attr.Str, 5);
          e.Pickup(new Item("builtin/ShortSword"));
          // TODO: give long blade and studded leather armor
          break;
        case "tailor":
        case "tinkerer":
          e.AlterBaseAttr(Attr.Dex, 2);
          // TODO: give knife
          break;
        case "cleric":
        case "priest":
          e.AlterBaseAttr(Attr.Int, 4);
          e.MemorizeSpell(ForceBoltSpell.Instance);
          e.MemorizeSpell(HealSpell.Instance);
          if(name!="cleric") { } // TODO: give weapon
          else
          {
            e.AlterBaseAttr(Attr.Str, 2);
            // TODO: give mace
          }
          break;
        case "carpenter":
        case "farmer":
          e.AlterBaseAttr(Attr.Str, 2);
          break;
        case "hobo":
        case "prostitute":
        case "housewife":
          e.AlterBaseAttr(Attr.Str, td.Male ? -1 : -2);
          if(name=="hobo" || Global.Coinflip()) { } // TODO: give knife
          break;
        case "shepherd":
          // TODO: give staff
          break;
        case "clerk":
        case "tanner":
          break;
        default: throw new NotImplementedException("Unknown job: "+name);
      }

      e.SetBaseAttr(Attr.MaxHP, e.GetBaseAttr(Attr.Str)+Global.Rand(td.Male ? 10 : 5));
      e.SetBaseAttr(Attr.MaxMP, e.GetBaseAttr(Attr.Int)+Global.Rand(10));
    }

    e.HP = e.MaxHP;
    e.MP = e.MaxMP;

    e.Data = td;
  }

  bool randomize;

  static readonly string[] jobs = new string[]
  { // female jobs
    "housewife", "housewife", "housewife", "prostitute", // housewives get selected more often than prostitutes
    // male jobs
    "blacksmith", "tanner", "tinkerer", "carpenter", "hunter",
    // either sex
    "farmer", "shepherd", "clerk", "hobo", "tailor", "cleric", "priest",
  };
}
#endregion

} // namespace Chrono