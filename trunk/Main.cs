using System;
using System.Collections;

namespace Chrono
{

public sealed class App
{ 
  public class MapCollection : ArrayList
  { public new Map this[int i] { get { return (Map)base[i]; } }
  }
  
  public static int CurrentLevel;
  public static InputOutput IO;
  public static MapCollection Maps = new MapCollection();
  public static bool Quit;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.01");
    IO.Print("Chrono 0.01 by Adam Milazzo");
    IO.Print();

    Player player = Player.Generate(CreatureClass.Fighter, Race.Human);
    player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    Maps.Add(new RoomyMapGenerator().Generate());
    Maps[0].Creatures.Add(player);
    Maps[0].Creatures.Add(Creature.Generate(typeof(Fighter), 0, CreatureClass.Fighter, Race.Orc));
    Maps[0].Creatures[1].Position = Maps[0].FreeSpace();
    IO.Render(player);

    for(int y=0; y<Maps[0].Height; y++) // place player on the up staircase of the first level
      for(int x=0; x<Maps[0].Width; x++)
        if(Maps[0][x, y].Type==TileType.UpStairs) { player.X = x; player.Y = y; break; }

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