using System;
using System.IO;

namespace Chrono
{

[Serializable]
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
  { if(mi!=0)
    { MapGenerator lg = (mi/5&1)==0 ? (MapGenerator)new RoomyMapGenerator() : (MapGenerator)new MetaCaveGenerator();
      Map map;
      maps[mi] = map = lg.Generate();
      map.Index = mi;

      int max = map.Width*map.Height/250, min = map.Width*map.Height/500;

      for(int i=0,num=Global.Rand(max-min)+min+2; i<num; i++) map.SpawnItem();
      for(int i=0,num=Global.Rand(max-min)+min; i<num; i++) map.SpawnMonster();

      for(int i=0; i<map.Links.Length; i++) map.Links[i].ToLevel = map.Links[i].Down ? mi+1 : mi-1;
    }
    else
    { FileStream f = File.Open("Overworld.txt", FileMode.Open, FileAccess.Read);
      maps[mi] = Map.Load(f);
      f.Close();
    }
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