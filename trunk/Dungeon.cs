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
  
  public enum Place { Overworld, Town1, Town2, Town3, Town4 }

  protected override Map Generate(int mi)
  { Map map;
    switch((Place)mi)
    { case Place.Overworld:
      { FileStream f = File.Open("Overworld.txt", FileMode.Open, FileAccess.Read);
        map = Map.Load(f);
        f.Close();

        AddLink(map, TileType.Grass, Place.Town1);
        AddLink(map, TileType.Forest, Place.Town2);
        AddLink(map, TileType.Mountain, Place.Town3);
        AddLink(map, TileType.Ice, Place.Town4);
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