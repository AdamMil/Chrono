using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

public enum Attr
{ Str, Dex, Int, NumBasics,
  MaxHP=NumBasics, MaxMP, Speed, AC, EV, Stealth, Light, NumModifiable,
  NumAttributes=NumModifiable,
  
  // not really attributes (only used for Effect)
  Poison, Sickness, Flags
}

public enum CarryStress { Normal, Burdened, Stressed, Overtaxed } // states related to how much we're carrying

public struct Damage
{ public Damage(int damage) { Physical=(ushort)damage; Direct=Heat=Cold=Electricity=Poison=0; }
  public int Total { get { return Direct+Physical+Heat+Cold+Electricity; } } // doesn't count poison
  public void Modify(int percent)
  { Direct += (ushort)(Direct*percent/100);
    Physical += (ushort)(Physical*percent/100);
    Heat += (ushort)(Heat*percent/100);
    Cold += (ushort)(Cold*percent/100);
    Electricity += (ushort)(Electricity*percent/100);
  }
  public ushort Direct, Physical, Heat, Cold, Electricity, Poison;
}

public enum Death { Combat, Falling, Poison, Quit, Starvation, Sickness, Trap } // causes of death

[Serializable]
public struct Effect
{ public Effect(object source, Attr attr, int value, int timeout)
  { Source=source; Attr=attr; Value=value; Timeout=timeout;
  }
  public Effect(object source, Entity.Flag mods, int timeout)
  { Source=source; Attr=Attr.Flags; Value=(int)mods; Timeout=timeout;
  }
  public object Source;
  public Attr Attr;
  public int  Value;
  public int  Timeout;
}

public enum EntityClass
{ Other=-2, // not a monster (boulder or some other entity)
  RandomClass=-1, Fighter, Wizard, NumClasses
}

public enum HungerLevel { Normal, Hungry, Starving, Starved };

public enum Race
{ RandomRace=-1, Human, Orc, NumRaces
}

public enum Skill
{ WeaponSkills,
  // these match weapon classes
  Dagger=WeaponSkills, ShortBlade, LongBlade, Axe, MaceFlail, PoleArm, Staff, Bow, Crossbow, Throwing,

  MagicSkills,
  // these match magic types
  Summoning=MagicSkills, Enchantment, Telekinesis, Translocation, Transformation, Divination, Channeling, Necromancy,
  Elemental, Poison,
  
  GeneralSkills, LocksTraps=GeneralSkills, Invoking, Casting, // general
  FightingSkills, UnarmedCombat=FightingSkills, Dodge, Fighting, Shields, Armor, MagicResistance, // fighting
  
  NumSkills
}

public enum Slot // where an item can be worn
{ Ring=-2, // either ring finger, only valid for Wearable.Slot. not valid for functions like Wearing(Slot) (yet?)
  Invalid=-1, Head, Cloak, Torso, Legs, Neck, Hands, Feet, LRing, RRing, NumSlots
}

public abstract class Entity : UniqueObject
{ [Flags] public enum Flag { None=0, Confused=1, Hallucinating=2, Asleep=4, Invisible=8, SeeInvisible=16 }

  public Entity() { ExpLevel=1; Smell=Map.MaxScentAdd/2; }
  protected Entity(SerializationInfo info, StreamingContext context) : base(info, context) { }

