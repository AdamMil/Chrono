using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public enum Attr
{ Str, Dex, Int, NumBasics,
  MaxHP=NumBasics, MaxMP, Speed, AC, EV, Stealth, Light, NumModifiable,
  Age=NumModifiable, Exp, ExpLevel, ExpPool, HP, MP, Gold, Hunger, Sickness, NumAttributes
}

public enum Death
{ Starvation, Trap, Combat
}

public enum EntityClass
{ RandomClass=-1, Fighter, NumClasses
}

public enum Race
{ RandomRace=-1, Human, Orc, NumRaces
}

public enum Skill
{ // these match weapon classes
  Dagger, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Thrown, WeaponSkills,
  // these match magic types
  Summoning=WeaponSkills, Enchantment, Translocation, Transformation, Divination, Channeling, Necromancy,
  Elemental, Poison, MagicSkills,
  
  LocksTraps=MagicSkills, Invoking, Casting, // general
  UnarmedCombat, Dodge, Fighting, Shields, Armor, MagicResistance, // fighting
  
  NumSkills
}

public enum Slot
{ Ring=-2, Invalid=-1, Head, Cloak, Torso, Legs, Neck, Hands, Feet, LRing, RRing, NumSlots
}

public enum Hunger { Normal, Hungry, Starving };

