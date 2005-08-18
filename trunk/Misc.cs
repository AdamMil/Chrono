using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Xml;

namespace Chrono
{

[AttributeUsage(AttributeTargets.Class)]
public sealed class NoCloneAttribute : Attribute { }

public sealed class EmptyEnumerator : IEnumerator
{ public object Current { get { throw new InvalidOperationException(); } }
  public bool MoveNext() { return false; }
  public void Reset() { }
}

public enum Direction : byte
{ Up=0, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft,
  Above, Below, Self, Invalid
};

public enum TraceAction { Stop=1, Go=2, HBounce=4, VBounce=8, Bounce=HBounce|VBounce };
public struct TraceResult
{ public TraceResult(Point pt, Point prev) { Point=pt; Previous=prev; }
  public Point Point;
  public Point Previous;
}
public delegate TraceAction LinePoint(Point point, object context);

public class UniqueObject
{ public UniqueObject() { ID=Global.NextID; }
  public ulong ID;
}

#region Range
public struct Range
{ public Range(int num) { L=R=num; Dice=false; }
  public Range(int min, int max) { L=min; R=max; Dice=false; }
  public Range(int lhs, int rhs, bool dice) { L=lhs; R=rhs; Dice=dice; }
  public Range(XmlNode node, string attr) : this(node.Attributes[attr], 0, 0) { }
  public Range(XmlNode node, string attr, int defaultValue)
    : this(node.Attributes[attr], defaultValue, defaultValue) { }
  public Range(XmlNode node, string attr, int min, int max) : this(node.Attributes[attr], min, max) { }
  public Range(XmlAttribute attr) : this(attr, 0, 0) { }
  public Range(XmlAttribute attr, int defaultValue)
    : this(attr==null ? null : attr.Value, defaultValue, defaultValue) { }
  public Range(XmlAttribute attr, int min, int max) : this(attr==null ? null : attr.Value, min, max) { }
  public Range(string range) : this(range, 0, 0) { }
  public Range(string range, int defaultValue) : this(range, defaultValue, defaultValue) { }
  public Range(string range, int min, int max)
  { Dice=false;
    if(range==null || range=="") { L=min; R=max; }
    else
    { int pos = range.IndexOf(':');
      if(pos!=-1)
      { L=int.Parse(range.Substring(0, pos));
        R=int.Parse(range.Substring(pos+1));
        return;
      }

      pos = range.IndexOf('d');
      if(pos!=-1)
      { L=int.Parse(range.Substring(0, pos));
        R=int.Parse(range.Substring(pos+1));
        Dice=true;
        return;
      }
      
      L=R=int.Parse(range);
    }
  }

  public int RandValue() { return Dice ? Global.NdN(L, R) : Global.Rand(L, R); }

  public int L, R;
  public bool Dice;
}
#endregion

#region SocialGroup
public struct SocialGroup
{ public SocialGroup(int id, bool hostile, bool permanent)
  { Entities=new ArrayList(); ID=id; Hostile=hostile; Permanent=permanent;
  }
  public ArrayList Entities;
  public int  ID;
  public bool Hostile, Permanent;
}
#endregion

#region SpawnInfo
public struct SpawnInfo
{ public SpawnInfo(Type t)
  { Item=t;

    BindingFlags flags = BindingFlags.FlattenHierarchy|BindingFlags.Static|BindingFlags.Public;
    FieldInfo f = t.GetField("SpawnChance", flags);
    Chance = (int)Math.Ceiling((float)f.GetValue(null)*100);

    f = t.GetField("SpawnMin", flags);
    int min = f==null ? 1 : (int)f.GetValue(null);

    f = t.GetField("SpawnMax", flags);
    int max = f==null ? min : (int)f.GetValue(null);

    f = t.GetField("ShopValue", flags);
    Value = f==null ? 0 : (int)f.GetValue(null);
    
    Count = new Range(min, max);
  }

  public SpawnInfo(XmlNode node)
  { Item   = node;
    Chance = (int)Math.Ceiling(Xml.FloatValue(node, "chance", 0)*100);
    Value  = Xml.IntValue(node, "value", 0);
    Count  = new Range(node, "spawn", 1);
  }

