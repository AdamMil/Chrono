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

  public static void CastChance(int intel, int skill, int diff) // assuming the user knows it
  { double div = Math.Pow(1.25, intel)/8;
    if(skill>0) div += skill*Math.Pow(1.02, skill)*16*div/100;
    int chance = 100-(int)Math.Round(diff/div);
    chance = chance<0 ? 0 : chance>100 ? 100 : chance;
    System.Diagnostics.Debugger.Log(1, "", string.Format("Int: {0}, Skill: {1}, Diff: {2} = {3}%\n", intel, skill, diff, chance));
  }

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.02");
    IO.Print("Chrono 0.02 by Adam Milazzo");
    IO.Print();

    for(int d=50; d<1000; d+=100)
      for(int i=8; i<23; i++) CastChance(i, 0, d);

    Map map = Dungeon[0];
    Player = Player.Generate(EntityClass.Wizard, Race.Human);
    Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    
    for(int y=0; y<map.Height; y++) // place Player on the up staircase of the first level
      for(int x=0; x<map.Width; x++)
        if(map[x, y].Type==TileType.UpStairs) { Player.X = x; Player.Y = y; break; }
    Player.SetRawAttr(Attr.AC, 7);
    Player.SetRawAttr(Attr.EV, 6);
    Player.SetRawAttr(Attr.Stealth, 5);
    Player.Pickup(new Bow());
    Player.Pickup(new ShortSword());
    Player.Pickup(new Dart()).Count = 20;
    Player.Pickup(new Buckler());
    Player.Pickup(new PaperBag());
    Player.Pickup(new BadArrow()).Count = 20;
    Player.Pickup(new GoodArrow()).Count = 20;
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