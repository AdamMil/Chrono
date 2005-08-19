using System;
using System.Collections;
using System.IO;
using ICSharpCode.SharpZipLib.GZip;

namespace Chrono
{

public sealed class App
{ 
  public static Dungeon World = new Dungeon("overworld");
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
      File.Delete("c:/chrono.sav");
      App.IO.Print("Welcome back to Chrono, {0}!", Player.Name);
    }
    else
    { char c = IO.CharChoice("(w)izard or (f)ighter?", "wf");
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
        Player.Pickup(Item.Make(ItemClass.Spellbook, "tinker toys"));
        Player.MemorizeSpell(ForceBolt.Default, 5000);
      }
      Player.Pickup(Item.Make(ItemClass.Food, "hamburger")).Count = 2;
      Player.Pickup(Item.Make(ItemClass.Scroll, "teleport"));
      Player.Pickup(Item.Make(ItemClass.Potion, "healing")).Count = 2;
      Player.Pickup(Item.Make(ItemClass.Tool, "deodorant"));
      Player.Pickup(new Gold()).Count = 100;
      foreach(Item i in Player.Inv) Player.AddKnowledge(i);

      Map map = World[World.StartSection][0];
      map.Entities.Add(Player);
      Player.Position = map.FreeSpaceNear(map.GetEntity("Pa").Position);

      for(int y=0; y<Player.Map.Height; y++) // magic mapping + find items
        for(int x=0; x<Player.Map.Width; x++)
        { Tile tile  = Player.Map[x, y];
          tile.Items = tile.Items==null || tile.Items.Count==0 ? null : tile.Items.Clone();
          Player.Memory[x, y] = tile;
        }
    }

    IO.Render(Player);
    while(!Quit)
    { try { Player.Map.Simulate(); }
      catch(Exception e) { App.IO.Print("{0} occurred: {1}", e.GetType().Name, e.Message); }
    }

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

  public static void Load(Stream stream) { throw new NotImplementedException(); }

  public static void Save(Stream stream)
  { App.IO.Print("Saving...");
    throw new NotImplementedException();
  }
}

} // namespace Chrono