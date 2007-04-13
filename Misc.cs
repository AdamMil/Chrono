using System;
using System.Collections;
using System.Reflection;
using System.Xml;

using Point=System.Drawing.Point;

namespace Chrono
{

#region Attributes
[AttributeUsage(AttributeTargets.Class)]
public sealed class NoCloneAttribute : Attribute { }
#endregion

#region Enums
public enum Direction : byte
{ Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft,
  Above, Below, Self, Invalid
};
#endregion

#region EmptyEnumerator
public sealed class EmptyEnumerator : IEnumerator
{ public object Current { get { throw new InvalidOperationException(); } }
  public bool MoveNext() { return false; }
  public void Reset() { }
  
  public static readonly EmptyEnumerator Instance = new EmptyEnumerator();
}
#endregion

#region EntityGroup
public sealed class EntityGroup
{ public EntityGroup(XmlNode node)
  { ArrayList list = new ArrayList();
    if(node.LocalName=="spawns")
    { foreach(XmlNode child in node.ChildNodes)
        if(child.NodeType==XmlNodeType.Element)
          list.Add(new Member(child.Attributes["group"]!=null ? (object)Global.GetEntityGroup(Xml.Attr(child, "group"))
                                                              : Global.GetEntityIndex(Xml.Attr(child, "entity")),
                              Xml.Float(child, "chance")));
    }
    else    
      foreach(XmlNode child in node.ChildNodes)
        if(child.NodeType==XmlNodeType.Element)
          switch(child.LocalName)
          { case "entity":
              if(child.Attributes["name"]!=null) list.Add(new Member(Global.GetEntityIndex(Xml.Attr(child, "name"))));
              break;
            case "ref":
              list.Add(new Member(Global.GetEntityIndex(Xml.Attr(child, "name")), Xml.Float(child, "chance")));
              break;
            case "group":
              list.Add(new Member(Global.GetEntityGroup(Xml.Attr(child, "name")), Xml.Float(child, "chance")));
              break;
          }
    
    string name = Xml.Attr(node, "name");

    Members = (Member[])list.ToArray(typeof(Member));
    if(Members.Length==0) throw new ArgumentException("EntityGroup "+name+" contains no members!");

    float chance=0;
    int count=0;
    for(int i=0; i<Members.Length; i++) if(Members[i].Chance!=0) { chance+=Members[i].Chance; count++; }
    if(chance>1)
      throw new ArgumentException("EntityGroup "+name+"'s chances add up to more than 100%");
    if(count!=Members.Length)
    { chance = (1-chance)/(Members.Length-count);
      for(int i=0; i<Members.Length; i++) if(Members[i].Chance==0) Members[i].Chance=chance;
    }
    else if(chance<1)
    { chance = 1/chance;
      for(int i=0; i<Members.Length; i++) Members[i].Chance *= chance;
    }
  }

  public int NextEntity()
  { float num = (float)Global.RandDouble();

    do
    { num -= Members[index].Chance;
      if(++index==Members.Length) index=0;
    } while(num>0);

    object obj = Members[(index==0 ? Members.Length : index) - 1].Ref;
    return obj is int ? (int)obj : ((EntityGroup)obj).NextEntity();
  }

  struct Member
  { public Member(int entityIndex) { Ref=entityIndex; Chance=Global.GetEntityClass(entityIndex).SpawnChance/10000f; }
    public Member(object oref, float chance) { Ref=oref; Chance=chance/100; }
    public object Ref;
    public float Chance; // 0-1
  }

  Member[] Members;
  int index;
}
#endregion

#region Comparers
public sealed class ItemComparer
{ ItemComparer() { }

  public static readonly IComparer ByChar=new ByCharComparer(), ByCharGoldFirst=new ByCharGoldFirstComparer(),
                                   ByTypeAndChar = new ByTypeAndCharComparer();

  #region ByChar
  public sealed class ByCharComparer : IComparer
  { public int Compare(object x, object y) { return core((Item)x, (Item)y); }

    internal static int core(Item x, Item y)
    { char ac=x.Char, bc=y.Char;

      // handle non-letters specially (they come before letters)
      if(!char.IsLetter(ac)) return char.IsLetter(bc) ? -1 : ac-bc;
      else if(!char.IsLetter(bc)) return 1;

      int cmp = ac-bc; // difference between two letters
      // if it's >=26 apart, we're comparing uppercase to lowercase, which is the opposite of what we want
      if(cmp>=26 || cmp<=-26) return -cmp;
      // otherwise, it may still be the case that we're comparing uppercase to lowercase (eg, 'Z' and 'a')
      return char.IsLower(ac)==char.IsLower(bc) ? cmp : -cmp;
    }
  }
  #endregion

