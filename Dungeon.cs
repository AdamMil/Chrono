using System;
using System.Collections.Generic;
using System.Xml;

namespace Chrono
{

public sealed class Dungeon
{
  public Dungeon(string path) : this(LoadDungeon(path)) { }
  public Dungeon(XmlElement dungeon) { node=dungeon; }

  #region Section
  public sealed class Section
  {
    public Section(XmlNode section, Dungeon dungeon)
    {
      this.node    = section;
      this.dungeon = dungeon;
      foreach(XmlNode part in node.SelectNodes("levels")) // convert depth ranges to constant values
      {
        part.Attributes["depth"].Value = Xml.RangeInt(part.Attributes["depth"].Value).ToString();
      }
    }

    public Map this[int index]
    {
      get
      {
        if(index>=maps.Count)
        {
          if(index>=Depth) throw new ArgumentOutOfRangeException("index");
          for(int mi=maps.Count; mi<=index; mi++) AddMap(mi);
        }
        return (Map)maps[index];
      }
    }

    public int Count { get { return maps.Count; } }

    public int Depth
    {
      get
      {
        int depth = 0;
        foreach(XmlNode part in node.SelectNodes("levels")) depth += int.Parse(part.Attributes["depth"].Value);
        return depth;
      }
    }

    public string Name { get { return Xml.Attr(node, "name", Dungeon.Name); } }

    public Section Next
    {
      get
      {
        XmlNode next = node.NextSibling;
        return next!=null && next.LocalName=="section" ? dungeon[next.Attributes["id"].Value] : null;
      }
    }

    public Section Previous
    {
      get
      {
        XmlNode prev = node.PreviousSibling;
        return prev!=null && prev.LocalName=="section" ? dungeon[prev.Attributes["id"].Value] : null;
      }
    }

    public Dungeon Dungeon { get { return dungeon; } }

    Map AddMap(int index)
    {
      XmlNode levels = null;
      int mi = index;

      foreach(XmlNode part in node.SelectNodes("levels")) // find the "levels" node corresponding to this index
      {
        int depth = int.Parse(part.Attributes["depth"].Value);
        if(depth>mi) { levels=part; break; }
        mi -= depth;
      }

      XmlAttribute attr = levels.Attributes["map"];
      if(attr==null) attr = node.Attributes["map"];
      if(attr==null) attr = node.Attributes["id"];
      string mapName = attr.Value;

      Map map = Map.Load(mapName, this, index);
      maps.Add(map);
      return map;
    }

    List<Map> maps = new List<Map>();
    Dungeon dungeon;
    XmlNode node;
  }
  #endregion

  public Section this[string name]
  {
    get
    {
      Section section = (Section)sections[name];
      if(section==null)
        sections[name] = section = new Section(node.SelectSingleNode("section[@id='"+name+"']"), this);
      return section;
    }
  }

  public string Name { get { return node.Attributes["name"].Value; } }

  public string StartSection
  {
    get
    {
      XmlAttribute start = node.Attributes["start"];
      return (start==null ? node.SelectSingleNode("section").Attributes["id"] : start).Value;
    }
  }

  XmlElement node;
  Dictionary<string,Section> sections = new Dictionary<string,Section>();

  public static Dungeon GetDungeon(string name)
  {
    Dungeon d = (Dungeon)dungeons[name];
    if(d==null) dungeons[name] = d = new Dungeon(name);
    return d;
  }

  static XmlElement LoadDungeon(string path)
  {
    if(path.IndexOf('/')==-1) path = "dungeon/"+path;
    if(path.IndexOf('.')==-1) path += ".xml";
    return Global.LoadXml(path).DocumentElement;
  }

  static Dictionary<string,Dungeon> dungeons = new Dictionary<string,Dungeon>();
}

} // namespace Chrono