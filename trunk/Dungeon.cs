using System;

namespace Chrono
{

public class Dungeon
{ public Map this[int i]
  { get
    { if(i>=numMaps)
      { ResizeTo(i+1);
        for(; numMaps<=i; numMaps++) Generate(numMaps);
      }
      return maps[i];
    }
  }
  
  public int Count { get { return numMaps; } }

  void Generate(int mi)
  { Map map;
    maps[mi] = map = new RoomyMapGenerator().Generate();
    map.Index = mi;

    for(int i=0; i<15; i++) map.SpawnMonster();
    for(int i=0; i<2; i++) map.AddItem(map.FreeSpace(true, true), new Hamburger());
    for(int i=0; i<3; i++) map.AddItem(map.FreeSpace(true, true), new FortuneCookie());
    if(Global.Coinflip()) map.AddItem(map.FreeSpace(true, true), new HealPotion());
    else map.AddItem(map.FreeSpace(true, true), new TeleportScroll());

    for(int i=0; i<map.Links.Length; i++) map.Links[i].ToLevel = map.Links[i].Down ? mi+1 : mi-1;
  }

  void ResizeTo(int size)
  { int len = maps.Length;
    if(size>len)
    { do len*=2; while(size>len);
      Map[] narr = new Map[len];
      Array.Copy(maps, narr, maps.Length);
      maps = narr;
    }
  }

  Map[] maps = new Map[8];
  int numMaps;
}

} // namespace Chrono