using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;

namespace Chrono
{

public abstract class MapCollection : UniqueObject
{ protected MapCollection() { }
  protected MapCollection(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public Map this[int i]
  { get
    { if(i>=maps.Count)
        for(int mi=maps.Count; mi<=i; mi++)
        { Map m = Generate(mi);
          m.Dungeon = this;
          m.Index   = mi;
          m.OnInit();
          for(int j=0; j<m.Links.Length; j++) if(m.Links[j].ToDungeon==null) m.Links[j].ToDungeon=this;
          maps.Add(m);
        }
      return (Map)maps[i];
    }
  }

  public int Count { get { return maps.Count; } }
  public virtual string GetName(int index) { return null; }

  protected abstract Map Generate(int mi);

  ArrayList maps = new ArrayList(8);
}

[Serializable]
public class Overworld : MapCollection
{ public Overworld() { }
  public Overworld(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public enum Place { Overworld, GTown, FTown, ITown, MTown }

  protected override Map Generate(int mi)
  { Map map;
    switch((Place)mi)
    { case Place.Overworld:
      { FileStream f = File.Open("Overworld.txt", FileMode.Open, FileAccess.Read);
        map = Map.Load(f);
        f.Close();

        AddLink(map, TileType.Grass, Place.GTown);
        AddLink(map, TileType.Forest, Place.FTown);
        AddLink(map, TileType.Mountain, Place.MTown);
        AddLink(map, TileType.Ice, Place.ITown);
        break;
      }

      case Place.GTown: case Place.FTown: case Place.ITown: case Place.MTown:
      { TownGenerator tg = new TownGenerator();
        tg.Generate(map = new Map(tg.DefaultSize));
        break;
      }
      
      default: throw new ArgumentOutOfRangeException("mi", mi, "No such place!");
    }

    return map;
  }

  public override string GetName(int index) { return index==0 ? "Drogea" : ((Place)index).ToString(); }

  void AddLink(Map map, TileType type, Place place)
  { Point pt = FreeSpace(map, type);
    map.AddLink(new Link(pt, this, (int)place, true));
    map.SetType(pt, TileType.Town);
  }

  void AddLink(Map map, TileType type, MapCollection dungeon)
  { Point pt = FreeSpace(map, type);
    map.AddLink(new Link(pt, dungeon, 0, true));
    map.SetType(pt, TileType.Town); // TODO: change this to something else (Cave, Tower, etc)
  }

  Point FreeSpace(Map map, TileType type)
  { Point pt;
    do pt=map.FreeSpace(Map.Space.NoLinks); while(map[pt].Type!=type);
    return pt;
  }
}

[Serializable]
public class TestDungeon : MapCollection
{ public TestDungeon() { }
  public TestDungeon(SerializationInfo info, StreamingContext context) : base(info, context) { }

  protected override Map Generate(int mi)
  { MapGenerator lg = (mi/5&1)==0 ? (MapGenerator)new RoomyMapGenerator() : (MapGenerator)new MetaCaveGenerator();
    Map map = new TestMap(lg.DefaultSize);
    lg.Generate(map);
    map.IsDungeon = true;
    for(int i=0; i<map.Links.Length; i++) map.Links[i].ToLevel = map.Links[i].Down ? mi+1 : mi-1;
    return map;
  }
}

} // namespace Chrono