  // all of these apply modifiers from items where applicable
  public int AC { get { return GetAttr(Attr.AC); } }
  public int CarryMax { get { return Str*100; } }
  public CarryStress CarryStress
  { get
    { int pct = CarryWeight*100/CarryMax;
      if(pct<BurdenedAt) return CarryStress.Normal;
      if(pct<StressedAt) return CarryStress.Burdened;
      if(pct<OvertaxedAt) return CarryStress.Stressed;
      return CarryStress.Overtaxed;
    }
  }
  public int CarryWeight { get { return Inv.Weight; } }
  public int Dex { get { return GetAttr(Attr.Dex); } }
  public int DexBonus // general bonus from dexterity (dex <8 is a penalty, >8 is a bonus)
  { get
    { int dex = Dex;
      return dex<8 ? (dex-9)/2 : dex-8;
    }
  }
  public int EV { get { return GetAttr(Attr.EV); } }
  public int Exp
  { get { return exp; }
    set
    { exp = value;
      if(value>=NextExp) LevelUp();
    }
  }
  public int ExpLevel
  { get { return expLevel; }
    set { expLevel=value; Title=GetTitle(); }
  }
  public Flag Flags { get { return flags; } }
  public int Gold
  { get
    { int gold = 0;
      foreach(Item i in Inv) if(i.Class==ItemClass.Gold) gold += i.Count;
      return gold;
    }
  }
  public bool HandsFull
  { get
    { bool full=true;
      for(int i=0; i<Hands.Length; i++)
        if(Hands[i]==null) full=false;
        else if(Hands[i].AllHandWield) return true;
      return full;
    }
  }
  public int HP // will be capped to the maximum
  { get { return hp; }
    set { hp = Math.Min(value, MaxHP); }
  }
  public HungerLevel HungerLevel
  { get
    { return Hunger<HungryAt ? HungerLevel.Normal
                             : Hunger<StarvingAt ? HungerLevel.Hungry
                                                 : Hunger<StarveAt ? HungerLevel.Starving : HungerLevel.Starved;
    }
  }
  public int Int { get { return GetAttr(Attr.Int); } }
  public int KillExp { get { return baseKillExp*ExpLevel; } } // experience given for killing me
  public int Light { get { return GetAttr(Attr.Light); } }
  public int MaxHP { get { return GetAttr(Attr.MaxHP); } }
  public int MaxMP { get { return GetAttr(Attr.MaxMP); } }
  public int MP // will be capped to the maximum
  { get { return mp; }
    set { mp = Math.Min(value, MaxMP); }
  }
  public int NextExp // next experience level
  { get
    { int level = ExpLevel-1;
      return (int)(level<8 ? 100*Math.Pow(1.75, level)
                           : level<20 ? 100*Math.Pow(1.3, level+10)-3000 : 100*Math.Pow(1.18, level+25)+50000) - 25;
    }
  }
  public int Poison
  { get // only one effect can be Poison
    { for(int i=0; i<numEffects; i++) if(effects[i].Attr==Attr.Poison) return effects[i].Value;
      return 0;
    }
  }
  public Shield Shield
  { get
    { for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null && Hands[i].Class==ItemClass.Shield) return (Shield)Hands[i];
      return null;
    }
  }
  public int Sickness
  { get // only one effect can be Sickness
    { for(int i=0; i<numEffects; i++) if(effects[i].Attr==Attr.Sickness) return effects[i].Value;
      return 0;
    }
  }
  public int Smell // player smelliness 0 - Map.MaxScentAdd
  { get { return smell; }
    set { smell = Math.Max(0, Math.Min(value, Map.MaxScentAdd)); }
  }
  public int Speed
  { get
    { int div=1, stress=(int)CarryStress;
      while(stress-->0) div*=2;
      return GetAttr(Attr.Speed) * div;
    }
  }
  public int Stealth { get { return GetAttr(Attr.Stealth); } }
  public int Str { get { return GetAttr(Attr.Str); } }
  public int StrBonus // general bonus from strength (str <10 is a penalty, >10 is a bonus)
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

  public string Prefix // "a " or "an "
  { get
    { if(prefix!=null) return prefix;
      if(Name!=null) return "";
      string name = Race.ToString();
      return Global.AorAn(name)+' ';
    }
  }
  public virtual string AName { get { return Global.Cap1(aName); } }
  public virtual string aName { get { return Name==null ? Prefix+Race.ToString().ToLower() : Name; } }
  public virtual string TheName { get { return Name==null ? "The "+Race.ToString().ToLower() : Name; } }
  public virtual string theName { get { return Name==null ? "the "+Race.ToString().ToLower() : Name; } }

  public void AddEffect(object source, Attr attr, int value, int timeout)
  { AddEffect(new Effect(source, attr, value, timeout));
  }
  public void AddEffect(object source, Entity.Flag mods, int timeout) { AddEffect(new Effect(source, mods, timeout)); }
  public void AddEffect(Effect effect)
  { int i;
    if(effects==null || numEffects==effects.Length)
    { Effect[] narr = new Effect[numEffects==0 ? 4 : numEffects*2];
      if(numEffects>0) Array.Copy(effects, narr, numEffects);
      effects = narr;
    }
    if(effect.Attr==Attr.Sickness || effect.Attr==Attr.Poison) // sickness/poison combines with existing effect
    { for(i=0; i<numEffects; i++) if(effects[i].Attr==effect.Attr) break;
      if(i==numEffects) effects[numEffects++] = effect;
      else
      { effect.Value += effects[i].Value;
        effects[i] = effect;
      }
    }
    else if(effect.Attr==Attr.Flags) // flags effects combine to increase duration
    { for(i=0; i<numEffects; i++) if(effects[i].Attr==effect.Attr && effects[i].Value==effects[i].Value) break;
      if(i==numEffects) { effects[numEffects++] = effect; CheckFlags(); }
      else
      { effect.Timeout += effects[i].Timeout;
        effects[i] = effect;
      }
    }
    else effects[numEffects++] = effect;
  }
  
  public void AddKnowledge(Item item) { AddKnowledge(item, false); }
  public void AddKnowledge(Item item, bool identify)
  { if(Knowledge==null) Knowledge = new Hashtable();
    Knowledge[item.GetType().ToString()] = null;
    if(identify) item.Status |= ItemStatus.Identified|ItemStatus.KnowCB;
  }

  // attack in a direction, can be used for attacking locked doors, etc
  public void Attack(Weapon weapon, Ammo ammo, Direction dir)
  { Attack(weapon, ammo, dir==Direction.Self ? Position : Global.Move(Position, dir), false);
  }
  public void Attack(Weapon weapon, Ammo ammo, Point pt) // attacks a point on the map
  { Attack(weapon, ammo, pt, false);
  }

  TraceAction AttackPoint(Point pt, object context)
  { if(!Map.IsPassable(Map[pt].Type)) return TraceAction.Stop;
    Entity e = Map.GetEntity(pt);
    if(e!=null && TryHit(e, (Item)context)) return TraceAction.Stop;
    return TraceAction.Go;
  }
  public void Attack(Item item, Ammo ammo, Point pt, bool thrown)
  { Weapon w = item as Weapon;
    bool ranged = thrown || pt!=Position && w!=null && (w.Ranged && ammo!=null || w.wClass==WeaponClass.Thrown);
    TraceResult res = ranged ? Global.TraceLine(Position, pt, ammo==null ? Math.Max(30, Str*10/item.Weight) : 30,
                                                false, new LinePoint(AttackPoint), item)
                             : new TraceResult(pt, Map.IsPassable(Map[pt].Type) ? pt : Position);

    Entity c = Map.GetEntity(res.Point);
    bool destroy = false;
    if(c!=null) destroy = Attack(c, item, ammo, ranged, thrown); // if ranged, TryHit has already been called
    else if(w!=null || item==null)
    { if(Map.IsPassable(Map[res.Point].Type))
      { if(this==App.Player && !thrown && ammo==null) App.IO.Print("You swing at thin air.");
      }
      else
      { Damage damage = w==null ? CalculateDamage(null) : w.CalculateDamage(this, ammo, null);
        if(!TryDamageTile(res.Point, damage) && this==App.Player)
        { App.IO.Print("Thunk!"); Map.MakeNoise(res.Point, this, Noise.Bang, 80);
        }
        if(w==null) Exercise(Global.Coinflip() ? Attr.Dex : Attr.Str);
        else Exercise((Skill)w.wClass);
      }
    }
    else if(item!=null) destroy = item.Hit(this, res.Point);

    if(ranged) // remove the item from our hand/quiver
    { if(ammo!=null) item=ammo;
      int staychance = destroy ? 0 : item.Durability==-1 ? item.Weight*5+15 : item.Durability;
      if(item.Count==1)
      { RemoveFromHand(item);
        Inv.Remove(item);
      }
      else item = item.Split(1);
      if(Global.Rand(100)<staychance) // put the item on the ground
        Map.AddItem(c!=null || Map.IsPassable(Map[res.Point].Type) ? res.Point : res.Previous, item);
    }
  }

  public void CancelEffect(int i)
  { bool cf = effects[i].Attr==Attr.Flags;
    for(i++; i<numEffects; i++) effects[i-1]=effects[i];
    numEffects--;
    if(cf) CheckFlags();
  }
  public void CancelEffect(Attr attr)
  { for(int i=0; i<numEffects; i++) if(effects[i].Attr==attr) CancelEffect(i--);
  }
  public void CancelEffect(Flag flag)
  { flag = ~flag;
    for(int i=0; i<numEffects; i++)
      if(effects[i].Attr==Attr.Flags && (effects[i].Value&=(int)flag)==0) CancelEffect(i--);
  }

  // true if item can be removed (not cursed, etc) or slot is empty
  public bool CanRemove(Slot slot) { return Slots[(int)slot]==null || !Slots[(int)slot].Cursed; }
  public bool CanRemove(Wearable item) { return !item.Cursed; }
  // true if there's a line of sight to the given creature/tile
  // FIXME: there's a disparity here. if creature==this, LookAt() says we can't see it, but CanSee says we can
  public bool CanSee(Entity creature) { return this==creature || LookAt(creature) != Direction.Invalid; }
  public bool CanSee(Point point) { return Position==point || LookAt(point) != Direction.Invalid; }
  // true if item can be unequipped (not cursed, etc) or hand is empty
  public bool CanUnequip(int hand) { return Hands[hand]==null || !Hands[hand].Cursed; }
  public bool CanUnequip(Wieldable item) { return !item.Cursed; }

  public void Die(Death cause) { Die(null, cause); }
  public abstract void Die(object killer, Death cause);

  public void DoDamage(Death cause, int damage)
  { Damage d = new Damage();
    d.Direct = (ushort)damage;
    DoDamage(null, cause, d);
  }
  public void DoDamage(Death cause, Damage dam) { DoDamage(null, cause, dam); }
  public void DoDamage(Entity entity, Death cause, Damage dam)
  { //if(HP<=0) return; // TODO: uncomment this later
    DoDamage((object)entity, cause, dam);
    if(HP<=0)
    { entity.OnKill(this);
      entity.GiveKillExp(this);
    }
  }
  public void DoDamage(object killer, Death cause, Damage dam)
  { int ac=AC, n, phys=dam.Physical;

    if(phys>0)
    { Shield shield = Shield;
      int blockchance;
      if(shield!=null)
      { blockchance = shield.BlockChance + (shield.BlockChance*GetSkill(Skill.Shields)*10+50)/100; // +10% PSL rounded
        Exercise(Skill.Shields);
        n = Global.Rand(100);
      }
      else { n=0; blockchance=0; }
      if(n*2>=blockchance)                  // shield blocks 100% damage half the time that it comes into effect
      { int odam=phys;
        if(n<blockchance) phys /= 2;        // shield blocks 50% damage the other half of the time
        if(ac>5) Exercise(Skill.Armor);     // if wearing substantial armor, exercise it
        n = ac>7 ? Global.NdN(4, ac/2)-2 : ac>1 ? Global.NdN(2, AC)-1 : ac;
        phys -= n + n*GetSkill(Skill.Armor)*10/100; // armor absorbs phys (+10% per skill level)
        if(phys<0) phys = 0;                // normalize damage
        App.IO.Print(Color.DarkGrey, "DAMAGE: {0} -> {1}, HP: {2} -> {3}", odam, phys, HP, HP-phys);
        HP -= phys;
      }
      else App.IO.Print(Color.DarkGrey, "BLOCKED");
    }

    HP -= dam.Direct + dam.Heat + dam.Cold + dam.Electricity; // FIXME: no resistance to these yet...

    if(dam.Poison>0) AddEffect(killer, Attr.Poison, dam.Poison, -1);
    if(HP<=0) Die(killer, cause);
  }

  public Item Drop(char c) // drops an item (assumes it's droppable), returns item dropped
  { Item i = Inv[c];
    Drop(i);
    return i;
  }
  // spits and drops an item (assumes it's droppable), returns item dropped
  public Item Drop(char c, int count) { return Drop(Inv[c], count); }
  public void Drop(Item item) // drops an item (assumes it's droppable)
  { Inv.Remove(item);
    Map.AddItem(Position, item);
    item.OnDrop(this);
    OnDrop(item);
  }
  public Item Drop(Item item, int count) // drops an item (assumes it's droppable)
  { if(count==item.Count) Inv.Remove(item);
    else item = item.Split(count);
    Map.AddItem(Position, item);
    item.OnDrop(this);
    OnDrop(item);
    return item;
  }

  public void Equip(Wieldable item) // equips an item (assumes there's room to equip it)
  { if(item.AllHandWield)
      for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) throw new ApplicationException("No room to wield!");
    if(HandsFull) throw new ApplicationException("No room to wield!");
    for(int i=0; i<Hands.Length; i++)
      if(Hands[i]==null)
      { Hands[i] = item;
        OnEquip(item);
        item.OnEquip(this);
        CheckFlags();
        break;
      }
  }

  public bool Equipped(int hand) { return Hands[hand]!=null; } // returns true if the given hand is holding something
  public bool Equipped(Item item) // returns true if the given item is equipped
  { for(int i=0; i<Hands.Length; i++) if(Hands[i]==item) return true;
    return false;
  }

  public void Exercise(Attr attribute) // exercises an attribute (not guaranteed)
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
  public void Exercise(Skill skill) // exercises a skill (not guaranteed)
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
  
  public int GetAttr(Attr attribute) // returns an attribute, applying any bonuses from items, etc
  { int idx=(int)attribute, val = attr[idx];
    if(attribute>=Attr.NumModifiable) return val;
    for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) val += Slots[i].Mods[idx];
    for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) val += Hands[i].Mods[idx];
    for(int i=0; i<numEffects; i++) if(effects[i].Attr==attribute) val += effects[i].Value;
    if(attribute==Attr.Stealth) // stealth is from 0 to 10
    { if(val<0) val=0;
      if(val>10) val=10;
    }
    return val;
  }

  // generates an entity, gives it default stats
  public virtual void Generate(int level, EntityClass myClass)
  { if(myClass==EntityClass.RandomClass)
      myClass = (EntityClass)Global.Rand((int)EntityClass.NumClasses);

    Class = myClass; Title = GetTitle(); SetBaseAttr(Attr.Light, 8);

    int[] mods = raceAttrs[(int)Race].Mods; // attributes for race
    for(int i=0; i<mods.Length; i++) attr[i] += mods[i];

    mods = classAttrs[(int)myClass].Mods;   // attribute modifiers from class
    for(int i=0; i<mods.Length; i++) attr[i] += mods[i];

    int points = 8; // allocate extra points randomly
    while(points>0)
    { int a = Global.Rand((int)Attr.NumBasics);
      if(attr[a]<=17 || Global.Coinflip()) { SetBaseAttr((Attr)a, attr[a]+1); points--; }
    }

    HP = MaxHP; MP = MaxMP;

    while(--level>0) LevelUp();
  }

  public int GetBaseAttr(Attr attribute) { return attr[(int)attribute]; } // gets a raw attribute value (no modifiers)
  public void SetBaseAttr(Attr attribute, int val) // sets a base attribute value
  { attr[(int)attribute]=val;
  }

  public bool GetRawFlag(Flag flag) { return (BaseFlags&flag)!=0; } // gets a raw flag (no modifiers)
  public void SetRawFlag(Flag flag, bool on) // sets a base flag
  { if(on) BaseFlags |= flag; else BaseFlags &= ~flag;
    CheckFlags();
  }

  public int GetSkill(Skill skill) { return Skills[(int)skill]; }
  public void SetSkill(Skill skill, int value) { Skills[(int)skill]=value; }

  // interrupts the creature (something interesting has happened). this will break out of multi-turn loops, etc
  public void Interrupt() { interrupt = true; }

  public void Invoke(Item item) // invoke an item. assumes the item is wielded
  { OnInvoke(item);
    if(item.Invoke(this)) Exercise(Skill.Invoking);
  }

  public bool Is(Flag flag) { return (Flags&flag)!=0; } // returns true if we have the given flag

  // returns true if a monster is within the visible area
  public bool IsCreatureVisible() { return IsCreatureVisible(VisibleTiles()); }
  public bool IsCreatureVisible(Point[] vis)
  { foreach(Entity e in Map.Entities)
      if(e.Class!=EntityClass.Other && e!=this && (!e.Is(Flag.Invisible) || Is(Flag.SeeInvisible)))
      { Point cp = e.Position;
        for(int j=0; j<vis.Length; j++) if(vis[j]==cp) return true;
      }
    return false;
  }

  // call Think() for all items in our inventory
  public void ItemThink()
  { for(int i=0; i<Inv.Count; i++) if(Inv[i].Think(this)) Inv.RemoveAt(i--);
  }

  public bool KnowsAbout(Item item) { return Knowledge!=null && Knowledge.Contains(item.GetType().ToString()); }

  public virtual void LevelDown() { ExpLevel--; } // TODO: make this subtract from stats
  public virtual void LevelUp()
  { int hpGain = attr[(int)Attr.Str]/3+1;
    int mpGain = attr[(int)Attr.Int]/3+1;
    SetBaseAttr(Attr.MaxHP, GetBaseAttr(Attr.MaxHP)+hpGain);
    SetBaseAttr(Attr.MaxMP, GetBaseAttr(Attr.MaxMP)+mpGain);
    HP += hpGain; mp += mpGain;
    ExpLevel++;
  }

  public Direction LookAt(Entity creature) // return the direction to a visible creature or Invalid if not visible
  { if(creature.Is(Flag.Invisible) && !Is(Flag.SeeInvisible)) return Direction.Invalid;
    return LookAt(creature.Position);
  }
  public Direction LookAt(Point pt)
  { int x2 = pt.X-X, y2 = pt.Y-Y, light=Light;
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
        if(pt.X==x+X && pt.Y==y+Y) break;
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
        if(pt.X==x+X && pt.Y==y+Y) break;
        if(!Map.IsPassable(x+X, y+Y) || Math.Sqrt(x*x+y*y)-0.5>light) return Direction.Invalid;
      } while(dy>=0);
    }
    return Global.PointToDir(off);
  }

  public void MemorizeSpell(Spell spell, int memory)
  { if(Spells==null) Spells = new ArrayList();
    Type type = spell.GetType();
    foreach(Spell s in Spells) if(s.GetType()==type) { s.Memory = memory; return; }
    spell = (Spell)type.GetConstructor(Type.EmptyTypes).Invoke(null);
    spell.Memory = memory;
    Spells.Add(spell);
  }

  // event handlers (for output, etc)
  public virtual void OnAttrChange(Attr attribute, int amount, bool fromExercise) { }
  public virtual void OnDrink(Potion potion) { }
  public virtual void OnDrop(Item item) { }
  public virtual void OnEquip(Wieldable item) { }
  public virtual void OnFlagsChanged(Flag oldFlags, Flag newFlags) { }
  public virtual void OnHit(Entity hit, object item, Damage damage) { }
  public virtual void OnHitBy(Entity attacker, object item, Damage damage) { }
  public virtual void OnInvoke(Item item) { }
  public virtual void OnKill(Entity killed) { }
  public virtual void OnMiss(Entity hit, object item) { }
  public virtual void OnMissBy(Entity attacker, object item) { }
  public virtual void OnNoise(Entity source, Noise type, int volume) { }
  public virtual void OnPickup(Item item) { }
  public virtual void OnReadScroll(Scroll scroll) { }
  public virtual void OnRemove(Wearable item) { }
  public virtual void OnRemoveFail(Wearable item) { }
  public virtual void OnSick(string howSick) { }
  public virtual void OnSkillUp(Skill skill) { }
  public virtual void OnUnequip(Wieldable item) { }
  public virtual void OnUnequipFail(Wieldable item) { }
  public virtual void OnWear(Wearable item) { }

  // place item in inventory, assumes it's within reach, already removed from other inventory, etc
  public Item Pickup(Item item)
  { Item ret = Inv.Add(item);
    if(ret!=null)
    { OnPickup(ret);
      ret.OnPickup(this);
    }
    return ret;
  }
  // removes an item from 'inv' and places it in our inventory
  public Item Pickup(IInventory inv, int index)
  { Item item = Pickup(inv[index]);
    if(item!=null) inv.RemoveAt(index);
    return item;
  }
  public Item Pickup(IInventory inv, Item item) // ditto
  { Item ret=Pickup(item);
    if(ret!=null) inv.Remove(item);
    return ret;
  }

  public void Remove(Item item) // removes a worn item (assumes it's being worn)
  { for(int i=0; i<Slots.Length; i++) if(Slots[i]==item) { Remove((Slot)i); return; }
    throw new ApplicationException("Not wearing item!");
  }
  public void Remove(Slot slot) // removes a worn item (assumes it's being worn)
  { Wearable item = Slots[(int)slot];
    Slots[(int)slot] = null;
    OnRemove(item);
    item.OnRemove(this);
    CheckFlags();
  }

  public void RemoveFromHand(Item item)
  { for(int i=0; i<Hands.Length; i++) if(Hands[i]==item) { RemoveFromHand(i); return; }
  }
  public void RemoveFromHand(int hand)
  { Hands[hand] = null;
    CheckFlags();
  }

  public Ammo SelectAmmo(Weapon w)
  { FiringWeapon fw = w as FiringWeapon;
    if(fw==null) return null;
    Compatibility cmp = Compatibility.None;
    Ammo ammo = null;
    foreach(Item i in Inv)
      if(i.Class==ItemClass.Ammo)
      { Ammo ta = (Ammo)i;
        Compatibility tc = fw.CompatibleWith(ta);
        if(tc>cmp) { ammo=ta; if(cmp==Compatibility.Perfect) break; cmp=tc; }
      }
    return ammo;
  }

  public int SpellKnowledge(Spell spell)
  { if(Spells==null) return 0;
    Type type = spell.GetType();
    foreach(Spell s in Spells) if(s.GetType()==type) return s.Memory;
    return 0;
  }

  public virtual void Think() // base Think()
  { Age++;
    Timer-=Speed;
    if(Age%10==0)
    { int maxHP=MaxHP, maxMP=MaxMP;
      if(HP<maxHP) HP += (maxHP+39)/40; // heal 2.5% every 10 turns (rounded up)
      if(MP<maxMP) MP += (maxMP+39)/40;
    }

    bool healthup=false;
    for(int i=0; i<numEffects; i++)
    { Effect e = effects[i];
      if(e.Attr==Attr.Poison || e.Attr==Attr.Sickness)
      { if(Global.Rand(5) < e.Value)
        { string msg;
          if(e.Value>10 && Global.Rand(e.Value)>=8)
          { HP -= 10;
            msg = "extremely sick.";
          }
          else if(e.Value>5 && Global.Coinflip())
          { HP -= Global.Coinflip() ? 3 : 2;
            msg = "very sick.";
          }
          else
          { HP--;
            msg = "sick";
          }
          OnSick(msg);
          if(HP<=0)
          { Die(e.Source, e.Attr==Attr.Poison ? Death.Poison : Death.Sickness);
            Entity killer = e.Source as Entity;
            if(killer!=null) killer.GiveKillExp(this);
          }
          else if(Global.OneIn(HP==1 ? 3 : 8))
          { if(--effects[i].Value<=0) CancelEffect(i--);
            healthup = true;
          }
        }
      }
      else if(--effects[i].Timeout<=0) CancelEffect(i--);
    }
    
    if(healthup && this==App.Player) App.IO.Print(Color.LightGreen, "You feel your health improve.");
  }
  
  public void ThrowItem(Item item, Direction dir)
  { if(dir==Direction.Above || dir==Direction.Below || dir==Direction.Self) Attack(item, null, Position, true);
    else Attack(item, null, Global.Move(Position, dir), true);
  }
  public void ThrowItem(Item item, Point pt) { Attack(item, null, pt, true); }

  public void Train(Skill skill, bool train) // gets/sets whether a skill is being trained
  { if(skillEnable==null)
    { skillEnable=new bool[(int)Skill.NumSkills];
      for(int i=0; i<skillEnable.Length; i++) skillEnable[i]=true;
    }
    skillEnable[(int)skill]=train;
  }
  public bool Training(Skill skill) { return skillEnable==null || skillEnable[(int)skill]; } // true if being trained

  // tries to equip an item. unequips other items as necessary. if 'item' is null, tries to unequip all items
  // true if successful, and false if not. can fail due to cursed items being unremovable, etc.
  // assumes item is not already equipped
  public bool TryEquip(Wieldable item)
  { if(item==null) // unequip all items
    { bool success=true;
      for(int i=0; i<Hands.Length; i++)
        if(Hands[i]!=null && Hands[i].Class==ItemClass.Weapon && !TryUnequip(i)) success=false;
      return success;
    }
    if(item.AllHandWield) // unequip all items so we can equip new one
    { for(int i=0; i<Hands.Length; i++) if(!CanUnequip(i)) return false;
      for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) Unequip(i);
    }
    else
    { for(int i=0; i<Hands.Length; i++) // unequip all items of the same class
        if(Hands[i]!=null && Hands[i].Class==item.Class && !TryUnequip(i)) return false;
      if(HandsFull) // unequip one item to make room
      { int i;
        for(i=0; i<Hands.Length; i++) if(TryUnequip(i)) break; // try any class
        if(i==Hands.Length) return false;
      }
    }
    Equip(item); // finally, equip the new item
    return true;
  }

  // try to move in a direction. will not move if the destination is impassable or dangerous. return true on success
  public bool TryMove(Direction dir) { return TryMove((int)dir); }
  public bool TryMove(int dir)
  { Point np = Global.Move(Position, dir);
    if(Map.IsPassable(np) && !Map.IsDangerous(np)) { Position = np; return true; }
    return false;
  }
  // moves to a position, assuming that position is passable and not dangerous. returns true on success
  public bool TryMove(Point pt)
  { if(Map.IsPassable(pt) && !Map.IsDangerous(pt)) { Position = pt; return true; }
    return false;
  }

  // unequips an item if possible. returns true if item could be unequipped, or it was not equipped in the first place
  public bool TryUnequip(Item item)
  { if(Equipped(item))
    { if(item.Cursed)
      { if(this==App.Player)
        { App.IO.Print("The {0} is stuck to your {1}!", item.Name, item.Class==ItemClass.Shield ? "arm" : "hand");
          item.Status |= ItemStatus.KnowCB;
        }
        return false;
      }
      Unequip(item);
    }
    return true;
  }
  public bool TryUnequip(int hand) { return Equipped(hand) ? TryUnequip(Hands[(int)hand]) : true; }

  // removes a worn item if possible. returns true if item could be removed, or if it was not being worn
  public bool TryRemove(Item item)
  { if(Wearing(item))
    { if(item.Cursed)
      { if(this==App.Player)
        { App.IO.Print("The {0} won't come off!", item.Name);
          item.Status |= ItemStatus.KnowCB;
        }
        return false;
      }
      Remove(item);
    }
    return true;
  }
  public bool TryRemove(Slot slot) { return Wearing(slot) ? TryRemove(Slots[(int)slot]) : true; }

  public bool TrySpellDamage(Spell spell, Point point, Damage damage) // 'damage' is base damage (no modifiers)
  { int skill = GetSkill(spell.Skill);
    damage.Modify(skill*10); // damage up 10% per skill level

    Entity e = Map.GetEntity(point);
    if(e!=null)
    { OnHit(e, spell, damage);
      e.OnHitBy(this, spell, damage);
      e.DoDamage(this, Death.Combat, damage);
      e.Exercise(Skill.MagicResistance);
      return true;
    }
    else return TryDamageTile(point, damage);
  }

  // unequips an item. it's assumed that the item is currently equipped
  public void Unequip(Item item)
  { for(int i=0; i<Hands.Length; i++) if(Hands[i]==item) { Unequip(i); return; }
    throw new ApplicationException("Not wielding "+item);
  }
  public void Unequip(int hand)
  { Wieldable i = Hands[hand];
    Hands[hand] = null;
    OnUnequip(i);
    i.OnUnequip(this);
    CheckFlags();
  }

  // returns a list of creatures visible from this position
  public Entity[] VisibleCreatures() { return VisibleCreatures(VisibleTiles()); }
  public Entity[] VisibleCreatures(Point[] vis)
  { foreach(Entity e in Map.Entities)
      if(e.Class!=EntityClass.Other && e!=this && (!e.Is(Flag.Invisible) || Is(Flag.SeeInvisible)))
      { Point cp = e.Position;
        for(int j=0; j<vis.Length; j++) if(vis[j]==cp) { list.Add(e); break; }
      }
    Entity[] ret = (Entity[])list.ToArray(typeof(Entity));
    list.Clear();
    return ret;
  }

  // returns a list of tiles visible from this position
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

  // puts on an item. assumes the destination slot is free and the item is not already worn
  public void Wear(Wearable item)
  { Slot slot = item.Slot;
    if(slot==Slot.Ring)
    { if(Slots[(int)Slot.LRing]!=null)
      { if(Slots[(int)Slot.RRing]!=null) throw new ApplicationException("Already wearing something!");
        Slots[(int)Slot.RRing] = item;
      }
      else Slots[(int)Slot.LRing] = item;
    }
    else if(Slots[(int)item.Slot]!=null) throw new ApplicationException("Already wearing something!");
    else Slots[(int)item.Slot] = item;
    OnWear(item);
    item.OnWear(this);
    CheckFlags();
  }

  // returns true if the given item is being worn
  public bool Wearing(Slot slot) { return Slots[(int)slot]!=null; }
  public bool Wearing(Item item)
  { for(int i=0; i<Slots.Length; i++) if(Slots[i]==item) return true;
    return false;
  }

  public Inventory  Inv = new Inventory(); // our pack
  public Wearable[] Slots = new Wearable[(int)Slot.NumSlots]; // our worn item slots
  public int[] Skills = new int[(int)Skill.NumSkills], SkillExp = new int[(int)Skill.NumSkills]; // our skills
  public Wieldable[] Hands = new Wieldable[2]; // our hands (currently just 2, but maybe more in the future)
  public ArrayList Spells;    // spells we've learned (don't modify this collection manually)
  public Hashtable Knowledge; // hash table of known item classes
  public string Name, Title;  // Name can be null (our race [eg "orc"] is displayed). Title can be null (no title)
  public Map    Map, Memory;  // the map and our memory of it
  public Point  Position;     // our position within the map
  public int    Timer;        // when timer >= speed, our turn is up
  public Flag   BaseFlags;    // our flags, not counting modifiers
  public Race   Race;         // our race
  public EntityClass Class;   // our class/job
  public Color  Color=Color.Dire; // our general color
  public int Age, ExpPool, Hunger;

  // generates a creature, creates it and calls the creature's Generate() method. class is RandomClass if not passed
  public static Entity Generate(Type type, int level) { return Generate(type, level, EntityClass.RandomClass); }
  public static Entity Generate(Type type, int level, EntityClass myClass)
  { Entity creature = MakeEntity(type);
    creature.Generate(level, myClass);
    return creature;
  }

  public static bool IsFighter(EntityClass c) // returns true if the class is, in general, a fighting class
  { return c==EntityClass.Fighter;
  }

  public static bool IsSpellCaster(EntityClass c) // returns true if the class is, in general, a spellcasting class
  { return c==EntityClass.Wizard;
  }

  public static Entity MakeEntity(Type type) // constructs an entity and returns it
  { Entity c = type.GetConstructor(Type.EmptyTypes).Invoke(null) as Entity;
    App.Assert(c!=null, "{0} is not a valid creature type", type);
    return c;
  }

  // a table of skill experience requirements for level up for races
  public static readonly int[][] RaceSkills = new int[(int)Race.NumRaces][]
  { new int[(int)Skill.NumSkills] // human
    { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
      100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
    },
    new int[(int)Skill.NumSkills] // orc
    { 110, 100,  80,  70,  80, // dagger, short, long, axe, mace
       80, 110, 120, 120, 130, // polearm, staff, bow, crossbow, thrown
      120, 120, 135, 150, 160, // summon, enchant, telekinesis, translocate, transform,
      160, 100, 100, 115, 110, // divine, channel, necromancy, elemental, poison,
      100, 100, 150,  90, 140, // locks/traps, invoke, cast, unarmed, dodge, 
       70,  80,  90, 125,      // fight, shield, armor, magicresist
    },
  };

  // the hunger values at which we're hungry, starving, and dead from starvation
  protected const int HungryAt=1000, StarvingAt=1500, StarveAt=1800;
  // the carry weight percentages at which we're burdened, stressed, and overtaxed
  protected const int BurdenedAt=60, StressedAt=75, OvertaxedAt=90;

  protected bool Attack(Entity c, Item item, Ammo ammo, bool hit, bool thrown)
  { Weapon w = item as Weapon;
    int noise = Math.Min(w!=null ? w.GetNoise(this) : item==null ? (10-Stealth)*15 : item.Weight+30, 255);
    bool destroyed = false;
    hit = hit || c==this || TryHit(c, item);

    if(hit) destroyed = TryDamage(c, item, ammo);
    else noise /= 2;
    if((this==App.Player || c==App.Player) && noise>0)
      Map.MakeNoise(this==App.Player ? Position : c.Position, this, Noise.Combat, (byte)noise);

    if(w==null) // exercise our battle skills
    { Exercise(Global.Coinflip() ? Attr.Dex : Attr.Str);
      Exercise(Skill.UnarmedCombat);
    }
    else
    { Exercise(thrown ? Attr.Str : w.Exercises);
      Exercise(thrown ? Skill.Throwing : (Skill)w.wClass); // first N skills map directly to weapon class values
      Exercise(Skill.Fighting);
      Timer -= Speed*w.Delay/100;
    }

    return destroyed;
  }

  // calculates our unarmed combat damage without skill bonuses
  protected virtual Damage CalculateDamage(Entity target)
  { // FIXME: make this higher for entities skilled in unarmed combat, and lower for those without
    return new Damage(Math.Max(1, StrBonus/2+1));
  }

  protected void CheckFlags() // check if our flags have changed and call OnFlagsChanged if so
  { Flag nf = BaseFlags;
    for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) nf |= Slots[i].FlagMods;
    for(int i=0; i<Hands.Length; i++) if(Hands[i]!=null) nf |= Hands[i].FlagMods;
    for(int i=0; i<numEffects; i++) if(effects[i].Attr==Attr.Flags) nf |= (Flag)effects[i].Value;
    if(nf!=flags) { OnFlagsChanged(flags, nf); flags=nf; Interrupt(); }
  }

  protected void GiveKillExp(Entity killed)
  { Exp += killed.KillExp;
    ExpPool += Global.NdN(2, killed.baseKillExp); // TODO: this probably needs revision
  }

  // called when the creature is added to or removed from a map
  protected internal virtual void OnMapChanged() { }

  protected void UpdateMemory() // updates Memory using the visible area
  { if(Memory==null) return;
    UpdateMemory(VisibleTiles());
  }
  protected void UpdateMemory(Point[] vis)
  { if(Memory==null) return;
    foreach(Point pt in vis)
    { Tile tile = Map[pt];
      tile.Items = tile.Items==null || tile.Items.Count==0 ? null : tile.Items.Clone();
      tile.Node  = Memory[pt].Node;
      Memory[pt] = tile;
    }
    Entity[] creats = VisibleCreatures(vis);
    foreach(Entity c in creats)
    { Tile tile = Memory[c.Position];
      tile.Entity = c;
      Memory[c.Position] = tile;
    }
  }

  protected string prefix;       // my name prefix (usually null)
  protected int baseKillExp;     // the base experience gotten for killing me
  protected bool interrupt;      // true if we've been interrupted

  struct AttrMods
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

  bool TryDamage(Entity c, Item item, Ammo ammo) // returns true if 'item' should be destroyed
  { Weapon w = item as Weapon;
    int wepskill = GetSkill(Skill.Fighting) +
                   (w==null ? GetSkill(item==null ? Skill.UnarmedCombat : Skill.Throwing) : GetSkill((Skill)w.wClass));
    wepskill = (wepskill+1)/2; // average of fighting and weapon skill
    bool destroyed=false;

    Damage dam;
    if(item==null || w!=null) dam=(w==null ? CalculateDamage(c) : w.CalculateDamage(this, ammo, c)); // real weapon (possibly our fists)
    else dam = new Damage((item.Weight+4)/5); // regular item, one damage per pound (rounded up)
    dam.Physical += (ushort)(dam.Physical * wepskill*10/100); // damage up 10% per skill level

    c.OnHitBy(this, item, dam);
    OnHit(c, item, dam);
    if(item!=null) if(item.Hit(this, c)) destroyed=true;
    c.DoDamage(this, Death.Combat, dam);

    return destroyed;
  }
  
  bool TryDamageTile(Point pt, Damage damage)
  { Tile t=Map[pt];
    string msg=null;
    byte noise=0;
    bool effect=false;

    if(t.Type==TileType.ClosedDoor)
    { int pdam = damage.Direct+damage.Physical, hdam = damage.Heat;
      if(pdam+hdam>=10)
      { Map.SetType(pt, TileType.RoomFloor);
        if(hdam>pdam) { msg="The door burns!"; noise=80; }
        else { msg="Crash! You break down the door."; noise=200; }
        effect = true;
      }
    }
    if(!effect) return false;

    if(this==App.Player)
    { if(msg!=null) App.IO.Print(msg);
      if(noise>0) Map.MakeNoise(pt, this, Noise.Bang, noise);
    }
    return true;
  }

  bool TryHit(Entity c, Item item)
  { Weapon w = item as Weapon;
    int toHit   = (Dex+EV+1)/2 + (w!=null ? w.ToHitBonus : 0); // average of dex and ev, rounded up
    int toEvade = c.EV;
    int wepskill = GetSkill(Skill.Fighting) +
                   (w==null ? GetSkill(item==null ? Skill.UnarmedCombat : Skill.Throwing) : GetSkill((Skill)w.wClass));
    wepskill = (wepskill+1)/2; // average of fighting and weapon skill

    toHit   += toHit*wepskill*10/100;                    // toHit   +10% per (avg of fighting + weapon) skill level 
    toEvade += toEvade*c.GetSkill(Skill.Dodge)*10/100;   // toEvade +10% per dodge level

    toHit   -= (toHit  *((int)  HungerLevel*10)+99)/100; // effects of hunger -10% per hunger level (rounded up)
    toEvade -= (toEvade*((int)c.HungerLevel*10)+99)/100;

    int n = Global.Rand(toHit+toEvade);
    App.IO.Print(Color.DarkGrey, "HIT: (toHit: {0}, EV: {1}, roll: {2} = {3})", toHit, toEvade, n, n>=toEvade ? "hit" : "miss");
    if(n<toEvade)
    { c.Exercise(Skill.Dodge);
      c.Exercise(Attr.EV);
      c.OnMissBy(this, item);
      OnMiss(c, item);
      return false;
    }
    return true;
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
  bool[] skillEnable; // are we training these skills?
  Effect[] effects;
  int exp, expLevel, hp, mp, smell, numEffects;
  Flag flags;      // cached set of flags

  static ArrayList list = new ArrayList(); // an arraylist used in some places (ie VisibleCreatures)
  static int[] vis = new int[128]; // vis point buffer
  static int visPts; // number of points in buffer

  static readonly AttrMods[] raceAttrs = new AttrMods[(int)Race.NumRaces] // base stats per race
  { new AttrMods(6, 6, 6), // Human - 18
    new AttrMods(9, 4, 3)  // Orc   - 16
  };
  static readonly AttrMods[] classAttrs = new AttrMods[(int)EntityClass.NumClasses] // stat modifiers per class
  {                                            // CLASS   - Str+Dex+Int, HP/MP, Speed, AC/EV, Stealth
    new AttrMods(7, 3, -1, 15, 2, 40, 0, 1),   // Fighter - 9, 15/2=17, 40, 0/1, 0
    new AttrMods(-1, 3, 7, 9, 8, 45, 0, 0, 1), // Wizard  - 9, 9/8=17,  45, 0/0, 1
  };
  // titles per exp level per class
  static readonly ClassLevel[][] classTitles = new ClassLevel[(int)EntityClass.NumClasses][]
  { new ClassLevel[]
    { new ClassLevel(1, "Whacker"), new ClassLevel(4, "Beater"), new ClassLevel(8, "Grunter"),
      new ClassLevel(13, "Fighter"), new ClassLevel(19, "Veteran")
    },
    new ClassLevel[]
    { new ClassLevel(1, "Neophyte"),
    },
  };
}

} // namespace Chrono
