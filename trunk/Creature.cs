using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public enum Attr
{ Str, Dex, Int, NumBasics,
  MaxHP=NumBasics, MaxMP, Speed, AC, EV, NumModifiable,
  Age=NumModifiable, Exp, ExpLevel, HP, MP, Gold, Hunger, Sickness, NumAttributes
}

public enum Slot
{ Ring=-2, Invalid=-1, Head, Cloak, Torso, Legs, Neck, Hands, Feet, LRing, RRing, NumSlots
}

public enum Race
{ RandomRace=-1, Human, Orc, NumRaces
}

public enum CreatureClass
{ RandomClass=-1, Fighter, NumClasses
}

public enum Hunger { Normal, Hungry, Starving };

public abstract class Creature
{ [Flags] public enum Flag { None=0, Confused=1, Stunned=2, Hallucinating=4, Asleep=8 }

  public int AC { get { return GetAttr(Attr.AC); } }
  public int Age { get { return attr[(int)Attr.Age]; } set { SetRawAttr(Attr.Age, value); } }
  public int Dex { get { return GetAttr(Attr.Dex); } }
  public int DexBonus
  { get
    { int dex = Dex;
      return dex<8 ? (dex-9)/2 : dex-8;
    }
  }
  public int EV { get { return GetAttr(Attr.EV); } }
  public int Exp
  { get { return attr[(int)Attr.Exp]; }
    set
    { SetRawAttr(Attr.Exp, value);
      if(value>=NextExp) LevelUp();
    }
  }
  public int ExpLevel
  { get { return attr[(int)Attr.ExpLevel]; }
    set
    { SetRawAttr(Attr.ExpLevel, value);
      Title = GetTitle();
    }
  }
  public Flag Flags { get { return flags; } set { flags=value; } }
  public int Gold { get { return attr[(int)Attr.Gold]; } set { SetRawAttr(Attr.Gold, value); } }
  public bool HandsFull
  { get
    { bool full=true;
      for(int i=0; i<Hands.Length; i++)
        if(Hands[i]==null) full=false;
        else if(Hands[i].AllHandWield) return true;
      return full;
    }
  }
  public int HP { get { return attr[(int)Attr.HP]; } set { SetRawAttr(Attr.HP, value); } }
  public int Hunger { get { return attr[(int)Attr.Hunger]; } set { SetRawAttr(Attr.Hunger, value); } }
  public Hunger HungerLevel
  { get { return Hunger<HungryAt ? Chrono.Hunger.Normal : Hunger<StarvingAt ? Chrono.Hunger.Hungry : Chrono.Hunger.Starving; }
  }
  public int Int { get { return GetAttr(Attr.Int); } }
  public int KillExp { get { return baseKillExp*(ExpLevel+1); } }
  public int MaxHP { get { return GetAttr(Attr.MaxHP); } }
  public int MaxMP { get { return GetAttr(Attr.MaxMP); } }
  public int MP { get { return attr[(int)Attr.MP]; } set { SetRawAttr(Attr.MP, value); } }
  public int NextExp
  { get
    { return (int)(ExpLevel<8 ? 100*Math.Pow(1.75, ExpLevel)
                              : ExpLevel<20 ? 100*Math.Pow(1.3, ExpLevel+10)-3000
                                            : 100*Math.Pow(1.18, ExpLevel+25)+50000) - 75;
    }
  }
  public int Sickness { get { return attr[(int)Attr.Sickness]; } set { SetRawAttr(Attr.Sickness, value); } }
  public int Speed { get { return GetAttr(Attr.Speed); } }
  public int Str { get { return GetAttr(Attr.Str); } }
  public int StrBonus
  { get
    { int str = Str;
      return str<10 ? (str-11)/2 : str-10;
    }
  }
  public int X { get { return Position.X; } set { Position.X=value; } }
  public int Y { get { return Position.Y; } set { Position.Y=value; } }

