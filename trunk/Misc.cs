using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

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

[Serializable]
public sealed class ObjectProxy : ISerializable, IObjectReference
{ public ObjectProxy(SerializationInfo info, StreamingContext context) { ID=info.GetUInt64("ID"); }
  public void GetObjectData(SerializationInfo info, StreamingContext context) { } // never called
  public object GetRealObject(StreamingContext context) { return Global.ObjHash[ID]; }
  ulong ID;
}

public class UniqueObject : ISerializable
{ public UniqueObject() { ID=Global.NextID; }
  protected UniqueObject(SerializationInfo info, StreamingContext context)
  { Type t = GetType();
    do // private fields are not inherited, so we traverse the class hierarchy ourselves
    { foreach(FieldInfo f in t.GetFields(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        if(!f.IsNotSerialized) f.SetValue(this, info.GetValue(f.Name, f.FieldType));
      t = t.BaseType;
    } while(t!=null);

    if(Global.ObjHash!=null) Global.ObjHash[ID] = this;
  }
  public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
  { if(Global.ObjHash!=null && Global.ObjHash.Contains(ID))
    { info.AddValue("ID", ID);
      info.SetType(typeof(ObjectProxy));
    }
    else
    { if(Global.ObjHash!=null) Global.ObjHash[ID]=this;
      
      Type t = GetType();
      do // private fields are not inherited, so we traverse the class hierarchy ourselves
      { foreach(FieldInfo f in t.GetFields(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
          if(!f.IsNotSerialized) info.AddValue(f.Name, f.GetValue(this));
        t = t.BaseType;
      } while(t!=null);
    }
  }
  
  public ulong ID;
}

[Serializable]
public struct SocialGroup
{ public SocialGroup(int id, bool hostile, bool permanent)
  { Entities=new ArrayList(); ID=id; Hostile=hostile; Permanent=permanent;
  }
  public ArrayList Entities;
  public int  ID;
  public bool Hostile, Permanent;
}

public struct SpawnInfo
{ public SpawnInfo(Type t)
  { ItemType=t;
  
    BindingFlags flags = BindingFlags.FlattenHierarchy|BindingFlags.Static|BindingFlags.Public;
    FieldInfo f = t.GetField("SpawnChance", flags);
    SpawnChance = (int)f.GetValue(null);

    f = t.GetField("SpawnMin", flags);
    SpawnMin = f==null ? 1 : (int)f.GetValue(null);

    f = t.GetField("SpawnMax", flags);
    SpawnMax = f==null ? SpawnMin : (int)f.GetValue(null);
    
    f = t.GetField("ShopValue", flags);
    ShopValue = f==null ? 0 : (int)f.GetValue(null);
  }

  public Type ItemType;
  public int SpawnChance, SpawnMin, SpawnMax, ShopValue;
}

public sealed class Global
{ private Global() { }

  static Global()
  { Type[] types = typeof(Item).Assembly.GetTypes(); // build a table of items and their spawn chances
    ArrayList list = new ArrayList();
    foreach(Type t in types)
      if(!t.IsAbstract && t.IsSerializable && t.IsSubclassOf(typeof(Item))) list.Add(new SpawnInfo(t));
    objSpawns = (SpawnInfo[])list.ToArray(typeof(SpawnInfo));
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

  public static bool Coinflip() { return Random.Next(100)<50; }

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
    { total += objSpawns[spawnIndex].SpawnChance;
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
  
  public static void Randomize(string[] names)
  { for(int i=0; i<names.Length; i++)
    { int j = Global.Rand(names.Length);
      string t = names[i]; names[i] = names[j]; names[j] = t;
    }
  }
  public static void Randomize(string[] names, Color[] colors)
  { for(int i=0; i<names.Length; i++)
    { int j = Global.Rand(names.Length);
      string n = names[i]; names[i] = names[j]; names[j] = n;
      Color  c = colors[i]; colors[i] = colors[j]; colors[j] = c;
    }
  }

  public static Item SpawnItem(SpawnInfo s)
  { Item item = (Item)s.ItemType.GetConstructor(Type.EmptyTypes).Invoke(null);
    item.Count = Global.Rand(s.SpawnMax-s.SpawnMin)+s.SpawnMin;

    // (x/10)^0.5 where x=0 to 100, truncated towards 0. bonus = 0 to 3
    int bonus = (int)Math.Sqrt(Global.Random.NextDouble()*10);
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

  public static void Deserialize(System.IO.Stream stream, IFormatter formatter)
  { socialGroups = (SocialGroup[])formatter.Deserialize(stream);
    Random = (Random)formatter.Deserialize(stream);
    nextID = (ulong)formatter.Deserialize(stream);
    spawnIndex = (int)formatter.Deserialize(stream);
    numSocials = (int)formatter.Deserialize(stream);
  }
  public static void Serialize(System.IO.Stream stream, IFormatter formatter)
  { formatter.Serialize(stream, socialGroups);
    formatter.Serialize(stream, Random);
    formatter.Serialize(stream, nextID);
    formatter.Serialize(stream, spawnIndex);
    formatter.Serialize(stream, numSocials);
  }

  public static readonly Point[] DirMap = new Point[8]
  { new Point(0, -1), new Point(1, -1), new Point(1, 0),  new Point(1, 1),
    new Point(0, 1),  new Point(-1, 1), new Point(-1, 0), new Point(-1, -1)
  };
  
  public static Hashtable ObjHash;

  static SpawnInfo[] objSpawns;
  static SocialGroup[] socialGroups;
  static Random Random = new Random();
  static ulong nextID=1;
  static int spawnIndex, numSocials;
}

} // namespace Chrono
