using System;
using System.Drawing;

namespace Chrono
{

public enum Attr { Strength, Dexterity, Intelligence, Perception, NumAttributes }

public abstract class Creature
{ 
  [Flags] public enum Flag { None=0, Confused=1, Stunned=2, Hallucinating=4, Asleep=8 }

  public int Exp { get { return exp; } set { exp=value; App.IO.RedrawStats=true; } }
  public int ExpLevel { get { return explevel; } set { explevel=value; App.IO.RedrawStats=true; } }
  public int HP { get { return hp; } set { hp=value; App.IO.RedrawStats=true; } }
  public int MaxHP { get { return maxHp; } set { maxHp=value; App.IO.RedrawStats=true; } }
  public int MP { get { return mp; } set { mp=value; App.IO.RedrawStats=true; } }
  public int MaxMP { get { return maxMp; } set { maxMp=value; App.IO.RedrawStats=true; } }
  public int AC { get { return ac; } set { ac=value; App.IO.RedrawStats=true; } }
  public int EV { get { return ev; } set { ev=value; App.IO.RedrawStats=true; } }
  public Flag Flags { get { return flags; } set { flags=value; App.IO.RedrawStats=true; } }

  public int NextExp
  { get
    { return (int)(ExpLevel<8 ? 100*Math.Pow(1.75, ExpLevel)
                              : ExpLevel<20 ? 100*Math.Pow(1.3, ExpLevel+10)-3000
                                            : 100*Math.Pow(1.18, ExpLevel+25)+50000) - 75;
    }
  }
  public Point Position { get { return new Point(X, Y); } set { X=value.X; Y=value.Y; } }

  public int GetAttr(Attr attribute) { return attr[(int)attribute]; }
  public int SetAttr(Attr attribute, int val) { App.IO.RedrawStats=true; return attr[(int)attribute]=val; }

  public bool GetFlag(Flag f) { return (Flags&f)!=0; }
  public bool SetFlag(Flag flag, bool on) { if(on) Flags |= flag; else Flags &= ~flag; return on; }

  public virtual void Think() { Age++; }

  public Inventory Inv = new Inventory();
  public string Name, Title;
  public int    X, Y;
  public int    Age, Light;
  public Level  Level;

  static public Creature MakeCreature(Type type)
  { Creature c = type.GetConstructor(Type.EmptyTypes).Invoke(null) as Creature;
    App.Assert(c!=null, "{0} is not a valid creature type", type);
    return c;
  }

  int[] attr = new int[(int)Attr.NumAttributes];
  int exp=0, explevel=0, hp, maxHp, mp, maxMp, ac, ev;
  Flag flags;
}

} // namespace Chrono