  public string Prefix
  { get
    { if(prefix!=null) return prefix;
      if(Name!=null) return "";
      string name = Race.ToString();
      switch(char.ToLower(name[0]))
      { case 'a': case 'e': case 'i': case 'o': case 'u': return "an ";
        default: return "a ";
      }
    }
  }
  public virtual string aName
  { get { return Name==null ? Prefix+Race.ToString().ToLower() : Name; }
  }
  public virtual string TheName
  { get { return Name==null ? "The "+Race.ToString().ToLower() : Name; }
  }
  public virtual string theName
  { get { return Name==null ? "the "+Race.ToString().ToLower() : Name; }
  }

  public void Attack(Direction dir)
  { Creature c = Map.GetCreature(Global.Move(Position, dir));
    if(c==null) App.IO.Print("You swing at thin air.");
    else Attack(c);
  }
  public void Attack(Creature c)
  { bool unarmed=true;
    int delay=0;
    for(int i=0; i<Hands.Length; i++)
    { Weapon w = Hands[i] as Weapon;
      if(w==null) continue;
      unarmed=false;
      TryHit(c, w);
      if(w.Delay>delay) delay=w.Delay;
    }
    if(unarmed) TryHit(c, null);
    Timer -= delay;
  }

  public bool CanRemove(Slot slot) { return true; }
  public bool CanRemove(Wearable item) { return true; }
  public bool CanUnequip(int hand) { return true; }
  public bool CanUnequip(Wieldable item) { return true; }

  public void Die(Creature c) { Die(c.aName); }
  public abstract void Die(string cause);

  public Item Drop(char c)
  { Item i = Inv[c];
    Drop(i);
    return i;
  }
  public Item Drop(char c, int count) { return Drop(Inv[c], count); }
  public void Drop(Item item)
  { Inv.Remove(item);
    Map.AddItem(Position, item);
    item.OnDrop(this);
    OnDrop(item);
  }
  public Item Drop(Item item, int count)
  { if(count==item.Count) Inv.Remove(item);
    else item = item.Split(count);
    Map.AddItem(Position, item);
    item.OnDrop(this);
    OnDrop(item);
    return item;
  }

