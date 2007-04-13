using System;

namespace Chrono
{

public sealed class App
{ App() { }

  public static Dungeon World = new Dungeon("overworld");
  public static Player Player;
  public static InputOutput IO;
  public static bool IsQuitting;

  public static void Main()
  { IO = new ConsoleIO();
    IO.SetTitle("Chrono 0.05");
    IO.Print("Chrono 0.05 by Adam Milazzo");
    IO.Print();

    Player = new Player("wizard");
    Player.Name = IO.Ask("Enter your name:", false, "I need to know what to call you!");
    Player.OriginalRace = Race.Human;

    foreach(Attr attr in Enum.GetValues(typeof(Attr)))
      if(attr>=0 && attr<Attr.NumAttributes) Player.SetBaseAttr(attr, 10);

    Player.Pickup(new Item("builtin/ShortSword"));
    Player.MemorizeSpell(ForceBoltSpell.Instance, 5000);
    Player.Pickup(new Item("food/hamburger")).Count = 2;
    Player.Pickup(new Item("scroll/teleport"));
    Player.Pickup(new Item("builtin/Gold")).Count = 100;

    foreach(Item i in Player.Inv)
    { Player.AddKnowledge(i.Class);
      i.Identify();
      i.Uncurse();
    }

    Map map = World[World.StartSection][0];
    map.Entities.Add(Player);
    Player.Pos = map.FreeSpaceNear(map.GetEntity("Pa").Pos);

    IO.Render(Player);
    while(!IsQuitting)
    { try { Player.Map.Simulate(); }
      catch(Exception e) { App.IO.Print("{0} occurred: {1}", e.GetType().Name, e.Message); }
    }
    
    if(Player.HP>0) Save();
  }

  public static void NotImplemented() { IO.Print("NOT IMPLEMENTED"); }

  static void Save() { throw new NotImplementedException(); }
}

} // namespace Chrono