  #region ByCharGoldFirst
  public sealed class ByCharGoldFirstComparer : IComparer
  { public int Compare(object x, object y)
    { Item ix=(Item)x, iy=(Item)y;

      // put gold first no matter what
      if(ix.Type==ItemType.Gold)
      { if(iy.Type!=ItemType.Gold) return -1;
      }
      else if(iy.Type==ItemType.Gold) return 1;
      
      return ByCharComparer.core(ix, iy); // otherwise just sort by character
    }
  }
  #endregion

  #region ByTypeAndChar
  public sealed class ByTypeAndCharComparer : IComparer
  { public int Compare(object x, object y)
    { Item ix=(Item)x, iy=(Item)y;
      int cmp = (int)ix.Type-(int)iy.Type;
      return cmp==0 ? ByCharComparer.core(ix, iy) : cmp;
    }
  }
  #endregion
}
#endregion

#region Global
public sealed class Global
{ Global() { }

  static Global()
  { LoadEntities();
    LoadItems();
  }

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

  public static bool Coinflip() { return Random.Next(100)<50; }

  public static EntityClass GetEntityClass(int index) { return entities[index]; }

  public static EntityGroup GetEntityGroup(string name)
  { EntityGroup eg = (EntityGroup)entityGroups[name];
    if(eg==null) throw new ArgumentException("No such entity group: "+name);
    return eg;
  }

  public static int GetEntityIndex(string name)
  { object index = entityNames[name];
    if(index==null) throw new ArgumentException("No such entity: "+name);
    return (int)index;
  }

  public static ItemClass GetItemClass(int index) { return itemClasses[index]; }

  public static int GetItemCount(ItemType type)
  { return ((int)type==itemTypeOffsets.Length-1 ? itemClasses.Length : itemTypeOffsets[(int)type+1]) -
           itemTypeOffsets[(int)type];
  }

  public static int GetItemIndex(ItemType type, int index) { return itemTypeOffsets[(int)type] + index; }

  public static int GetItemIndex(string name)
  { object index = itemNames[name];
    if(index==null) throw new ArgumentException("No such item: "+name);
    return (int)index;
  }

  public static int GetItemIndex(ItemType type, string name)
  { return GetItemIndex(type.ToString().ToLower()+"/"+name);
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

  // these only accept planar directions (ie, not self, up, down, or invalid)
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

  public static Direction OffsetToDir(Point offset)
  { for(int i=0; i<8; i++) if(DirMap[i]==offset) return (Direction)i;
    return Direction.Invalid;
  }

  public static bool OneIn(int n) { return Random.Next(n)==0; }

  public static int Rand(int min, int max) { return Random.Next(min, max+1); }
  public static int Rand(int max) { return Random.Next(max); }
  public static double RandDouble() { return Random.NextDouble(); }

  public static int RandItem()
  { if(itemClasses.Length==0) throw new InvalidOperationException("No items!");
    int left = Global.Rand(itemTotalChance);
    while(true)
    { ItemClass ic = itemClasses[itemIndex];
      if(++itemIndex==itemClasses.Length) itemIndex = 0;
      left -= ic.SpawnChance;
      if(left<0) return ic.Index;
    }
  }

  public static int RandItem(ItemType itemType)
  { if(itemTotalChances[(int)itemType]==0) throw new InvalidOperationException("No items of type: "+itemType);
    int offset = itemTypeOffsets[(int)itemType], count = GetItemCount(itemType),
          left = Global.Rand(itemTotalChances[(int)itemType]), index = itemIndexes[(int)itemType];
    while(true)
    { ItemClass ic = itemClasses[index+offset];
      if(++index==count) index = 0;
      left -= ic.SpawnChance;
      if(left<0)
      { itemIndexes[(int)itemType] = index;
        return ic.Index;
      }
    }
  }

  // if stopAtDest is true, the trace is not allowed to bounce
  public static TraceResult TraceLine(Point start, Point dest, int maxDist, bool stopAtDest,
                                      TracePoint func, object context)
  { int dx=dest.X-start.X, dy=dest.Y-start.Y, xi=Math.Sign(dx), yi=Math.Sign(dy), r, ru, p, dist=0;
    Point op = start;
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
          return new TraceResult(op, start);
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
          return new TraceResult(op, start);
        switch(ta)
        { case TraceAction.Go: op=start; break;
          case TraceAction.HBounce: xi=-xi; while(p<=0) p += r; break;
          case TraceAction.VBounce: yi=-yi; break;
          case TraceAction.Bounce: xi=-xi; yi=-yi; break;
        }
      }
    }
  }

  public static string WithAorAn(string str) { return char.IsDigit(str[0]) ? str : AorAn(str) + " " + str; }

  public static readonly Point[] DirMap = new Point[8]
  { new Point(0, -1), new Point(1, -1), new Point(1, 0),  new Point(1, 1),
    new Point(0, 1),  new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
  };

