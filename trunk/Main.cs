using System;
using System.Collections;

namespace Chrono
{

public sealed class App
{ 
  public static Dungeon Dungeon = new Dungeon();
  public static Player Player;
  public static int CurrentLevel;
  public static InputOutput IO;
  public static bool Quit;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.02");
    IO.Print("Chrono 0.02 by Adam Milazzo");
    IO.Print();

    Map map = Dungeon[0];
    Player = Player.Generate(EntityClass.Fighter, Race.Human);
    Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    
    for(int y=0; y<map.Height; y++) // place Player on the up staircase of the first level
      for(int x=0; x<map.Width; x++)
        if(map[x, y].Type==TileType.UpStairs) { Player.X = x; Player.Y = y; break; }
    Player.SetRawAttr(Attr.AC, 7);
    Player.SetRawAttr(Attr.EV, 6);
    Player.SetRawAttr(Attr.Stealth, 10);
    Player.Pickup(new CueStick());
    //Player.Pickup(new Deodorant());
    Player.Pickup(new Buckler());
    Player.Pickup(new TwigOfDeath());
    Player.Pickup(new ShortSword());
    Player.Pickup(new PaperBag());
    //Player.Pickup(new InvisibilityRing());
    //Player.Pickup(new SeeInvisibleRing());
    Player.Pickup(new Hamburger());
    Player.Pickup(new HealPotion()).Count=2;
    Player.Pickup(new TeleportScroll());
    map.Entities.Add(Player);

    IO.Render(Player);

    while(!Quit)
    { if(CurrentLevel>0) Dungeon[CurrentLevel-1].Simulate();
      if(CurrentLevel<Dungeon.Count-1) Dungeon[CurrentLevel+1].Simulate();
      Dungeon[CurrentLevel].Simulate();
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