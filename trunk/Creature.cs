using System;
using System.Drawing;

namespace Chrono
{

public enum Attr
{ Str, Dex, Int, NumBasics,
  MaxHP=NumBasics, MaxMP, Speed, AC, EV, Age, Exp, ExpLevel, HP, MP, Gold, NumAttributes
}

public enum Race
{ RandomRace=-1, Human, Orc, NumRaces
}

public enum CreatureClass
{ RandomClass=-1, Fighter, NumClasses
}

public abstract class Creature
{ [Flags] public enum Flag { None=0, Confused=1, Stunned=2, Hallucinating=4, Asleep=8 }

  public int AC { get { return attr[(int)Attr.AC]; } set { SetAttr(Attr.AC, value); } }
  public int Age { get { return attr[(int)Attr.Age]; } set { SetAttr(Attr.Age, value); } }
  public int Dex { get { return attr[(int)Attr.Dex]; } set { SetAttr(Attr.Dex, value); } }
  public int EV { get { return attr[(int)Attr.EV]; } set { SetAttr(Attr.EV, value); } }
  public int Exp
  { get { return attr[(int)Attr.Exp]; }
    set
    { SetAttr(Attr.Exp, value);
      if(value>=NextExp) LevelUp();
    }
  }
  public int ExpLevel
  { get { return attr[(int)Attr.ExpLevel]; }
    set
    { SetAttr(Attr.ExpLevel, value);
      Title = GetTitle();
    }
  }
  public int Gold { get { return attr[(int)Attr.Gold]; } set { SetAttr(Attr.Gold, value); } }
  public int HP { get { return attr[(int)Attr.HP]; } set { SetAttr(Attr.HP, value); } }
  public int Int { get { return attr[(int)Attr.Int]; } set { SetAttr(Attr.Int, value); } }
  public int MaxHP { get { return attr[(int)Attr.MaxHP]; } set { SetAttr(Attr.MaxHP, value); } }
  public int MaxMP { get { return attr[(int)Attr.MaxMP]; } set { SetAttr(Attr.MaxMP, value); } }
  public int MP { get { return attr[(int)Attr.MP]; } set { SetAttr(Attr.MP, value); } }
  public int Speed { get { return attr[(int)Attr.Speed]; } set { SetAttr(Attr.Speed, value); } }
  public int Str { get { return attr[(int)Attr.Str]; } set { SetAttr(Attr.Str, value); } }

  public Flag Flags { get { return flags; } set { flags=value; /* FIXME: redraw stats */ } }

  public int NextExp
  { get
    { return (int)(ExpLevel<8 ? 100*Math.Pow(1.75, ExpLevel)
                              : ExpLevel<20 ? 100*Math.Pow(1.3, ExpLevel+10)-3000
                                            : 100*Math.Pow(1.18, ExpLevel+25)+50000) - 75;
    }
  }
  public Point Position { get { return new Point(X, Y); } set { X=value.X; Y=value.Y; } }

  public int GetAttr(Attr attribute) { return attr[(int)attribute]; }
  public int SetAttr(Attr attribute, int val)
  { if(val != attr[(int)attribute])
    { App.IO.RedrawStats = true;
      attr[(int)attribute]=val;
    }
    return val;
  }

  public bool GetFlag(Flag f) { return (Flags&f)!=0; }
  public bool SetFlag(Flag flag, bool on) { if(on) Flags |= flag; else Flags &= ~flag; return on; }
  
  public virtual void LevelDown() { ExpLevel--; }
  public virtual void LevelUp()   { ExpLevel++; }

  public virtual void Generate(int level, CreatureClass myClass, Race race)
  { if(myClass==CreatureClass.RandomClass)
      myClass = (CreatureClass)Global.Rand((int)CreatureClass.NumClasses);
    if(race==Race.RandomRace) Race = (Race)Global.Rand((int)Race.NumRaces);

    Class = myClass; Race = race; Title = GetTitle();

    Light = 8;

    int[] mods = raceAttrs[(int)race].Mods; // attributes for race
    for(int i=0; i<mods.Length; i++) attr[i] += mods[i];

    mods = classAttrs[(int)myClass].Mods;     // attribute modifiers from class
    for(int i=0; i<mods.Length; i++) attr[i] += mods[i];
    
    int points = 8; // allocate extra points randomly
    while(points>0)
    { int a = Global.Rand((int)Attr.NumBasics);
      if(attr[a]<=17 || Global.Coinflip()) { SetAttr((Attr)a, attr[a]+1); points--; }
    }

    HP = MaxHP; MP = MaxMP;

    while(level-->0) LevelUp();
  }
  public virtual void Think() { Age++; Timer-=100; }

  public Inventory Inv = new Inventory();
  public string Name, Title;
  public int    X, Y;
  public int    Light, Timer;
  public Map    Map;
  public Race   Race;
  public CreatureClass Class;

  static public Creature MakeCreature(Type type)
  { Creature c = type.GetConstructor(Type.EmptyTypes).Invoke(null) as Creature;
    App.Assert(c!=null, "{0} is not a valid creature type", type);
    return c;
  }

  static public Creature Generate(Type type, int level) { return Generate(type, level, CreatureClass.RandomClass); }
  static public Creature Generate(Type type, int level, CreatureClass myClass)
  { return Generate(type, level, myClass);
  }
  static public Creature Generate(Type type, int level, CreatureClass myClass, Race race)
  { Creature creature = MakeCreature(type);
    creature.Generate(level, myClass, race);
    return creature;
  }

  protected struct AttrMods
  { public AttrMods(params int[] mods) { Mods = mods; }
    public int[] Mods;
  }

  struct ClassLevel
  { public ClassLevel(int level, string title) { Level=level; Title=title; }
    public int Level;
    public string Title;
  }

  string GetTitle()
  { string title=string.Empty;
    ClassLevel[] classes = classTitles[(int)Class];
    for(int i=0; i<classes.Length; i++)
    { if(classes[i].Level>ExpLevel) break;
      title = classes[i].Title;
    }
    return title;
  }

  int[] attr = new int[(int)Attr.NumAttributes];
  Flag flags;

  
  static readonly AttrMods[] raceAttrs = new AttrMods[(int)Race.NumRaces]
  { new AttrMods(6, 6, 6), // Human
    new AttrMods(9, 3, 4)  // Orc
  };
  static readonly AttrMods[] classAttrs = new AttrMods[(int)CreatureClass.NumClasses]
  { new AttrMods(7, 0, 3, 15, 0, 10) // Fighter
  };
  static readonly ClassLevel[][] classTitles = new ClassLevel[(int)CreatureClass.NumClasses][]
  { new ClassLevel[]
    { new ClassLevel(0, "Whacker"), new ClassLevel(3, "Beater"), new ClassLevel(7, "Grunter"),
      new ClassLevel(12, "Fighter"), new ClassLevel(18, "Veteran")
    }
  };
}

} // namespace Chrono