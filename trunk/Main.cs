using System;
using System.Collections;

namespace Chrono
{

public sealed class App
{ 
  public class MapCollection : ArrayList
  { public new Map this[int i] { get { return (Map)base[i]; } }
  }

  public static Player Player;
  public static int CurrentLevel;
  public static InputOutput IO;
  public static MapCollection Maps = new MapCollection();
  public static bool Quit;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.01");
    IO.Print("Chrono 0.01 by Adam Milazzo");
    IO.Print();

    Map map = new RoomyMapGenerator().Generate();

    Player = Player.Generate(CreatureClass.Fighter, Race.Human);
    Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    for(int y=0; y<map.Height; y++) // place Player on the up staircase of the first level
      for(int x=0; x<map.Width; x++)
        if(map[x, y].Type==TileType.UpStairs) { Player.X = x; Player.Y = y; break; }
    Player.SetRawAttr(Attr.AC, 7);
    Player.SetRawAttr(Attr.EV, 6);
    Player.Pickup(new CueStick());
    Player.Pickup(new PaperBag());
    map.Creatures.Add(Player);

    for(int i=0; i<10; i++)
    { int idx = map.Creatures.Add(Creature.Generate(typeof(Orc), 0, CreatureClass.Fighter));
      map.Creatures[idx].Position = map.FreeSpace();
    }

    for(int i=0; i<15; i++) map.AddItem(map.FreeSpace(true, true), new FortuneCookie());
    for(int i=0; i<5; i++) map.AddItem(map.FreeSpace(true, true), new Hamburger());
    map.AddItem(map.FreeSpace(true, true), new TwigOfDeath());

    Maps.Add(map);
    IO.Render(Player);

    while(!Quit)
    { if(CurrentLevel>0) Maps[CurrentLevel-1].Simulate();
      if(CurrentLevel<Maps.Count-1) Maps[CurrentLevel+1].Simulate();
      Maps[CurrentLevel].Simulate();
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