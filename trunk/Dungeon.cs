using System;
using System.Collections;
using System.Collections.Specialized;
using System.Xml;
using System.Runtime.Serialization;

namespace Chrono
{

[Serializable]
public class Dungeon
{ public Dungeon(string path) : this(LoadDungeon(path)) { }
  public Dungeon(XmlElement dungeon) { node=dungeon; }

  #region Section
  [Serializable]
  public class Section : UniqueObject
  { public Section(XmlNode section, Dungeon dungeon)
    { node=section; this.dungeon=dungeon;
      foreach(XmlNode part in node.SelectNodes("levels")) // convert depth ranges to constant values
        part.Attributes["depth"].Value = Xml.RangeInt(part.Attributes["depth"].Value).ToString();
    }
    public Section(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public Map this[int index]
    { get
      { if(index>=maps.Count)
        { if(index>=Depth) throw new ArgumentOutOfRangeException("index");
          for(int mi=maps.Count; mi<=index; mi++) AddMap(mi);
        }
        return (Map)maps[index];
      }
    }

    public int Count { get { return maps.Count; } }

    public int Depth
    { get
      { int depth = 0;
        foreach(XmlNode part in node.SelectNodes("levels")) depth += int.Parse(part.Attributes["depth"].Value);
        return depth;
      }
    }
    
    public Section Next
    { get
      { XmlNode next = node.NextSibling;
        return next!=null && next.LocalName=="section" ? dungeon[next.Attributes["name"].Value] : null;
      }
    }

    public Section Previous
    { get
      { XmlNode prev = node.PreviousSibling;
        return prev!=null && prev.LocalName=="section" ? dungeon[prev.Attributes["name"].Value] : null;
      }
    }

    public Dungeon Dungeon { get { return dungeon; } }

    Map AddMap(int index)
    { XmlNode levels = null;
      int mi = index;

      foreach(XmlNode part in node.SelectNodes("levels")) // find the "levels" node corresponding to this index
      { int depth = int.Parse(part.Attributes["depth"].Value);
        if(depth>mi) { levels=part; break; }
        mi -= depth;
      }

      XmlAttribute attr = levels.Attributes["map"];
      if(attr==null) attr = node.Attributes["map"];
      if(attr==null) attr = node.Attributes["name"];
      string mapName = attr.Value;
      
      Map map = Map.Load(mapName, this, index);
      maps.Add(map);
      return map;
    }

    ArrayList maps = new ArrayList(8);
    Dungeon dungeon;
    XmlNode node;
  }
  #endregion
  
  public Section this[string name]
  { get
    { Section section = (Section)sections[name];
      if(section==null)
        sections[name] = section = new Section(node.SelectSingleNode("section[@name='"+name+"']"), this);
      return section;
    }
  }
  
  public string Name { get { return node.Attributes["name"].Value; } }

  public string StartSection
  { get
    { XmlAttribute start = node.Attributes["start"];
      return (start==null ? node.SelectSingleNode("section").Attributes["name"] : start).Value;
    }
  }

  XmlElement node;
  HybridDictionary sections = new HybridDictionary();
  
  public static Dungeon GetDungeon(string name)
  { Dungeon d = (Dungeon)dungeons[name];
    if(d==null) dungeons[name] = d = new Dungeon(name);
    return d;
  }

  public static void Deserialize(System.IO.Stream stream, IFormatter formatter)
  { dungeons = (HybridDictionary)formatter.Deserialize(stream);
  }
  public static void Serialize(System.IO.Stream stream, IFormatter formatter)
  { formatter.Serialize(stream, dungeons);
  }

  static XmlElement LoadDungeon(string path)
  { if(path.IndexOf('/')==-1) path = "dungeon/"+path;
    if(path.IndexOf('.')==-1) path += ".xml";
    return Global.LoadXml(path).DocumentElement;
  }
  
  static HybridDictionary dungeons = new HybridDictionary();
}

} // namespace Chrono