  public Item MakeItem()
  { Item item;

    if(Item is Type) item = (Item)((Type)Item).GetConstructor(Type.EmptyTypes).Invoke(null);
    else
    { XmlNode node = (XmlNode)Item;
      switch(node.LocalName)
      { case "armor": item=new XmlArmor(node); break;
        case "food": item=new XmlFood(node); break;
        case "ring": item=new XmlRing(node); break;
        case "scroll": item=new XmlScroll(node); break;
        case "shield": item=new XmlShield(node); break;
        case "tool": item=Xml.IsEmpty(node, "charges") ? new XmlTool(node) : (Item)new XmlChargedTool(node); break;
        case "wand": item=new XmlWand(node); break;
        default: throw new NotImplementedException("unknown xml item type: "+node.LocalName);
      }
    }

    item.Count = Count.RandValue();

    // (x/10)^0.5 where x=0 to 100, truncated towards 0. bonus = 0 to 3
    int bonus = (int)Math.Sqrt(Global.RandDouble()*10);
    if(Global.Rand(100)<15) { item.Curse(); bonus = -bonus; }
    else if(Global.Rand(100)<8) item.Bless();
    
    if(!item.Uncursed) // cursed or blessed
    { switch(item.Class)
      { case ItemClass.Armor: case ItemClass.Shield: ((Modifying)item).AC += bonus; break;
        case ItemClass.Spellbook:
        { Spellbook book = (Spellbook)item;
          book.Reads += bonus*2;
          if(book.Reads<1) book.Reads=1;
          break;
        }
        case ItemClass.Weapon:
        { Weapon w = (Weapon)item;
          if(Global.OneIn(10)) w.DamageMod = w.ToHitMod = bonus;
          else if(Global.Coinflip()) w.DamageMod = bonus;
          else w.ToHitMod = bonus;
          break;
        }
        default:
          if(item is Chargeable)
          { Chargeable c = (Chargeable)item;
            c.Charges += bonus + Math.Sign(bonus);
            if(c.Charges<0) c.Charges=0;
          }
          break;
      }
    }

    return item;
  }

  public object Item;
  public int Chance, Value;
  public Range Count;
}
#endregion

#region Global
public sealed class Global
{ private Global() { }

  static Global()
  { LoadSocialGroups();
    LoadItems();
    LoadEntities();
  }

  public static ulong NextID { get { return nextID++; } }

  #region Social groups
  public static void AddToSocialGroup(int id, Entity e)
  { socialGroups[id].Entities.Add(e);
  }

  public static void RemoveFromSocialGroup(int id, Entity e)
  { socialGroups[id].Entities.Remove(e);
    if(!socialGroups[id].Permanent && socialGroups[id].Entities.Count==0)
    { socialGroups[id].Entities = null;
      socialGroups[id].ID = -1;
    }
  }

  public static SocialGroup GetSocialGroup(int id) { return socialGroups[id]; }
  public static int GetSocialGroup(string name) { return (int)namedSocialGroups[name]; }

  public static int NewSocialGroup(bool hostile, bool permanent)
  { int i;
    for(i=0; i<numSocials; i++) if(socialGroups[i].ID==-1) break;

    if(i==numSocials)
    { SocialGroup[] narr = new SocialGroup[numSocials==0 ? 8 : numSocials*2];
      if(numSocials>0) Array.Copy(socialGroups, narr, numSocials);
      for(int j=numSocials; j<narr.Length; j++) narr[j].ID = -1;
      socialGroups = narr;
      numSocials++;
    }

    socialGroups[i] = new SocialGroup(i, hostile, permanent);
    return i;
  }

  public static void UpdateSocialGroup(int id, bool hostile) { socialGroups[id].Hostile = hostile; }
  #endregion

  public static string AorAn(string s)
  { char fc = char.ToLower(s[0]);
    if(fc=='a' || fc=='e' || fc=='i' || fc=='o' || fc=='u') return "an";
    else return "a";
  }

  public static string Cap1(string s)
  { if(s.Length==0) return s;
    string ret = char.ToUpper(s[0]).ToString();
    if(s.Length>1) ret += s.Substring(1);
    return ret;
  }

  public static object ChangeType(object value, Type type)
  { if(type.IsSubclassOf(typeof(Enum))) return Enum.Parse(type, value.ToString());
    return Convert.ChangeType(value, type);
  }

  public static bool Coinflip() { return Random.Next(100)<50; }

  public static void DeclareVar(string name, string value)
  { if(vars.Contains(name)) throw new ArgumentException("global variable "+name+" declared twice");
    vars[name] = value;
  }

  public static string GetVar(string name)
  { if(vars.Contains(name)) return (string)vars[name];
    throw new ArgumentException("variable "+name+" not declared");
  }