public abstract class Entity
{ public Entity() { ExpLevel=1; }
  [Flags] public enum Flag { None=0, Confused=1, Stunned=2, Hallucinating=4, Asleep=8 }

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
  public int ExpPool { get { return attr[(int)Attr.ExpPool]; } set { SetRawAttr(Attr.ExpPool, value); } }
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
  public int KillExp { get { return baseKillExp*ExpLevel; } }
  public int Light { get { return GetAttr(Attr.Light); } }
  public int MaxHP { get { return GetAttr(Attr.MaxHP); } }
  public int MaxMP { get { return GetAttr(Attr.MaxMP); } }
  public int MP { get { return attr[(int)Attr.MP]; } set { SetRawAttr(Attr.MP, value); } }
  public int NextExp
  { get
    { int level = ExpLevel-1;
      return (int)(level<8 ? 100*Math.Pow(1.75, level)
                           : level<20 ? 100*Math.Pow(1.3, level+10)-3000 : 100*Math.Pow(1.18, level+25)+50000) - 75;
    }
  }
  public Shield Shield
  { get
    { for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null && Hands[i].Class==ItemClass.Shield) return (Shield)Hands[i];
      return null;
    }
  }
  public int Sickness { get { return attr[(int)Attr.Sickness]; } set { SetRawAttr(Attr.Sickness, value); } }
  public int Speed { get { return GetAttr(Attr.Speed); } }
  public int Stealth { get { return GetAttr(Attr.Stealth); } }
  public int Str { get { return GetAttr(Attr.Str); } }
  public int StrBonus
  { get
    { int str = Str;
      return str<10 ? (str-11)/2 : str-10;
    }
  }
  public Weapon Weapon
  { get
    { for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null && Hands[i].Class==ItemClass.Weapon) return (Weapon)Hands[i];
      return null;
    }
  }
  public int X { get { return Position.X; } set { Position.X=value; } }
  public int Y { get { return Position.Y; } set { Position.Y=value; } }

  public string Prefix
  { get
    { if(prefix!=null) return prefix;
      if(Name!=null) return "";
      string name = Race.ToString();
      return Global.AorAn(name)+' ';
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
  { Point np = Global.Move(Position, dir);
    Entity c = Map.GetEntity(np);
    string msg=null;
    byte noise=0;
    if(c!=null) Attack(c);
    else
    { Tile t = Map[np];
      Weapon w = Weapon;
      if(t.Type==TileType.ClosedDoor)
      { int damage = w==null ? CalculateDamage() : w.CalculateDamage(this);
        if(damage>=10)
        { msg = "Crash! You break down the door.";
          Map.SetType(np, TileType.RoomFloor);
          noise = 200;
          if(w==null) Exercise(Global.Coinflip() ? Attr.Dex : Attr.Str);
          else Exercise((Skill)w.wClass);
        }
        else { msg = "Wham!"; noise = 80; }
      }
      else if(!Map.IsPassable(t.Type)) { msg = "Wham!"; noise = 80; }
      else msg = "You swing at thin air.";
      if(this==App.Player)
      { if(msg!=null) App.IO.Print(msg);
        if(noise>0) Map.MakeNoise(np, this, Noise.Bang, noise);
      }
    }
  }
  public void Attack(Entity c)
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
  public bool CanSee(Entity creature) { return LookAt(creature) != Direction.Invalid; }
  public bool CanUnequip(int hand) { return true; }
  public bool CanUnequip(Wieldable item) { return true; }

  public abstract void Die(Entity killer, Item impl); // death from item (impl==null means hand-to-hand combat)
  public abstract void Die(Death cause);

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

  public void Exercise(Attr attribute)
  { int aval = attr[(int)attribute];
    if(aval>17) // over 17, it gets harder to increase attributes through exercise
    { aval -= 18;
      if(Global.Rand(100) < aval*10+50) return; // returns: 18:50%, 19:60%, 20:70%, 21:80%, 22:90%, 23+:100%
    }
    int points = Math.Min(Global.Rand(6), ExpPool);
    if(points==0) return;
    if(Global.Rand(100)<33) // 33% chance of exercise
    { ExpPool -= points;
      if((attrExp[(int)attribute] += points) >= 250)
      { attrExp[(int)attribute] -= 250;
        attr[(int)attribute]++;
        OnAttrChange(attribute, 1, true);
      }
    }
  }
  public void Exercise(Skill skill)
  { if(!Training(skill)) return;
    int points = Math.Min(Global.Rand(11), ExpPool);
    if(points==0) return;
    if(Global.Rand(100)<33) // 33% chance of exercise
    { int need = RaceSkills[(int)Race][(int)skill];
      ExpPool -= points;
      if((SkillExp[(int)skill] += points) >= need)
      { SkillExp[(int)skill] -= need;
        Skills[(int)skill]++;
        OnSkillUp(skill);
      }
    }
  }
  
  public int GetAttr(Attr attribute)
  { int idx=(int)attribute, val = attr[idx];
    if(attribute>=Attr.NumModifiable) return val;
    for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) val += Slots[i].Mods[idx];
    for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) val += Hands[i].Mods[idx];
    if(attribute==Attr.Stealth) // stealth is from 0 to 10
    { if(val<0) val=0;
      if(val>10) val=10;
    }
    return val;
  }

  public virtual void Generate(int level, EntityClass myClass)
  { if(myClass==EntityClass.RandomClass)
      myClass = (EntityClass)Global.Rand((int)EntityClass.NumClasses);

    Class = myClass; Title = GetTitle(); SetRawAttr(Attr.Light, 8);

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

    while(--level>0) LevelUp();
  }

  public bool GetFlag(Flag f) { return (Flags&f)!=0; }
  public bool SetFlag(Flag flag, bool on) { if(on) Flags |= flag; else Flags &= ~flag; return on; }

  public int GetRawAttr(Attr attribute) { return attr[(int)attribute]; }
  public int SetRawAttr(Attr attribute, int val) { return attr[(int)attribute]=val; }
  
  public int GetSkill(Skill skill) { return Skills[(int)skill]; }
  public int SetSkill(Skill skill, int value) { return Skills[(int)skill]=value; }

  public void Interrupt() { interrupt = true; }

  public void Invoke(Item item)
  { OnInvoke(item);
    if(item.Invoke(this)) Exercise(Skill.Invoking);
  }

  public bool IsMonsterVisible() { return IsMonsterVisible(VisibleTiles()); }
  public bool IsMonsterVisible(Point[] vis)
  { for(int i=0; i<Map.Entities.Count; i++)
      if(Map.Entities[i]!=this)
      { Point cp = Map.Entities[i].Position;
        for(int j=0; j<vis.Length; j++)
          if(vis[j]==cp) return true;
      }
    return false;
  }

  public void ItemThink() { for(int i=0; i<Inv.Count; i++) Inv[i].Think(this); }

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

  public Direction LookAt(Entity creature)
  { int x2 = creature.Position.X-X, y2 = creature.Position.Y-Y, light=Light;
    int x=0, y=0, dx=Math.Abs(x2), dy=Math.Abs(y2), xi=Math.Sign(x2), yi=Math.Sign(y2), r, ru, p;
    Point off = new Point();
    if(dx>=dy)
    { r=dy*2; ru=r-dx*2; p=r-dx;
      off.X = xi;
      if(p>0) off.Y = yi;
      do
      { if(p>0) { y+=yi; p+=ru; }
        else p+=r;
        x+=xi; dx--;
        if(creature.X==x+X && creature.Y==y+Y) break;
        if(!Map.IsPassable(x+X, y+Y) || Math.Sqrt(x*x+y*y)-0.5>light) return Direction.Invalid;
      } while(dx>=0);
    }
    else
    { r=dx*2; ru=r-dy*2; p=r-dy;
      if(p>0) off.X = xi;
      off.Y = yi;
      do
      { if(p>0) { x+=xi; p+=ru; }
        else p+=r;
        y+=yi; dy--;
        if(creature.X==x+X && creature.Y==y+Y) break;
        if(!Map.IsPassable(x+X, y+Y) || Math.Sqrt(x*x+y*y)-0.5>light) return Direction.Invalid;
      } while(dy>=0);
    }
    return Global.PointToDir(off);
  }

  public void LoudNoise()
  {
  }

  public virtual void OnAttrChange(Attr attribute, int amount, bool fromExercise) { }
  public virtual void OnDrink(Potion potion) { }
  public virtual void OnDrop(Item item) { }
  public virtual void OnEquip(Wieldable item) { }
  public virtual void OnHit(Entity hit, Weapon w, int damage) { }
  public virtual void OnHitBy(Entity hit, Weapon w, int damage) { }
  public virtual void OnInvoke(Item item) { }
  public virtual void OnKill(Entity killed) { }
  public virtual void OnMiss(Entity hit, Weapon w) { }
  public virtual void OnMissBy(Entity hit, Weapon w) { }
  public virtual void OnNoise(Entity source, Noise type, int volume) { }
  public virtual void OnReadScroll(Scroll scroll) { }
  public virtual void OnRemove(Wearable item) { }
  public virtual void OnRemoveFail(Wearable item) { }
  public virtual void OnSkillUp(Skill skill) { }
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
    if(Age%10==0)
    { if(HP<MaxHP) HP++;
      if(MP<MaxMP) MP++;
    }
  }

  public void Train(Skill skill, bool train)
  { if(skillEnable==null)
    { skillEnable=new bool[(int)Skill.NumSkills];
      for(int i=0; i<skillEnable.Length; i++) skillEnable[i]=true;
    }
    skillEnable[(int)skill]=train;
  }
  public bool Training(Skill skill) { return skillEnable==null || skillEnable[(int)skill]; }

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
      for(int i=0; i<Hands.Length; i++)
        if(Hands[i]!=null && Hands[i].Class==item.Class && TryUnequip(i)) { success=true; break; }
      if(!success) for(int i=0; i<Hands.Length; i++) if(TryUnequip(i)) { success=true; break; }
      if(!success) return false;
    }
    else for(int i=0; i<Hands.Length; i++)
      if(Hands[i]!=null && Hands[i].Class==item.Class && !TryUnequip(i)) return false;
    Equip(item);
    return true;
  }

  public bool TryMove(Direction dir)
  { Point np = Global.Move(Position, dir);
    if(Map.IsPassable(np) && !Map.IsDangerous(np)) { Position = np; return true; }
    return false;
  }
  public bool TryMove(int dir)
  { Point np = Global.Move(Position, dir);
    if(Map.IsPassable(np) && !Map.IsDangerous(np)) { Position = np; return true; }
    return false;
  }
  public bool TryMove(Point pt)
  { if(Map.IsPassable(pt) && !Map.IsDangerous(pt)) { Position = pt; return true; }
    return false;
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

  public Entity[] VisibleCreatures() { return VisibleCreatures(VisibleTiles()); }
  public Entity[] VisibleCreatures(Point[] vis)
  { list.Clear();
    for(int i=0; i<Map.Entities.Count; i++)
      if(Map.Entities[i]!=this)
      { Point cp = Map.Entities[i].Position;
        for(int j=0; j<vis.Length; j++)
          if(vis[j]==cp) { list.Add(Map.Entities[i]); break; }
      }
    return (Entity[])list.ToArray(typeof(Entity));
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
  public int[] Skills = new int[(int)Skill.NumSkills], SkillExp = new int[(int)Skill.NumSkills];
  public Wieldable[] Hands = new Wieldable[2];
  public string Name, Title;
  public Point Position;
  public int    Timer;
  public Map    Map, Memory;
  public Race   Race;
  public Color  Color=Color.Dire;
  public EntityClass Class;

  static public Entity MakeCreature(Type type)
  { Entity c = type.GetConstructor(Type.EmptyTypes).Invoke(null) as Entity;
    App.Assert(c!=null, "{0} is not a valid creature type", type);
    return c;
  }

  static public Entity Generate(Type type, int level) { return Generate(type, level, EntityClass.RandomClass); }
  static public Entity Generate(Type type, int level, EntityClass myClass)
  { Entity creature = MakeCreature(type);
    creature.Generate(level, myClass);
    return creature;
  }

  public static readonly int[][] RaceSkills = new int[(int)Race.NumRaces][]
  { new int[(int)Skill.NumSkills] // human
    { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
      100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
    },
    new int[(int)Skill.NumSkills] // orc
    { 110, 100,  80,  70,  80, // dagger, short, long, axe, mace
       80, 110, 120, 120, 130, // polearm, staff, bow, crossbow, thrown
      120, 120, 150, 160, 160, // summon, enchant, translocate, transform, divine
      100, 100, 115, 110, 100, // channel, necromancy, elemental, poison, locks/traps
      100, 150,  90, 140,  70, // invoke, cast, unarmed, dodge, fight,
       80,  90, 125,           // shield, armor, magicresist
    },
  };

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
    Entity[] creats = VisibleCreatures(vis);
    foreach(Entity c in creats)
    { Tile tile = Memory[c.Position];
      tile.Entity = c;
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

  void TryHit(Entity c, Weapon w)
  { int toHit   = (Dex+EV+1)/2 + (w!=null ? w.ToHitBonus : 0); // average of dex and ev, rounded up
    int toEvade = c.EV;
    int noise   = w==null ? (10-Stealth)*15 : w.Noise*15 - Stealth*8;
    int wepskill = 
      (GetSkill(Skill.Fighting) + (w==null ? GetSkill(Skill.UnarmedCombat) : GetSkill((Skill)w.wClass)) + 1) / 2;

    toHit   += toHit*wepskill*10/100;                  // toHit   +10% per (avg of fighting + weapon) skill level 
    toEvade += toEvade*c.GetSkill(Skill.Dodge)*10/100; // toEvade +10% per dodge level

    toHit   -= toHit  *((int)  HungerLevel*10+99)/100; // effects of hunger -10% per hunger level (rounded up)
    toEvade -= toEvade*((int)c.HungerLevel*10+99)/100;

    c.Exercise(Skill.Dodge);

    int n = Global.Rand(toHit+toEvade);
string msg = string.Format("HIT: (toHit: {0}, EV: {1}, roll: {2} = {3})", toHit, toEvade, n, n>=toEvade ? "hit" : "miss");
    if(n>=toEvade) // hit
    { Shield shield = c.Shield;
      int blockchance=0;
      if(shield!=null)
      { blockchance = shield.BlockChance + (shield.BlockChance*c.GetSkill(Skill.Shields)*10+50)/100;
        c.Exercise(Skill.Shields);
        n = Global.Rand(100);
      }
      else n=0;
      if(shield==null || n*2>=blockchance)  // shield blocks 100% damage half the time that it comes into effect
      { int damage=(w==null ? CalculateDamage() : w.CalculateDamage(this)), ac=c.AC;
        damage += damage * wepskill*10/100; // damage up 10% per skill level
        int odam=damage;
        if(n<blockchance) damage /= 2;      // shield blocks 50% damage the other half of the time
        if(ac>5) c.Exercise(Skill.Armor);   // if wearing substantial armor, exercise it
        n = Global.Rand(ac);
        damage -= n + n*c.GetSkill(Skill.Armor)*10/100; // armor absorbs damage (+10% per skill level)
        if(damage<0) damage = 0;            // normalize damage
        App.IO.Print(Color.DarkGrey, "{4}, DAMAGE: {0} -> {1}, HP: {2} -> {3}", odam, damage, c.HP, c.HP-damage, msg);
        c.HP -= damage;
        c.OnHitBy(this, w, damage);
        OnHit(c, w, damage);
        if(c.HP<=0)
        { c.Die(this, w);
          if(c.HP<=0) // check health again because amulet of saving, etc could have taken effect
          { OnKill(c);
            Exp += c.KillExp;
            ExpPool += Global.NdN(2, c.baseKillExp);
          }
        }
      }
      else App.IO.Print(Color.DarkGrey, msg+" BLOCKED");
    }
    else // miss
    { App.IO.Print(msg);
      c.Exercise(Attr.EV);
      c.OnMissBy(this, w);
      OnMiss(c, w);
      noise /= 2;
    }
    if((this==App.Player || c==App.Player) && noise>0)
      Map.MakeNoise(this==App.Player ? Position : c.Position, this, Noise.Combat, (byte)noise);

    if(w==null) // exercise our battle skills
    { Exercise(Global.Coinflip() ? Attr.Dex : Attr.Str);
      Exercise(Skill.UnarmedCombat);
    }
    else
    { Exercise(w.Exercises);
      Exercise((Skill)w.wClass); // first N skills map directly to weapon class values
      Exercise(Skill.Fighting);
    }
  }

  void VisibleLine(int x2, int y2)
  { int x=0, y=0, dx=Math.Abs(x2), dy=Math.Abs(y2), xi=Math.Sign(x2), yi=Math.Sign(y2), r, ru, p, light=Light;
    if(dx>=dy)
    { r=dy*2; ru=r-dx*2; p=r-dx;
      do
      { if(p>0) { y+=yi; p+=ru; }
        else p+=r;
        x+=xi; dx--;
        if(Math.Sqrt(x*x+y*y)-0.5>light) break;
      } while(dx>=0 && VisiblePoint(x, y));
    }
    else
    { r=dx*2; ru=r-dy*2; p=r-dy;
      do
      { if(p>0) { x+=xi; p+=ru; }
        else p+=r;
        y+=yi; dy--;
        if(Math.Sqrt(x*x+y*y)-0.5>light) break;
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

  int[] attr = new int[(int)Attr.NumAttributes], attrExp = new int[(int)Attr.NumAttributes];
  bool[] skillEnable;
  Flag flags;

  static ArrayList list = new ArrayList();
  static int[] vis = new int[128];
  static int visPts;

  static readonly AttrMods[] raceAttrs = new AttrMods[(int)Race.NumRaces]
  { new AttrMods(6, 6, 6), // Human - 18
    new AttrMods(9, 4, 3)  // Orc   - 16
  };
  static readonly AttrMods[] classAttrs = new AttrMods[(int)EntityClass.NumClasses]
  { new AttrMods(7, 3, -1, 15, 2, 40, 0, 1) // Fighter - 10, 15/2, 40, 0/1
  };
  static readonly ClassLevel[][] classTitles = new ClassLevel[(int)EntityClass.NumClasses][]
  { new ClassLevel[]
    { new ClassLevel(1, "Whacker"), new ClassLevel(4, "Beater"), new ClassLevel(8, "Grunter"),
      new ClassLevel(13, "Fighter"), new ClassLevel(19, "Veteran")
    }
  };
}

} // namespace Chrono
