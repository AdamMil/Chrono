using System;
using System.Collections;

namespace Chrono
{

public sealed class App
{ 
  public class LevelCollection : ArrayList
  { public new Level this[int i] { get { return (Level)base[i]; } }
  }
  
  public static int CurrentLevel;
  public static InputOutput IO;
  public static LevelCollection Levels = new LevelCollection();
  public static bool Quit;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.01");
    IO.Print("Chrono 0.01 by Adam Milazzo");
    IO.Print();

    Player player = (Player)Creature.MakeCreature(typeof(Player));
    player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    player.Title = "Grunt";
    player.MaxHP = 1;
    Levels.Add(new RoomyLevelGenerator().Generate());
    Levels[0].Creatures.Add(player);
    IO.Render(player);

    for(int y=0; y<Levels[0].Height; y++) // place player on the up staircase of the first level
      for(int x=0; x<Levels[0].Width; x++)
        if(Levels[0].Map[x, y].Type==TileType.UpStairs) { player.X = x; player.Y = y; break; }

    while(!Quit)
    { if(CurrentLevel>0) Levels[CurrentLevel-1].Simulate();
      if(CurrentLevel<Levels.Count-1) Levels[CurrentLevel+1].Simulate();
      Levels[CurrentLevel].Simulate();
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