  public static System.IO.Stream LoadData(string path)
  { return System.IO.File.Open("../../data/"+path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
  }

  public static System.Xml.XmlDocument LoadXml(string path)
  { System.IO.Stream stream = LoadData(path);
    System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
    doc.Load(stream);
    stream.Close();
    return doc;
  }

  // these only accept cardinal directions (eg, not self, up, down, or invalid)
  public static Point Move(Point pt, Direction d) { return Move(pt, (int)d); }
  public static Point Move(Point pt, int d)
  { if(d<0) { d=d%8; if(d!=0) d+=8; }
    else if(d>7) d = d%8;
    pt.Offset(DirMap[d].X, DirMap[d].Y);
    return pt;
  }

  public static int NdN(int ndice, int nsides) // dice range from 1 to nsides, not 0 to nsides-1
  { int val=0;
    while(ndice-->0) { val += Random.Next(nsides)+1; }
    return val;
  }

  public static SpawnInfo NextSpawn()
  { int n = Random.Next(10000)+1, total=0;
    while(true)
    { total += objSpawns[spawnIndex].Chance;
      if(++spawnIndex==objSpawns.Length) spawnIndex=0;
      if(total>=n) return objSpawns[spawnIndex==0 ? objSpawns.Length-1 : spawnIndex-1];
    }
  }

  public static Direction PointToDir(Point off)
  { for(int i=0; i<8; i++) if(DirMap[i]==off) return (Direction)i;
    return Direction.Invalid;
  }

  public static bool OneIn(int n) { return Random.Next(n)==0; }

  public static int Rand(int min, int max) { return Random.Next(min, max+1); }
  public static int Rand(int max) { return Random.Next(max); }
  public static double RandDouble() { return Random.NextDouble(); }
  
  public static void Randomize(string[] names)
  { for(int i=0; i<names.Length; i++)
    { int j = Rand(names.Length);
      string t = names[i]; names[i] = names[j]; names[j] = t;
    }
  }
  public static void Randomize(string[] names, Color[] colors)
  { for(int i=0; i<names.Length; i++)
    { int j = Rand(names.Length);
      string n = names[i]; names[i] = names[j]; names[j] = n;
      Color  c = colors[i]; colors[i] = colors[j]; colors[j] = c;
    }
  }

  public static string SetVar(string name, string value)
  { if(vars.Contains(name)) vars[name]=value;
    throw new ArgumentException("variable "+name+" not declared");
  }

  // bouncing is incompatible with stopAtDest
  public static TraceResult TraceLine(Point start, Point dest, int maxDist, bool stopAtDest,
                                      LinePoint func, object context)
  { int dx=dest.X-start.X, dy=dest.Y-start.Y, xi=Math.Sign(dx), yi=Math.Sign(dy), r, ru, p, dist=0;
    Point op=start;
    TraceAction ta;
    if(dx<0) dx=-dx;
    if(dy<0) dy=-dy;
    if(dx>=dy)
    { r=dy*2; ru=r-dx*2; p=r-dx;
      while(true)
      { if(p>0) { start.Y+=yi; p+=ru; }
        else p+=r;
        start.X+=xi; dx--;
        ta = func(start, context);
        if(ta==TraceAction.Stop || maxDist!=-1 && ++dist>=maxDist || stopAtDest && dx<0)
          return new TraceResult(start, op);
        switch(ta)
        { case TraceAction.Go: op=start; break;
          case TraceAction.HBounce: xi=-xi; break;
          case TraceAction.VBounce: yi=-yi; while(p<=0) p += r; break;
          case TraceAction.Bounce: xi=-xi; yi=-yi; break;
        }
      }
    }
    else
    { r=dx*2; ru=r-dy*2; p=r-dy;
      while(true)
      { if(p>0) { start.X+=xi; p+=ru; }
        else p+=r;
        start.Y+=yi; dy--;
        ta = func(start, context);
        if(ta==TraceAction.Stop || maxDist!=-1 && ++dist>=maxDist || stopAtDest && dy<0)
          return new TraceResult(start, op);
        switch(ta)
        { case TraceAction.Go: op=start; break;
          case TraceAction.HBounce: xi=-xi; while(p<=0) p += r; break;
          case TraceAction.VBounce: yi=-yi; break;
          case TraceAction.Bounce: xi=-xi; yi=-yi; break;
        }
      }
    }
  }
  
  public static string WithAorAn(string str) { return char.IsDigit(str[0]) ? str : AorAn(str) + ' ' + str; }

  public static readonly Point[] DirMap = new Point[8]
  { new Point(0, -1), new Point(1, -1), new Point(1, 0),  new Point(1, 1),
    new Point(0, 1),  new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
  };
  
  public static Hashtable ObjHash;

  static void LoadItems()
  { ArrayList list = new ArrayList();
    Type[] types = Assembly.GetExecutingAssembly().GetTypes(); // build a table of items and their spawn chances
    foreach(Type t in types)
      if(!t.IsAbstract && t.IsSubclassOf(typeof(Item)))
      { SpawnInfo si = new SpawnInfo(t);
        if(si.Chance!=0) list.Add(si);
      }

    foreach(XmlNode node in items.DocumentElement.ChildNodes)
    { SpawnInfo si = new SpawnInfo(node);
      if(si.Chance!=0) list.Add(si);
    }

    objSpawns = (SpawnInfo[])list.ToArray(typeof(SpawnInfo));
  }

  static void LoadSocialGroups()
  { namedSocialGroups["player"] = NewSocialGroup(false, true);
    namedSocialGroups["enemy"]  = NewSocialGroup(true, true);
    foreach(System.Xml.XmlNode group in LoadXml("ai/socialGroups.xml").SelectNodes("//groups"))
      namedSocialGroups[Xml.Attr(group, "name")] =
        NewSocialGroup(Xml.IsTrue(group.Attributes["hostile"]), Xml.IsTrue(group.Attributes["permanent"]));
  }

  static readonly XmlDocument entities=LoadXml("entities.xml"), items=LoadXml("items.xml");
  static Hashtable vars=new Hashtable(), namedSocialGroups=new Hashtable();
  static SpawnInfo[] objSpawns;
  static SocialGroup[] socialGroups;
  static Random Random = new Random();
  static ulong nextID=1;
  static int spawnIndex, numSocials;
}
#endregion

} // namespace Chrono
