using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using ICSharpCode.SharpZipLib.GZip;

namespace Chrono
{

public sealed class App
{ 
  public static Overworld World = new Overworld();
  public static Player Player;
  public static InputOutput IO;
  public static bool Quit;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.05");
    IO.Print("Chrono 0.05 by Adam Milazzo");
    IO.Print();
    
    if(false&&File.Exists("c:/chrono.sav"))
    { FileStream f = File.Open("c:/chrono.sav", FileMode.Open);
      Load(f);
      f.Close();
      //File.Delete("c:/chrono.sav");
      App.IO.Print("Welcome back to Chrono, {0}!", Player.Name);
    }
    else
    { Map map = World[0];

      char c = IO.CharChoice("(w)izard or (f)ighter?", "wf");
      Player = Player.Generate(c=='w' ? EntityClass.Wizard : EntityClass.Fighter, Race.Human);
      Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");

      if(Player.Class==EntityClass.Fighter)
      { Player.SetSkill(Skill.Fighting, 1);
        Player.SetSkill(Skill.Armor, 1);
        Player.SetSkill(Skill.Bow, 1);
        Player.SetSkill(Skill.ShortBlade, 1);
        Player.Pickup(new ShortSword());
      }
      else
      { Player.SetSkill(Skill.Casting, 1);
        Player.SetSkill(Skill.Elemental, 1);
        Player.SetSkill(Skill.Telekinesis, 1);
        Player.SetSkill(Skill.Divination, 1);
        Player.Pickup(new PoisonDart()).Count = 10;
        Player.Pickup(new FoolsBook());
        Player.MemorizeSpell(ForceBolt.Default, 5000);
      }
      Player.Pickup(new Hamburger()).Count = 2;
      Player.Pickup(new TeleportScroll());
      Player.Pickup(new HealPotion()).Count = 2;
      Player.Pickup(new Deodorant());
      Player.Pickup(new Gold()).Count = 100;
      foreach(Item i in Player.Inv) Player.AddKnowledge(i);

      for(int i=0; i<map.Links.Length; i++)
        if(map.Links[i].ToLevel==(int)Overworld.Place.GTown)
        { Link link = map.GetLink(map.Links[i].FromPoint);
          link.ToDungeon[link.ToLevel].Entities.Add(Player);
          break;
        }

      for(int y=0; y<Player.Map.Height; y++)
        for(int x=0; x<Player.Map.Width; x++)
        { Tile tile  = Player.Map[x, y];
          tile.Items = tile.Items==null || tile.Items.Count==0 ? null : tile.Items.Clone();
          Player.Memory[x, y] = tile;
        }
    }

    IO.Render(Player);
    while(!Quit) Player.Map.Simulate();

    if(Player.HP>0)
    { FileStream f = File.Open("c:/chrono.sav", FileMode.Create);
      Save(f);
      f.Close();
    }
  }

  public static void Assert(bool test, string message)
  { if(!test) throw new ApplicationException("ASSERT: "+message);
  }
  public static void Assert(bool test, string format, params object[] parms)
  { if(!test) throw new ApplicationException("ASSERT: "+String.Format(format, parms));
  }

  public static void Load(Stream stream)
  { GZipInputStream s = new GZipInputStream(stream);
    BinaryFormatter f = new BinaryFormatter();
    Global.ObjHash = new Hashtable();

    Global.Deserialize(s, f);
    Potion.Deserialize(s, f);
    Ring.Deserialize(s, f);
    Scroll.Deserialize(s, f);
    Wand.Deserialize(s, f);

    World  = (Overworld)f.Deserialize(s);
    Player = (Player)f.Deserialize(s);

    Global.ObjHash = null;
  }

  public static void Save(Stream stream)
  { App.IO.Print("Saving...");
    GZipOutputStream s = new GZipOutputStream(stream);
    BinaryFormatter f = new BinaryFormatter();
    Global.ObjHash = new Hashtable();

    Global.Serialize(s, f);
    Potion.Serialize(s, f);
    Ring.Serialize(s, f);
    Scroll.Serialize(s, f);
    Wand.Serialize(s, f);

    f.Serialize(s, World);
    f.Serialize(s, Player);

    Global.ObjHash = null;
    s.Close();
  }
}

} // namespace Chrono