  public void Equip(Wieldable item)
  { if(item.AllHandWield)
      for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) throw new ApplicationException("No room to wield!");
    if(HandsFull) throw new ApplicationException("No room to wield!");
    for(int i=0; i<Hands.Length; i++)
      if(Hands[i]==null)
      { Hands[i] = item;
        OnEquip(item);
        item.OnEquip(this);
        break;
      }
  }

  public bool Equipped(int hand) { return Hands[hand]!=null; }
  public bool Equipped(Item item)
  { for(int i=0; i<Hands.Length; i++) if(Hands[i]==item) return true;
    return false;
  }

  public int GetAttr(Attr attribute)
  { int idx=(int)attribute, val = attr[idx];
    if(attribute>=Attr.NumModifiable) return val;
    for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) val += Slots[i].Mods[idx];
    return val;
  }

  public int GetRawAttr(Attr attribute) { return attr[(int)attribute]; }
  public int SetRawAttr(Attr attribute, int val) { return attr[(int)attribute]=val; }

  public bool GetFlag(Flag f) { return (Flags&f)!=0; }
  public bool SetFlag(Flag flag, bool on) { if(on) Flags |= flag; else Flags &= ~flag; return on; }

  public virtual void LevelDown() { ExpLevel--; }
  public virtual void LevelUp()
  { int hpGain = attr[(int)Attr.Str]/3+1;
    int mpGain = attr[(int)Attr.Int]/3+1;
    attr[(int)Attr.MaxHP] += hpGain;
    attr[(int)Attr.MaxMP] += mpGain;
    attr[(int)Attr.HP] += hpGain;
    attr[(int)Attr.MP] += mpGain;
    ExpLevel++;
  }

  public virtual void Generate(int level, CreatureClass myClass)
  { if(myClass==CreatureClass.RandomClass)
      myClass = (CreatureClass)Global.Rand((int)CreatureClass.NumClasses);

    Class = myClass; Title = GetTitle(); Light = 8;

    int[] mods = raceAttrs[(int)Race].Mods; // attributes for race
    for(int i=0; i<mods.Length; i++) attr[i] += mods[i];

    mods = classAttrs[(int)myClass].Mods;   // attribute modifiers from class
    for(int i=0; i<mods.Length; i++) attr[i] += mods[i];

    int points = 8; // allocate extra points randomly
    while(points>0)
    { int a = Global.Rand((int)Attr.NumBasics);
      if(attr[a]<=17 || Global.Coinflip()) { SetRawAttr((Attr)a, attr[a]+1); points--; }
    }

    HP = MaxHP; MP = MaxMP;

    while(level-->0) LevelUp();
  }

  public void Interrupt() { interrupt = true; }

  public bool IsMonsterVisible() { return IsMonsterVisible(VisibleTiles()); }
  public bool IsMonsterVisible(Point[] vis)
  { for(int i=0; i<Map.Creatures.Count; i++)
      if(Map.Creatures[i]!=this)
      { Point cp = Map.Creatures[i].Position;
        for(int j=0; j<vis.Length; j++)
          if(vis[j]==cp) return true;
      }
    return false;
  }

  public void ItemThink() { for(int i=0; i<Inv.Count; i++) Inv[i].Think(this); }

  public virtual void OnDrop(Item item) { }
  public virtual void OnEquip(Wieldable item) { }
  public virtual void OnHit(Creature hit, Weapon w, int damage) { }
  public virtual void OnHitBy(Creature hit, Weapon w, int damage) { }
  public virtual void OnKill(Creature killed) { }
  public virtual void OnMiss(Creature hit, Weapon w) { }
  public virtual void OnMissBy(Creature hit, Weapon w) { }
  public virtual void OnRemove(Wearable item) { }
  public virtual void OnRemoveFail(Wearable item) { }
  public virtual void OnUnequip(Wieldable item) { }
  public virtual void OnUnequipFail(Wieldable item) { }
  public virtual void OnWear(Wearable item) { }

  public virtual Item Pickup(Item item)
  { Item ret = Inv.Add(item);
    ret.OnPickup(this);
    return ret;
  }
  public Item Pickup(IInventory inv, int index)
  { Item item = inv[index];
    inv.RemoveAt(index);
    return Pickup(item);
  }
  public Item Pickup(IInventory inv, Item item)
  { inv.Remove(item);
    return Pickup(item);
  }

  public void Remove(Item item)
  { for(int i=0; i<Slots.Length; i++) if(Slots[i]==item) { Remove((Slot)i); return; }
    throw new ApplicationException("Not wearing item!");
  }
  public void Remove(Slot slot)
  { Wearable item = Slots[(int)slot];
    Slots[(int)slot] = null;
    item.OnRemove(this);
    OnRemove(item);
  }

  public virtual void Think()
  { Age++;
    Timer-=Speed;
    if(Age%16==0)
    { if(HP<MaxHP) HP++;
      if(MP<MaxMP) MP++;
    }
  }

  public bool TryEquip(Wieldable item)
  { if(item==null)
    { bool success=true;
      for(int i=0; i<Hands.Length; i++) if(!TryUnequip(i)) success=false;
      return success;
    }
    if(item.AllHandWield)
    { for(int i=0; i<Hands.Length; i++) if(!CanUnequip(i)) return false;
      for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) Unequip(i);
    }
    else if(HandsFull)
    { bool success=false;
      for(int i=0; i<Hands.Length; i++) if(Hands[i].Class==item.Class && TryUnequip(i)) { success=true; break; }
      if(!success) for(int i=0; i<Hands.Length; i++) if(TryUnequip(i)) { success=true; break; }
      if(!success) return false;
    }
    else for(int i=0; i<Hands.Length; i++)
      if(Hands[i]!=null && Hands[i].Class==item.Class && !TryUnequip(i)) return false;
    Equip(item);
    return true;
  }

  public bool TryUnequip(Item item)
  { if(Equipped(item)) Unequip(item);
    return true;
  }
  public bool TryUnequip(int hand)
  { if(Equipped(hand)) Unequip(hand);
    return true;
  }

  public bool TryRemove(Item item)
  { Remove(item);
    return true;
  }
  public bool TryRemove(Slot slot)
  { Remove(slot);
    return true;
  }

  public void Unequip(Item item)
  { for(int i=0; i<Hands.Length; i++)
      if(Hands[i]==item)
      { Hands[i].OnUnequip(this);
        OnUnequip(Hands[i]);
        Hands[i]=null;
        return;
      }
    throw new ApplicationException("Not wielding "+item);
  }
  public void Unequip(int hand)
  { Wieldable i = Hands[hand];
    Hands[hand] = null;
    i.OnUnequip(this);
    OnUnequip(i);
  }

  public Creature[] VisibleCreatures() { return VisibleCreatures(VisibleTiles()); }
  public Creature[] VisibleCreatures(Point[] vis)
  { list.Clear();
    for(int i=0; i<Map.Creatures.Count; i++)
      if(Map.Creatures[i]!=this)
      { Point cp = Map.Creatures[i].Position;
        for(int j=0; j<vis.Length; j++)
          if(vis[j]==cp) { list.Add(Map.Creatures[i]); break; }
      }
    return (Creature[])list.ToArray(typeof(Creature));
  }

  public Point[] VisibleTiles()
  { int x=0, y=Light*4, s=1-y;
    visPts=0;
    VisiblePoint(0, 0);
    while(x<=y)
    { VisibleLine(x, y);
      VisibleLine(x, -y);
      VisibleLine(-x, y);
      VisibleLine(-x, -y);
      VisibleLine(y, x);
      VisibleLine(y, -x);
      VisibleLine(-y, x);
      VisibleLine(-y, -x);
      if(s<0) s = s+2*x+3;
      else { s = s+2*(x-y)+5; y--; }
      x++;
    }
    Point[] ret = new Point[visPts];
    for(visPts--; visPts>=0; visPts--)
    { y = Math.DivRem(vis[visPts], Map.Width, out x);
      ret[visPts] = new Point(x, y);
    }
    return ret;
  }

  public void Wear(Wearable item)
  { if(Slots[(int)item.Slot] != null) throw new ApplicationException("Already wearing something!");
    Slots[(int)item.Slot] = item;
    OnWear(item);
    item.OnWear(this);
  }

  public bool Wearing(Slot slot) { return Slots[(int)slot]!=null; }
  public bool Wearing(Item item)
  { for(int i=0; i<Slots.Length; i++) if(Slots[i]==item) return true;
    return false;
  }

  public Inventory  Inv = new Inventory();
  public Wearable[] Slots = new Wearable[(int)Slot.NumSlots];
  public Wieldable[] Hands = new Wieldable[2];
  public string Name, Title;
  public Point Position;
  public int    Light, Timer;
  public Map    Map, Memory;
  public Race   Race;
  public Color  Color=Color.Dire;
  public CreatureClass Class;

  static public Creature MakeCreature(Type type)
  { Creature c = type.GetConstructor(Type.EmptyTypes).Invoke(null) as Creature;
    App.Assert(c!=null, "{0} is not a valid creature type", type);
    return c;
  }

  static public Creature Generate(Type type, int level) { return Generate(type, level, CreatureClass.RandomClass); }
  static public Creature Generate(Type type, int level, CreatureClass myClass)
  { Creature creature = MakeCreature(type);
    creature.Generate(level, myClass);
    return creature;
  }

  protected const int HungryAt=500, StarvingAt=800, StarveAt=1000;

  protected struct AttrMods
  { public AttrMods(params int[] mods) { Mods = mods; }
    public int[] Mods;
  }

  protected virtual int CalculateDamage() { return Global.NdN(1, (Str*2+2)/3); }

  protected internal virtual void OnMapChanged() { }

  protected void UpdateMemory()
  { if(Memory==null) return;
    UpdateMemory(VisibleTiles());
  }
  protected void UpdateMemory(Point[] vis)
  { if(Memory==null) return;
    foreach(Point pt in vis)
    { Tile tile = Map[pt];
      tile.Items = tile.Items==null || tile.Items.Count==0 ? null : tile.Items.Clone();
      Memory[pt] = tile;
    }
    Creature[] creats = VisibleCreatures(vis);
    foreach(Creature c in creats)
    { Tile tile = Memory[c.Position];
      tile.Creature = c;
      Memory[c.Position] = tile;
    }
  }

  protected string prefix;
  protected int baseKillExp;
  protected bool interrupt;

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

  void TryHit(Creature c, Weapon w)
  { int toHit   = (Dex+EV+1)/2 + (w!=null ? w.ToHitBonus : 0); // average of dex and ev, rounded up
    int toEvade = c.EV;
    toHit   -= toHit*(int)HungerLevel*10/100+1; // effects of hunger
    toEvade -= toEvade*(int)c.HungerLevel*10/100+1;
    int n = Global.Rand(toHit+toEvade);
string msg = string.Format("HIT: (toHit: {0}, EV: {1}, roll: {2} = {3})", toHit, toEvade, n, n>=toEvade ? "hit" : "miss");
    if(n>=toEvade) // hit
    { int damage = w==null ? CalculateDamage() : w.CalculateDamage(this);
      n = Global.Rand(c.AC);
      damage -= n; // armor absorbs damage
      if(damage<0) damage = 0; // normalize
      App.IO.Print("{4}, DAMAGE: {0} -> {1}, HP: {2} -> {3}", damage+n, damage, c.HP, c.HP-damage, msg);
      c.HP -= damage;
      c.OnHitBy(this, w, damage);
      OnHit(c, w, damage);
      if(c.HP<=0)
      { c.Die(this); Exp += c.KillExp;
        if(c.HP<=0) OnKill(c); // amulet of saving, etc...
      }
    }
    else // miss
    { App.IO.Print(msg);
      c.OnMissBy(this, w);
      OnMiss(c, w);
    }
  }

  void VisibleLine(int x2, int y2)
  { int x=0, y=0, dx=Math.Abs(x2), dy=Math.Abs(y2), xi=Math.Sign(x2), yi=Math.Sign(y2), r, ru, p;
    if(dx>=dy)
    { r=dy*2; ru=r-dx*2; p=r-dx;
      do
      { if(p>0) { y+=yi; p+=ru; }
        else p+=r;
        x+=xi; dx--;
        if(Math.Sqrt(x*x+y*y)-0.5>Light) break;
      } while(dx>=0 && VisiblePoint(x, y));
    }
    else
    { r=dx*2; ru=r-dy*2; p=r-dy;
      do
      { if(p>0) { x+=xi; p+=ru; }
        else p+=r;
        y+=yi; dy--;
        if(Math.Sqrt(x*x+y*y)-0.5>Light) break;
      } while(dy>=0 && VisiblePoint(x, y));
    }
  }

  bool VisiblePoint(int x, int y)
  { x += X; y += Y;
    TileType type = Map[x, y].Type;
    if(type==TileType.Border) return false;

    if(visPts==vis.Length)
    { int[] narr = new int[visPts*2];
      Array.Copy(vis, narr, visPts);
      vis = narr;
    }

    int ti = y*Map.Width+x;
    for(int i=0; i<visPts; i++) if(vis[i]==ti) goto ret;
    vis[visPts++] = ti;
    ret: return Map.IsPassable(type);
  }

  int[] attr = new int[(int)Attr.NumAttributes];
  Flag flags;

  static ArrayList list = new ArrayList();
  static int[] vis = new int[128];
  static int visPts;

  static readonly AttrMods[] raceAttrs = new AttrMods[(int)Race.NumRaces]
  { new AttrMods(6, 6, 6), // Human - 18
    new AttrMods(9, 4, 3)  // Orc   - 16
  };
  static readonly AttrMods[] classAttrs = new AttrMods[(int)CreatureClass.NumClasses]
  { new AttrMods(7, 3, -1, 15, 2, 40, 0, 1) // Fighter - 10, 15/2, 40, 0/1
  };
  static readonly ClassLevel[][] classTitles = new ClassLevel[(int)CreatureClass.NumClasses][]
  { new ClassLevel[]
    { new ClassLevel(0, "Whacker"), new ClassLevel(3, "Beater"), new ClassLevel(7, "Grunter"),
      new ClassLevel(12, "Fighter"), new ClassLevel(18, "Veteran")
    }
  };
}

} // namespace Chrono
