using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using ICSharpCode.SharpZipLib.GZip;

namespace Chrono
{

public sealed class App
{ 
  public static Dungeon Dungeon = new Dungeon();
  public static Player Player;
  public static InputOutput IO;
  public static bool Quit;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.02");
    IO.Print("Chrono 0.02 by Adam Milazzo");
    IO.Print();
    
    if(File.Exists("c:/chrono.sav"))
    { FileStream f = File.Open("c:/chrono.sav", FileMode.Open);
      Load(f);
      f.Close();
      //File.Delete("c:/chrono.sav");
      App.IO.Print("Welcome back to Chrono, {0}!", Player.Name);
    }
    else
    { Map map = Dungeon[0];

      Player = Player.Generate(EntityClass.Wizard, Race.Human);
      Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
      
      for(int y=0; y<map.Height; y++) // place Player on the up staircase of the first level
        for(int x=0; x<map.Width; x++)
          if(map[x, y].Type==TileType.UpStairs) { Player.X = x; Player.Y = y; break; }
      Player.SetBaseAttr(Attr.AC, 5);
      Player.SetBaseAttr(Attr.EV, 5);
      Player.SetSkill(Skill.Casting, 1);
      Player.SetSkill(Skill.Elemental, 1);
      Player.SetSkill(Skill.Telekinesis, 1);
      /*Player.SetSkill(Skill.Fighting, 1);
      Player.SetSkill(Skill.Armor, 1);*/
      Player.Pickup(new Bow());
      Player.Pickup(new ShortSword());
      Player.Pickup(new Dart()).Count = 20;
      Player.Pickup(new Buckler());
      Player.Pickup(new PaperBag());
      Player.Pickup(new BasicArrow()).Count = 20;
      Player.Pickup(new FlamingArrow()).Count = 10;
      Player.Pickup(new Hamburger());
      Player.Pickup(new InvisibilityRing());
      Player.Pickup(new SeeInvisibleRing());
      Player.Pickup(new TeleportScroll());
      Player.Pickup(new IdentifyScroll()).Count=2;
      Player.Pickup(new HealPotion()).Count=2;
      Player.Pickup(new Deodorant());
      Player.Pickup(new WandOfFire());
      Player.Pickup(new FoolsBook());

      Player.Inv[0].Curse();
      Player.Inv[3].Curse();
      Player.Inv[4].Curse();

      map.Entities.Add(Player);
    }

    IO.Render(Player);

    while(!Quit)
    { int level = Player.Map.Index;
      if(level>0) Dungeon[level-1].Simulate();
      if(level<Dungeon.Count-1) Dungeon[level+1].Simulate();
      Dungeon[level].Simulate();
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

  public static void Load(Stream stream)
  { GZipInputStream s = new GZipInputStream(stream);
    BinaryFormatter f = new BinaryFormatter();
    Global.ObjHash = new Hashtable();

    Global.Deserialize(s, f);
    Potion.Deserialize(s, f);
    Ring.Deserialize(s, f);
    Scroll.Deserialize(s, f);
    Wand.Deserialize(s, f);

    Dungeon = (Dungeon)f.Deserialize(s);
    Player  = (Player)f.Deserialize(s);

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

    f.Serialize(s, Dungeon);
    f.Serialize(s, Player);

    Global.ObjHash = null;
    s.Close();
  }
}

} // namespace Chrono