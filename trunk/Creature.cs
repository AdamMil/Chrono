using System;
using System.Drawing;

namespace Chrono
{

public enum Attr
{ Str, Dex, Int, Per, Age, Exp, ExpLevel, HP, MaxHP, MP, MaxMP, AC, EV, Gold, Speed,
  NumAttributes
}

public abstract class Creature
{ [Flags] public enum Flag { None=0, Confused=1, Stunned=2, Hallucinating=4, Asleep=8 }

  public int AC { get { return attr[(int)Attr.AC]; } set { SetAttr(Attr.AC, value); } }
  public int Age { get { return attr[(int)Attr.Age]; } set { SetAttr(Attr.Age, value); } }
  public int Dex { get { return attr[(int)Attr.Dex]; } set { SetAttr(Attr.Dex, value); } }
  public int EV { get { return attr[(int)Attr.EV]; } set { SetAttr(Attr.EV, value); } }
  public int Exp { get { return attr[(int)Attr.Exp]; } set { SetAttr(Attr.Exp, value); } }
  public int ExpLevel { get { return attr[(int)Attr.ExpLevel]; } set { SetAttr(Attr.ExpLevel, value); } }
  public int Gold { get { return attr[(int)Attr.Gold]; } set { SetAttr(Attr.Gold, value); } }
  public int HP { get { return attr[(int)Attr.HP]; } set { SetAttr(Attr.HP, value); } }
  public int Int { get { return attr[(int)Attr.Int]; } set { SetAttr(Attr.Int, value); } }
  public int MaxHP { get { return attr[(int)Attr.MaxHP]; } set { SetAttr(Attr.MaxHP, value); } }
  public int MaxMP { get { return attr[(int)Attr.MaxMP]; } set { SetAttr(Attr.MaxMP, value); } }
  public int MP { get { return attr[(int)Attr.MP]; } set { SetAttr(Attr.MP, value); } }
  public int Per { get { return attr[(int)Attr.Per]; } set { SetAttr(Attr.Per, value); } }
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

  public virtual void Think() { Age++; Timer-=100; }

  public Inventory Inv = new Inventory();
  public string Name, Title;
  public int    X, Y;
  public int    Light, Timer;
  public Map    Map;

  static public Creature MakeCreature(Type type)
  { Creature c = type.GetConstructor(Type.EmptyTypes).Invoke(null) as Creature;
    App.Assert(c!=null, "{0} is not a valid creature type", type);
    return c;
  }

  int[] attr = new int[(int)Attr.NumAttributes];
  Flag flags;
}

} // namespace Chrono