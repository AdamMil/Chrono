using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;

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

    Map map = Dungeon[0];
    Player = Player.Generate(EntityClass.Fighter, Race.Orc);
    Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    
    for(int y=0; y<map.Height; y++) // place Player on the up staircase of the first level
      for(int x=0; x<map.Width; x++)
        if(map[x, y].Type==TileType.UpStairs) { Player.X = x; Player.Y = y; break; }
    Player.SetBaseAttr(Attr.AC, 6);
    Player.SetBaseAttr(Attr.EV, 5);
    /*Player.SetSkill(Skill.Casting, 1);
    Player.SetSkill(Skill.Elemental, 1);
    Player.SetSkill(Skill.Telekinesis, 1);*/
    Player.SetSkill(Skill.Fighting, 1);
    Player.SetSkill(Skill.Armor, 1);
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
    Player.Pickup(new HealPotion()).Count=2;
    Player.Pickup(new Deodorant());
    Player.Pickup(new WandOfFire());
    Player.Pickup(new FoolsBook());
    map.Entities.Add(Player);

    IO.Render(Player);

    while(!Quit)
    { int level = Player.Map.Index;
      if(level>0) Dungeon[level-1].Simulate();
      if(level<Dungeon.Count-1) Dungeon[level+1].Simulate();
      Dungeon[level].Simulate();
    }
  }

  public static void Assert(bool test, string message)
  { if(!test) throw new ApplicationException("ASSERT: "+message);
  }
  public static void Assert(bool test, string format, params object[] parms)
  { if(!test) throw new ApplicationException("ASSERT: "+String.Format(format, parms));
  }
}

} // namespace Chrono