  struct ItemIndex
  { public ItemIndex(ItemType type, int index) { Type=type; Index=index; }
    public ItemType Type;
    public int Index;
  }

  static void LoadEntities()
  { ArrayList classes = new ArrayList();
    Hashtable names = new Hashtable();

    foreach(Type type in Assembly.GetExecutingAssembly().GetTypes())
      if(!type.IsAbstract && type.IsSubclassOf(typeof(EntityClass)))
      { ConstructorInfo ci = type.GetConstructor(Type.EmptyTypes);
        if(ci!=null)
        { string name = type.Name;
          if(name.EndsWith("Class")) name = name.Substring(0, name.Length-5);
          names["builtin/"+name] = classes.Add(ci.Invoke(null));
        }
      }

    XmlDocument doc = LoadXml("entities.xml");

    Hashtable idcache = new Hashtable();

    foreach(XmlNode node in doc.SelectNodes("//entity[@fullName]"))
      names[node.Attributes["fullName"].Value] = classes.Add(XmlEntityClass.Make(node, idcache));

    foreach(XmlNode node in doc.SelectNodes("//entity[@name]"))
      if(node.Attributes["fullName"]==null)
        names[node.Attributes["name"].Value] = classes.Add(XmlEntityClass.Make(node, idcache));

    entities = (EntityClass[])classes.ToArray(typeof(EntityClass));

    entityNames = new SortedList(names);

    foreach(XmlNode node in doc.SelectNodes("//entityGroup"))
      entityGroups[node.Attributes["name"].Value] = new EntityGroup(node);
  }
  
  static void LoadItems()
  { ArrayList[] lists = new ArrayList[(int)ItemType.NumTypes];
    Hashtable names = new Hashtable();

    foreach(Type type in Assembly.GetExecutingAssembly().GetTypes())
      if(!type.IsAbstract && type.IsSubclassOf(typeof(ItemClass)))
      { ConstructorInfo ci = type.GetConstructor(Type.EmptyTypes);
        if(ci!=null)
        { ItemClass ic = (ItemClass)ci.Invoke(null);
          if(lists[(int)ic.Type]==null) lists[(int)ic.Type] = new ArrayList();
          names["builtin/"+type.Name] = new ItemIndex(ic.Type, lists[(int)ic.Type].Add(ic));
        }
      }

    XmlDocument doc = LoadXml("items.xml");
    foreach(XmlNode node in doc.DocumentElement.ChildNodes)
      if(node.NodeType==XmlNodeType.Element && node.LocalName!="randomNames")
      { ItemClass ic = ItemClass.FromXml(node);
        if(lists[(int)ic.Type]==null) lists[(int)ic.Type] = new ArrayList();
        names[ic.Type.ToString().ToLower()+"/"+node.Attributes["name"].Value]
          = new ItemIndex(ic.Type, lists[(int)ic.Type].Add(ic));
      }

    itemClasses = new ItemClass[names.Count];
    for(int i=0, offset=0; i<lists.Length; i++)
    { itemTypeOffsets[i] = offset;
      if(lists[i]!=null)
      { lists[i].CopyTo(itemClasses, offset);
        int total = 0;
        for(int j=0,count=lists[i].Count; j<count; j++) total += itemClasses[offset+j].SpawnChance;
        itemTotalChances[i] = total;
        offset += lists[i].Count;
      }
    }
    
    for(int i=0; i<(int)ItemType.NumTypes; i++) itemTotalChance += itemTotalChances[i];

    for(int i=0; i<itemClasses.Length; i++) itemClasses[i].Index = i; // set all the ItemClass.Index values
    
    ArrayList keys = new ArrayList(names.Keys);
    foreach(string key in keys)
    { ItemIndex ii = (ItemIndex)names[key];
      names[key] = GetItemIndex(ii.Type, ii.Index);
    }

    itemNames = new SortedList(names);
  }

  static Random Random = new Random();
  
  static EntityClass[] entities;
  static ItemClass[] itemClasses;
  static int[] itemTypeOffsets = new int[(int)ItemType.NumTypes],
                   itemIndexes = new int[(int)ItemType.NumTypes],
              itemTotalChances = new int[(int)ItemType.NumTypes];
  static SortedList entityNames, itemNames, entityGroups=new SortedList();
  static int itemIndex, itemTotalChance;
}
#endregion

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

  public int RandValue() { return Dice ? Global.NdN(L, R) : L==R ? L : Global.Rand(L, R); }

  public int L, R;
  public bool Dice;
}
#endregion

#region Tracing
[Flags] public enum TraceAction { Stop=0, Go=1, HBounce=2, VBounce=4, Bounce=HBounce|VBounce };

public delegate TraceAction TracePoint(Point point, object context);

public struct TraceResult
{ public TraceResult(Point start, Point end) { Start=start; End=end; }
  public Point Start, End;
}
#endregion

} // namespace Chrono