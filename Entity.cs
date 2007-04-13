using System;
using System.Collections;
using System.Xml;
using Point=System.Drawing.Point;

namespace Chrono
{

#region Enums
// primarily used to create the message shown when an enemy attacks
public enum AttackType : byte { Bite, Breath, Explosion, Gaze, Kick, Spell, Spit, Sting, Touch, Weapon }

public enum Attr
{ Invalid=-1,
  Str, // influences attack damage, toughness, carry weight, etc
  Dex, // influences accuracy, evasiveness, etc
  Int, // influences spellcasting, puzzlesolving, etc
  NumBasics,

  MaxHP=NumBasics,
  MaxMP,
  Speed,      // 0 - 100 (timer must fill up to 100-Speed before the creature can take a turn)
  AC,         // reduces damage taken
  EV,         // reduces chance of being hit
  Stealth,    // 0 - 100, reduces chance of alerting other monsters
  Smelliness, // amount of stench added to the map each turn
  NumAttributes,

  // these aren't stored in the attribute array

  // quality of eyesight (0-100% chance of seeing a monster in visual range, visual range is Sight/4 tiles)
  Sight=NumAttributes,
  Hearing,    // quality of hearing (0-100% chance of hearing a sound that reaches the ears)
  Smell,      // quality of smell (0-100% chance of smelling a scent that reaches the nose)
}

// states related to how much we're carrying (values indicate percentage of maximum carry weight)
public enum CarryStress { Normal, Burdened=25, Stressed=41, Strained=61, Overtaxed=81, Overloaded=101 }

public enum Death // causes of death
{ Quit, Starvation, Petrified, Disintegrated, Digested, Choking, KilledBy
}

// how to resolve the situation where an identical effect is already in place (NoChange only used for value combination)
public enum EffectCombo { Greater, Lesser, Sum, Replace, KeepOld, NoChange }
public enum EffectType { Ability, Ailment, Attr, Intrinsic, Poison, Sickness }

public enum HungerLevel
{ Starved=0, Fainting=200, Weak=250, Hungry=350, NotHungry=1200, Satiated=1700, Stuffed=2200, Choked
}

public enum Noise { Walking, Bang, Combat, Alert, NeedHelp, Item, Zap } // types of noise that we can make

public enum Race : sbyte // Player means "whatever race the player is"
{ Player=-2, Invalid=-1, Human=0, NumRaces
}

public sealed class Speed
{ Speed() { }
  public const int Stopped=0, Slowest=1, OneFourth=6, Quarter=OneFourth, Half=Quarter*2, Normal=Half*2,
                   Double=Normal*2, TwoPointFive=OneFourth*10, Quadruple=Double*2, Fastest=100;
}

[Flags]
public enum Ailment : byte // (usually) temporary ailments
{ None          =0x00,
  Confused      =0x01, // slight impairment, more like to miss or stagger
  Hallucinating =0x02, // everything looks weird!
  Sleeping      =0x04,
  Blind         =0x08,
  Invisible     =0x10,
  Stunned       =0x20, // serious impairment, quite likely to miss or stagger
}

[Flags]
public enum Ability : ushort // abilities (usually gained), and resistances
{ None            =0x0000,
  SeeInvisible    =0x0001, // can see invisible
  TeleportControl =0x0002, // can teleport at will and control where it teleports to
  PolymorphControl=0x0004, // can polymorph at will and control what it polymorphs into
  Levitating      =0x0008, 
  Warning         =0x0010, // has warning of nearby monsters
  MagicBreath     =0x0020, // can breathe magically
  Reflection      =0x0040, // reflects rays, beams, etc
  Clairvoyant     =0x0080, // can sense creatures that have minds
  Regenerates     =0x0100, // regenerates HP quickly

  FireRes         =0x0200, // resistances
  ColdRes         =0x0400,
  SleepRes        =0x0800,
  ElectricRes     =0x1000,
  PoisonRes       =0x2000,
  AcidRes         =0x4000,
  PetrifyRes      =0x8000,
}

[Flags]
public enum Intrinsic : uint // abilities (usually intrinsic) and body description
{ None          =0x000000,
  CanFly        =0x000001, // can toggle levitation at will
  NoWalk        =0x000002, // cannot traverse land
  CanSwim       =0x000004, // can traverse water
  Amorphous     =0x000008, // can change its body to squeeze through any opening (eg, go under doors, etc)
  Phasing       =0x000010, // can move through solid matter by warping into another dimension
  Tunnels       =0x000020, // digs tunnels
  Breathless    =0x000040, // doesn't need to breathe
  Oviparous     =0x000080, // can lay eggs
  NoCarry       =0x000100, // cannot carry items
  NoHands       =0x000200, // doesn't have hands (can't manipulate items)
  NoLimbs       =0x000400, // can't hit or kick, or wear items
  NoHead        =0x000800, // no head (can't wear helmets, can't be beheaded)
  Mindless      =0x001000, // has (and needs) no mind
  Autorevive    =0x002000, // can revive itself automatically (sometimes)
  Teleportation =0x004000, // can teleport (not at will)
  Acidic        =0x008000, // is acidic if eaten
  Poisonous     =0x010000, // is poisonous if eaten
  Carnivore     =0x020000, // eats meat
  Herbivore     =0x040000, // eats plants
  Omnivore      =Carnivore|Herbivore,
  Metallivore   =0x080000, // eats metal
  Undead        =0x100000,
  Were          =0x200000, // is a lycanthrope
}

[Flags]
public enum AIFlag : byte // AI and other misc flags
{ None        =0x00,
  CantPolyInto=0x01, // player can't polymorph into this creature
  WantsGold   =0x02, // picking up gold is a high priority
  WantsGems   =0x04, // picking up gems is a high priority
  Intelligent =0x08, // intelligent enough to try clever strategies (eg, polymorphing itself)
  Genocided   =0x10, // has been genocided
  Unique      =0x20, // unique creature
  HasData     =0x40, // data has been initialized
}

public enum EntitySize : byte
{ Tiny, Small, Medium, Humanoid=Medium, Large, Huge, Gigantic
}

public enum Gender : byte { Male, Female, Neither, Either }

public enum Skill : byte
{ // these match weapon types
  WeaponSkills, Dagger=WeaponSkills, ShortBlade, LongBlade, Axe, MaceFlail, Polearm, Staff, Bow, Crossbow, Throwing,
  // these match spell types
  MagicSkills, Attack=MagicSkills, Healing, Divination, Enchantment, Summoning, Matter, Escape,
  GeneralSkills, LocksTraps=GeneralSkills, Invoking, Casting,
  FightingSkills, UnarmedCombat=FightingSkills, Dodge, Fighting, MultiWeapon, Shields, Armor, MagicResistance,
  NumSkills
}

public enum Slot : sbyte
{ Ring=-2, // any available ring finger
  Invalid=-1,
  Head, Cloak, Torso, Legs, Neck, Hands, Feet, LRing, RRing, NumSlots
}
#endregion

#region Attack
public struct Attack
{ public Attack(XmlNode node)
  { Type   = (AttackType)Enum.Parse(typeof(AttackType), Xml.Attr(node, "type"));
    Damage = (DamageType)Enum.Parse(typeof(DamageType), Xml.Attr(node, "damage", "Physical"));
    Amount = new Range(node.Attributes["amount"]);
  }

  public Range Amount;
  public AttackType Type;
  public DamageType Damage;
}
#endregion

#region Conference
public struct Conference
{ public Conference(XmlNode node)
  { Ability   = Xml.Abilities(node.Attributes["ability"]);
    Ailment   = Xml.Ailments(node.Attributes["ailment"]);
    Intrinsic = Xml.Intrinsics(node.Attributes["intrinsic"]);
    Chance    = (byte)Xml.Int(node, "chance", 100);
  }

  public Ability   Ability;
  public Ailment   Ailment;
  public Intrinsic Intrinsic;
  public byte      Chance; // 0-100%
}
#endregion

#region Effect
public struct Effect
{ public Effect(Ability ability, bool isOn, int timeout)
    : this(EffectType.Ability, (uint)ability, isOn ? 1 : -1, timeout) { }
  public Effect(Ailment ailment, int timeout) : this(EffectType.Ailment, (uint)ailment, 1, timeout) { }
  public Effect(Intrinsic intrinsic, bool isOn, int timeout)
    : this(EffectType.Intrinsic, (uint)intrinsic, isOn ? 1 : -1, timeout) { }
  public Effect(Attr attr, int value, int timeout) : this(EffectType.Attr, (uint)attr, value, timeout) { }
  public Effect(EffectType type, uint flag, int value, int timeout)
  { Type=type; Flag=flag; Value=value; Timeout=timeout;
  }

  public EffectType Type;
  public uint Flag;
  public int Value, Timeout;
}
#endregion

#region EntityClass
public abstract class EntityClass
{ protected EntityClass()
  { baseName     = "UNNAMED";
    description  = "UNDESCRIBED CREATURE";
    GroupSize    = new Range(1);
    CorpseChance = -1;
    MaxSpawn     = 255;
    color        = Color.White;
    gender       = Gender.Either;
    race         = Race.Invalid;
  }

  public virtual bool CanPass(Entity e, TileType type)
  { switch(type)
    { case TileType.Border:
        return false;
      case TileType.ShallowWater:
        // shallow water is deep for tiny creatures, but others are okay
        return Size!=EntitySize.Tiny || e.HasIntrinsic(Intrinsic.CanSwim|Intrinsic.Phasing);
      case TileType.DeepWater:
        // deep water isn't so deep for gigantic greatures
        return Size==EntitySize.Gigantic || e.HasIntrinsic(Intrinsic.CanSwim|Intrinsic.Phasing);
      case TileType.Altar: case TileType.Corridor: case TileType.DeepIce: case TileType.DirtSand:
      case TileType.DownStairs: case TileType.Forest: case TileType.Grass: case TileType.Hill: case TileType.Hole:
      case TileType.Ice:case TileType.OpenDoor: case TileType.Pit: case TileType.Portal: case TileType.Road:
      case TileType.RoomFloor: case TileType.Town: case TileType.Tree: case TileType.UpStairs:
        return !e.HasIntrinsic(Intrinsic.NoWalk);
      case TileType.ClosedDoor:
        return e.HasIntrinsic(Intrinsic.Amorphous|Intrinsic.Phasing); // amorphous creatures can slip under doors
      case TileType.Mountain:
        return Size>=EntitySize.Huge && !e.HasIntrinsic(Intrinsic.NoWalk) || e.HasIntrinsic(Intrinsic.Phasing);
      case TileType.SolidRock: case TileType.Wall:
        return e.HasIntrinsic(Intrinsic.Tunnels) && !e.HasIntrinsic(Intrinsic.NoWalk) ||
               e.HasIntrinsic(Intrinsic.Phasing);
      case TileType.HardRock: case TileType.HardWall:
        return e.HasIntrinsic(Intrinsic.Phasing);
      default: return true;
    }
  }

  public int GetExpAt(int level)
  { return level<2 ? 0 : level<11 ? 10<<level : level<21 ? 10000<<(level-11) : (level-20)*10000000;
  }

  public int GetLevelAt(int exp)
  { int val, level;

    if(exp<10000) { val=exp/20; level=1; }
    else if(exp<10000000) { val=exp/20000; level=11; }
    else return exp/10000000+20;

    while(val!=0) { level++; val>>=1; }
    return level;
  }

  public int GetHearing(Entity e) { return raceAttrs[(int)GetRace(e)*numRaceAttrs + 3]; }
  public int GetSight(Entity e) { return raceAttrs[(int)GetRace(e)*numRaceAttrs + 4]; }
  public int GetSmell(Entity e) { return raceAttrs[(int)GetRace(e)*numRaceAttrs + 5]; }

  public virtual string GetBaseName(Entity e) { return baseName; }
  public virtual Color GetColor(Entity e) { return color; }
  public virtual string GetDescription(Entity e) { return description; }
  public virtual Gender GetGender(Entity e) { return gender; }
  public virtual Race GetRace(Entity e) { return race==Race.Player ? App.Player.OriginalRace : race; }

  public virtual void Initialize(Entity e)
  { e.RawIntrinsics = Intrinsics;
    e.RawAbilities  = Abilities;
    e.RawAilments   = Ailments;
    e.AIFlags       = AIFlags;
    e.Gender        = gender==Gender.Either ? Global.Coinflip() ? Gender.Female : Gender.Male : gender;
    
    if(race!=Race.Invalid)
    { int index = (int)(race==Race.Player ? App.Player.OriginalRace : race)*numRaceAttrs;
      e.SetBaseAttr(Attr.Str, raceAttrs[index+0]);
      e.SetBaseAttr(Attr.Int, raceAttrs[index+1]);
      e.SetBaseAttr(Attr.Dex, raceAttrs[index+2]);
    }
  }

  public virtual object InitializeData(Entity e) { return null; }

  public virtual bool IsDangerous(Entity e, Point pt)
  { TileType tt = e.Map[pt].Type;
    if(tt!=TileType.Mountain && e.HasAbility(Ability.Levitating)) return false;

    switch(tt)
    { case TileType.DeepWater:
        return Size!=EntitySize.Gigantic && !e.HasIntrinsic(Intrinsic.CanSwim|Intrinsic.Phasing);
      case TileType.Lava:
        return !e.HasAbility(Ability.FireRes);
      case TileType.Mountain:
        return (Size<EntitySize.Huge || e.HasIntrinsic(Intrinsic.NoWalk)) && !e.HasIntrinsic(Intrinsic.Phasing);
      default: return false;
    }
  }

  public virtual void LevelTo(Entity e, int level)
  { Race race = GetRace(e);
    int hpBonus=0, mpBonus=0;

    switch(race)
    { case Race.Human: hpBonus = mpBonus = 1; break;
      default: throw new NotImplementedException("Unhandled race: "+race);
    }

    { int str = e.GetBaseAttr(Attr.Str);
      if(str<4) hpBonus -= 2;
      else if(str<7) hpBonus--;
      else if(str<15) { }
      else if(str<17) hpBonus++;
      else if(str==18) hpBonus += 2;
      else if(str==19) hpBonus += 3;
      else hpBonus += 4;
    }

    int totalHpGain=0, totalMpGain=0;
    while(e.expLevel!=level)
    { int hpGain = Math.Max(1, Global.Rand(1, 8) + hpBonus);
      int mpGain = Math.Max(0, Global.Rand(1, e.GetAttr(Attr.Int)/2+1) + mpBonus);

      if(level<e.XL) { hpGain=-hpGain; mpGain=-mpGain; e.expLevel--; }
      else e.expLevel++;

      totalHpGain += hpGain;
      totalMpGain += mpGain;
    }

    e.AlterBaseAttr(Attr.MaxHP, totalHpGain);
    e.AlterBaseAttr(Attr.MaxMP, totalMpGain);
    if(totalMpGain>0) e.MP += totalMpGain;
  }

  public virtual bool OnDeath(Entity e, Death type, string message)
  { if(type==Death.Quit) return false;

    bool genCorpse;
    if(type==Death.Petrified) throw new NotImplementedException(); // TODO: leave statue
    else if(type==Death.Disintegrated) throw new NotImplementedException(); // TODO: drop only unique/quest items
    else if(CorpseChance!=-1) genCorpse = Global.Rand(100)<CorpseChance;
    else if(Size>=EntitySize.Large) genCorpse = true;
    else genCorpse = Global.OneIn(Size==EntitySize.Tiny ? 4 : 3);

    if(genCorpse) LeaveCorpse(e);
    DropItems(e);
    
    e.Map.Entities.Remove(e);
    return true;
  }

  public virtual void Think(Entity e) { }
  public virtual void Tick(Entity e) { }

  public string Name;
  public Conference[] Confers;
  public Range GroupSize;
  public int CorpseChance, Difficulty, Index, KillExp, Weight, Nutrition; // CorpseChance from 0-100, or -1 for default
  public int MaxSpawn, SpawnChance; // MaxSpawn is maximum number that will be randomly generated
  public Intrinsic Intrinsics;
  public Ability Abilities;
  public Ailment Ailments;
  public AIFlag  AIFlags;
  public EntitySize Size;

  protected void DropItems(Entity e)
  { if(e.Inv!=null)
    { foreach(Item i in e.Inv) e.Map.AddItem(e.Pos, i);
      e.Inv.Clear();
    }
  }

  protected void LeaveCorpse(Entity e) { e.Map.AddItem(e.Pos, Corpse.Make(e)); }

  public string baseName, description;
  protected Race race;
  protected Color color;
  protected Gender gender;

  const int numRaceAttrs = 6;
  static readonly byte[] raceAttrs = new byte[(int)Race.NumRaces*numRaceAttrs]
  { // Str, Int, Dex (sum to ~18), Sight, Hearing, Smell
    6, 6, 6, 95, 70, 25, // Human
  };
}
#endregion

#region Entity
public class Entity
{ public Entity(string name) : this(Global.GetEntityIndex(name)) { }
  public Entity(int index)
  { expLevel=1; this.index=index;
    for(int i=0; i<(int)Attr.NumAttributes; i++) SetBaseAttr((Attr)i, 0); // set attributes to minimum allowed
    Class.Initialize(this);
  }

  public Entity(XmlNode npc) : this(Xml.Attr(npc, "entity"))
  { foreach(XmlNode item in npc.SelectNodes("give")) Pickup(new Item(item));
  }

  public Ability Abilities
  { get { return (Ability)ApplyFlagEffect(EffectType.Ability, (uint)RawAbilities); }
  }
  public Ailment Ailments
  { get { return (Ailment)ApplyFlagEffect(EffectType.Ailment, (uint)RawAilments); }
  }
  public Intrinsic Intrinsics
  { get { return (Intrinsic)ApplyFlagEffect(EffectType.Intrinsic, (uint)RawIntrinsics); }
  }

  public int AC { get { return GetAttr(Attr.AC); } }
  public int Dex { get { return GetAttr(Attr.Dex); } }
  public int EV { get { return GetAttr(Attr.EV); } }
  public int Int { get { return GetAttr(Attr.Int); } }
  public int MaxHP { get { return GetAttr(Attr.MaxHP); } }
  public int MaxMP { get { return GetAttr(Attr.MaxMP); } }
  public int Stealth { get { return GetAttr(Attr.Stealth); } }
  public int Str { get { return GetAttr(Attr.Str); } }
  public int VisionRange { get { return (GetAttr(Attr.Sight)+3)/4; } }

  public int HP
  { get { return hp; }
    set { hp = Math.Min(value, MaxHP); }
  }

  public int MP
  { get { return mp; }
    set { mp = Math.Min(value, MaxMP); }
  }

  public int Speed
  { get
    { int speed=GetAttr(Attr.Speed), pct;
      switch(CarryStress)
      { case CarryStress.Normal: return speed;
        case CarryStress.Burdened: pct=60; break;
        case CarryStress.Strained: pct=40; break;
        case CarryStress.Stressed: pct=33; break;
        case CarryStress.Overtaxed: pct=20; break;
        case CarryStress.Overloaded: pct=10; break;
        default: throw new NotSupportedException("unsupported carry stress");
      }
      return Math.Min(5, speed*pct/100);
    }
  }

  public string AName { get { return Global.Cap1(aName); } }
  public string aName
  { get
    { EntityClass ec = Class;
      if(ec.Name!=null) return Called(ec.Name);
      string baseName = ec.GetBaseName(this);
      return Called(Global.AorAn(baseName)+" "+baseName);
    }
  }
  
  public string TheName
  { get
    { EntityClass ec = Class;
      return Called(ec.Name==null ? "The "+ec.GetBaseName(this) : ec.Name);
    }
  }
  
  public string theName
  { get
    { EntityClass ec = Class;
      return Called(ec.Name==null ? "the "+ec.GetBaseName(this) : ec.Name);
    }
  }

  public EntityClass Class { get { return Global.GetEntityClass(index); } }

  public int CarryWeight { get { return Inv==null ? 0 : Inv.Weight; } }

  public CarryStress CarryStress
  { get
    { int pct = CarryWeight*100/MaxCarryWeight;
      if(pct<(int)CarryStress.Burdened) return CarryStress.Normal;
      else if(pct<(int)CarryStress.Stressed) return CarryStress.Burdened;
      else if(pct<(int)CarryStress.Strained) return CarryStress.Stressed;
      else if(pct<(int)CarryStress.Overtaxed) return CarryStress.Strained;
      else if(pct<(int)CarryStress.Overloaded) return CarryStress.Overtaxed;
      else return CarryStress.Overloaded;
    }
  }

  public int MaxCarryWeight { get { return Str*6048; } } // 6048 grams (13.33 lbs) per Str point

  public object Data
  { get
    { if(!HasAIFlag(AIFlag.HasData))
      { data = Class.InitializeData(this);
        AIFlags |= AIFlag.HasData;
      }
      return data;
    }

    set
    { AIFlags |= AIFlag.HasData;
      data = value;
    }
  }

  // gets or sets our current experience level. this does not print any output when the level is changed
  public int XL
  { get { return expLevel; }
    set
    { if(expLevel!=value)
      { exp = Class.GetExpAt(value);
        LevelTo(value, false);
      }
    }
  }
  
  // gets or sets our total number of experience points. setting this may change our current experience level, but does
  // not print any output when it does so
  public int XP
  { get { return exp; }
    set
    { if(exp!=value)
      { int newLevel = Class.GetLevelAt(value);
        exp = value;
        if(newLevel != XL) LevelTo(newLevel, false);
      }
    }
  }

  public int NextXL { get { return Class.GetExpAt(expLevel+1); } }

  public int Gold { get { return HowMuchGold(false); } }

  public HungerLevel HungerLevel
  { get
    { if(food<(int)HungerLevel.Starved) return HungerLevel.Starved;
      else if(food<(int)HungerLevel.Fainting) return HungerLevel.Fainting;
      else if(food<(int)HungerLevel.Weak) return HungerLevel.Weak;
      else if(food<(int)HungerLevel.Hungry) return HungerLevel.Hungry;
      else if(food<(int)HungerLevel.NotHungry) return HungerLevel.NotHungry;
      else if(food<(int)HungerLevel.Satiated) return HungerLevel.Satiated;
      else return HungerLevel.Stuffed;
    }
  }

  public int Stench
  { get { return stench; }
    set { stench = Math.Max(0, Math.Min(value, Map.MaxScentAdd)); }
  }

  public int Turns { get { return turns; } }

  public int X { get { return Pos.X; } set { Pos.X=value; } }
  public int Y { get { return Pos.Y; } set { Pos.Y=value; } }

  public void AddEffect(Effect effect, EffectCombo timeout, EffectCombo value)
  { for(int i=0; i<numEffects; i++)
      if(effects[i].Type==effect.Type && effects[i].Flag==effects[i].Flag)
      { if(effect.Value != effects[i].Value && // flag effects that cancel out
           (effect.Type==EffectType.Ability || effect.Type==EffectType.Ailment || effect.Type==EffectType.Attr ||
            effect.Type==EffectType.Intrinsic))
        { if(effects[i].Timeout > effect.Timeout) effects[i].Timeout -= effect.Timeout;
          else if(effects[i].Timeout < effect.Timeout) effects[i] = effect;
          else effects[i]=effects[--numEffects];
        }
        else // otherwise, effects that need to be combined
        { int newValue;
          switch(value)
          { case EffectCombo.Greater: newValue = Math.Max(effects[i].Value, effect.Value); break;
            case EffectCombo.KeepOld: newValue = effects[i].Value; break;
            case EffectCombo.Lesser:  newValue = Math.Min(effects[i].Value, effect.Value); break;
            case EffectCombo.NoChange: newValue = 0; break;
            case EffectCombo.Replace: newValue = effect.Value; break;
            case EffectCombo.Sum: newValue = effects[i].Value + effect.Value; break;
            default: throw new NotSupportedException();
          }

          bool keep = false;
          switch(timeout)
          { case EffectCombo.Greater: if(effect.Timeout>=effects[i].Timeout) keep = true; break;
            case EffectCombo.KeepOld: keep = true; break;
            case EffectCombo.Lesser:  if(effect.Timeout<=effects[i].Timeout) keep = true; break;
            case EffectCombo.Sum: effects[i].Timeout += effect.Timeout; keep = true; break;
          }

          if(keep) effects[i] = effect;
          if(value!=EffectCombo.NoChange) effects[i].Value = newValue;
        }
        return;
      }

    if(effects==null || numEffects==effects.Length)
    { Effect[] narr = new Effect[numEffects==0 ? 4 : numEffects*2];
      if(numEffects!=0) effects.CopyTo(narr, 0);
      effects = narr;
    }

    effects[numEffects++] = effect; 
  }

  public int AlterBaseAttr(Attr attribute, int amount)
  { return SetBaseAttr(attribute, GetBaseAttr(attribute)+amount);
  }

  public int GetBaseAttr(Attr attr) // gets a raw attribute value (no modifiers)
  { if(attr<Attr.NumAttributes) return attrs[(int)attr];
    else
      switch(attr)
      { case Attr.Hearing: return Class.GetHearing(this);
        case Attr.Sight:   return Class.GetSight(this);
        case Attr.Smell:   return Class.GetSmell(this);
        default: throw new ArgumentException("Unknown attribute: "+attr);
      }
  }

  public int SetBaseAttr(Attr attribute, int value) // sets a base value
  { return attrs[(int)attribute] = (int)ClipAttrValue(attribute, value);
  }

  public bool CanPass(TileType type) { return Class.CanPass(this, type); }
  public bool CanSee(Entity e) { return LookAt(e)!=Direction.Invalid; }
  public bool CanSee(Point pt) { return LookAt(pt)!=Direction.Invalid; }

  public bool Die(Death type)
  { switch(type)
    { case Death.Choking: return OnDeath(Death.Choking, "choked to death");
      case Death.Quit: return OnDeath(Death.Quit, "quit");
      case Death.Starvation: return OnDeath(Death.Starvation, "starved to death");
      default: throw new NotSupportedException();
    }
  }

  public bool Die(string killedBy) { return OnDeath(Death.KilledBy, "killed by "+killedBy); }

  public int DistanceTo(Entity e) { return DistanceTo(e.Pos); } // ignores walls that are in the way, etc
  public int DistanceTo(Point pt) { return Math.Min(Math.Abs(X-pt.X), Math.Abs(Y-pt.Y)); }

  public void Drop(Item item) // removes an item from the inventory and drops it
  { Inv.Remove(item);
    if(OnDrop(item))
    { item.OnDrop(this);
      Map.AddItem(Pos, item);
    }
  }

  public Item Drop(Item item, int count) // removes an item from the inventory and drops it
  { if(count==item.Count) Inv.Remove(item);
    else item = item.Split(count);
    if(OnDrop(item))
    { item.OnDrop(this);
      Map.AddItem(Pos, item);
    }
    return item;
  }

  // returns true if the given hand is holding something
  public bool Equipped(Item item) // returns true if the given item is equipped
  { if(Hands==null) return false;
    for(int i=0; i<Hands.Length; i++) if(Hands[i]==item) return true;
    return false;
  }

  public virtual void Abuse(Attr attr) { }
  public virtual void Exercise(Attr attr) { }
  public virtual void Exercise(Skill skill) { }

  public HungerLevel GainNutrition(int amount)
  { if(this!=App.Player)
    { food += amount;
      return HungerLevel;
    }
    else
    { HungerLevel oldLevel = HungerLevel;
      food += amount;
      HungerLevel newLevel = HungerLevel;

      if(oldLevel!=newLevel)
      { if(oldLevel<newLevel) // gaining food
        { if(newLevel==HungerLevel.Stuffed) App.IO.Print("You're having a hard time getting it all down.");
          else if(newLevel==HungerLevel.Choked)
          { App.IO.Print("You choke over your food.");
            Die(Death.Choking);
          }
        }
        else // losing food
        { if(newLevel==HungerLevel.Hungry) App.IO.Print(Color.Warning, "You feel hungry.");
          else if(newLevel==HungerLevel.Weak) App.IO.Print(Color.Warning, "You need food, badly!");
          else if(newLevel==HungerLevel.Fainting) App.IO.Print(Color.Dire, "You feel lightheaded.");
          else if(newLevel==HungerLevel.Starved)
          { App.IO.Print("You faint from hunger! You don't wake up.");
            Die(Death.Starvation);
          }
        }
      }

      return newLevel;
    }
  }

  public int GetAttr(Attr attr)
  { float value = GetBaseAttr(attr);

    if(Slots!=null)
      for(int i=0; i<Slots.Length; i++) if(Slots[i]!=null) value = Slots[i].Modify(attr, value);

    if(Hands!=null)
    { Item prevHand = null;
      for(int i=0; i<Hands.Length; i++)
        if(Hands[i]!=null && Hands[i]!=prevHand)
        { prevHand = Hands[i];
          value = prevHand.Modify(attr, value);
        }
    }

    for(int i=0; i<numEffects; i++)
      if(effects[i].Type==EffectType.Attr && (Attr)effects[i].Flag==attr)
        value += effects[i].Value;

    return (int)Math.Round(ClipAttrValue(attr, value));
  }

  public int GetEffectValue(EffectType type)
  { for(int i=0; i<numEffects; i++) if(effects[i].Type==type) return effects[i].Value;
    return 0;
  }

  public Item GetGold(int amount, bool acceptLess)
  { if(!acceptLess && amount>Gold) return null;
    return GetGold(amount, Inv);
  }

  public Item GetSlot(Slot slot) { return Slots==null ? null : Slots[(int)slot]; }

  public virtual int GetSkill(Skill skill) { throw new NotImplementedException(); }

  public bool HasAbility(Ability flag) { return (Abilities&flag)!=0; }
  public bool HasAbilities(Ability flag) { return (Abilities&flag)==flag; }

  public bool HasAIFlag(AIFlag flag) { return (AIFlags&flag)!=0; }
  public bool HasAIFlags(AIFlag flag) { return (AIFlags&flag)==flag; }

  public bool HasAilment(Ailment flag) { return (Ailments&flag)!=0; }
  public bool HasAilments(Ailment flag) { return (Ailments&flag)==flag; }

  public bool HasIntrinsic(Intrinsic flag) { return (Intrinsics&flag)!=0; }
  public bool HasIntrinsics(Intrinsic flag) { return (Intrinsics&flag)==flag; }

  // returns how much gold we have in our pack. if 'recurse' is true, it recurses into containers that the pack contains
  public int HowMuchGold(bool recurse) { return Inv==null ? 0 : HowMuchGold(Inv, recurse); }

  public void Invoke(Item item) // invoke an item
  { OnInvoke(item);
    if(item.Invoke(this)) Inv.Remove(item);
  }

  // returns true if a given point on the map would be dangerous to traverse (contains a known trap or would damage us)
  public bool IsDangerous(Point pt) { return Class.IsDangerous(this, pt); }

  // returns true if we are or can see the given entity
  public bool IsOrSees(Entity e) { return this==e || CanSee(e); }

  // levels the character up or down to the given level. if 'display' is true, a message may be printed regarding the
  // change
  public void LevelTo(int level, bool display)
  { if(level==expLevel) return;

    int oldLevel = expLevel;
    Class.LevelTo(this, level);

    if(display)
    { if(this==App.Player)
      { if(level>oldLevel) App.IO.Print("You feel more experienced! Welcome to level {0}.", level);
        else App.IO.Print("You just lost some experience! Goodbye level {0}.", oldLevel);
      }
      else if(App.Player.CanSee(this))
        App.IO.Print("{0} looks {1} experienced.", TheName, level>oldLevel ? "more" : "less");
    }
  }

  // returns the direction that would bring us closest to the entity if we can see it, or Direction.Invalid otherwise
  public Direction LookAt(Entity e)
  { if(Map!=e.Map || e.HasAilment(Ailment.Invisible) && !HasAbility(Ability.SeeInvisible) ||
       HasAilment(Ailment.Blind) && (!HasAbilities(Ability.Clairvoyant) || e.HasIntrinsic(Intrinsic.Mindless)))
      return Direction.Invalid;
    return LookAt(e.Pos);
  }

  // return the direction that brings us closest to 'pt' if we can see it, or Direction.Invalid otherwise
  public Direction LookAt(Point pt)
  { if(pt==Pos) return Direction.Self;

    int x2 = pt.X-X, y2 = pt.Y-Y, light=VisionRange;
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
        if(!CanPass(Map[x+X, y+Y].Type) || Math.Sqrt(x*x+y*y)-0.5>light) return Direction.Invalid;
        if(pt.X==x+X && pt.Y==y+Y) break;
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
        if(!CanPass(Map[x+X, y+Y].Type) || Math.Sqrt(x*x+y*y)-0.5>light) return Direction.Invalid;
        if(pt.X==x+X && pt.Y==y+Y) break;
      } while(dy>=0);
    }

    return Global.OffsetToDir(off);
  }

  public void MemorizeSpell(Spell spell) { MemorizeSpell(spell, -1); }
  public void MemorizeSpell(Spell spell, int memory)
  { if(Spells==null) Spells = new SpellMemory[1] { new SpellMemory(spell, memory) };
    else
    { for(int i=0; i<Spells.Length; i++)
        if(Spells[i].Spell==spell)
        { if(memory==-1) Spells[i].Memory = -1;
          else Spells[i].Memory = Math.Max(memory, Spells[i].Memory);
          return;
        }
      SpellMemory[] narr = new SpellMemory[Spells.Length+1];
      Spells.CopyTo(narr, 0);
      narr[Spells.Length] = new SpellMemory(spell, memory);
      Spells = narr;
    }
  }

  public virtual void OnAttrChange(Attr attr, int amount, bool fromExercise) { }
  public virtual bool OnDeath(Death type, string message) { return Class.OnDeath(this, type, message); }

  public virtual bool OnDrop(Item item) // returns 'true' if the item should be added to the map
  { Shop shop = Map.GetShop(Pos);
    if(shop!=null && shop.Shopkeeper!=null) item.Shop = shop; // gives the item to the shopkeeper, if there is one
    return true;
  }

  public virtual void OnDrink(Item item) { }
  public virtual void OnInvoke(Item item) { }
  public virtual void OnMapChange(Map map) { Map = map; } // called by Map.EntityCollection
  public virtual void OnNoise(Entity source, Noise type, int volume) { }
  public virtual void OnPickup(Item item, IInventory from) { }
  public virtual void OnUnequip(Item item) { }
  public virtual void OnUse(Item item) { }

  // place an item in our inventory. assumes it's within reach, already removed from the other inventory, etc.
  // returns the new inventory item if it was able to be added and null otherwise
  public Item Pickup(Item item) { return Pickup(item, null); }
  public Item Pickup(Item item, IInventory from)
  { // we use an itempile for non-player entities so it doesn't re-key the items
    if(Inv==null) Inv = this is Player ? new Inventory() : (IInventory)new ItemPile();
    OnPickup(item, from);
    Item ret = Inv.Add(item);
    if(ret!=null) ret.OnPickup(this);
    return ret;
  }
  // removes an item from 'inv' and places it in our inventory. return the new inventory item if it was able to be
  // added, and null if it could not (eg, inventory is full)
  public Item Pickup(IInventory inv, int index)
  { Item item = Pickup(inv[index], inv);
    if(item!=null) inv.RemoveAt(index);
    return item;
  }

  public Item Pickup(IInventory inv, Item item) // ditto
  { Item ret = Pickup(item, inv);
    if(ret!=null) inv.Remove(item);
    return ret;
  }

  public virtual void Think()
  { int turn = ++turns;
    Class.Think(this);
    
    // each turn, consume base amount (~10% as much while sleeping) if carnivorous or herbivorous
    int food = 0;
    if(HasIntrinsic(Intrinsic.Carnivore|Intrinsic.Herbivore) && (!HasAilment(Ailment.Sleeping) || Global.OneIn(10)))
      switch(Class.Size)
      { case EntitySize.Tiny: food = Global.OneIn(3) ? 1 : 0; break;
        case EntitySize.Small: food = Global.Coinflip() ? 1 : 0; break;
        case EntitySize.Medium: food = 1; break;
        case EntitySize.Large: food = Global.Coinflip() ? 2 : 1; break;
        case EntitySize.Huge: food = 2; break;
        case EntitySize.Gigantic: food = 3; break;
      }

    if((turn&1)!=0) // on odd turns
    { if(HasAbility(Ability.Regenerates)) food++; // regeneration takes an additional one every odd turn
      if(CarryStress>=CarryStress.Stressed) food++; // plus, we need more food when carrying more
    }
    int mod20 = turn%20+1;
    if(mod20==4)
    { Item ring = GetSlot(Slot.LRing);
      if(ring!=null) food += ((Ring)ring.Class).ExtraHunger; // extra food on 4th turn of 20 if wearing left ring
    }
    else if(mod20==8 && GetSlot(Slot.Neck)!=null) food++; // extra food on 8th turn of 20 if wearing amulet
    else if(mod20==12)
    { Item ring = GetSlot(Slot.RRing);
      if(ring!=null) food += ((Ring)ring.Class).ExtraHunger; // extra food on 12th turn of 20 if wearing right ring
    }

    GainNutrition(-food);
  }

  public virtual void Tick()
  { if(Inv!=null) Tick(Inv);
    Class.Tick(this);
  }

  public void TrySpellDamage(Spell spell, Entity caster, Item item, ref Damage damage)
  { throw new NotImplementedException();
  }

  // unequips an item if possible. returns true if item could be unequipped, or it was not equipped in the first place
  public bool TryUnequip(Item item)
  { if(Equipped(item))
    { if(item.Cursed)
      { if(this==App.Player)
        { App.IO.Print("The {0} is stuck to your {1}!", item.GetFullName(),
                       item.Type==ItemType.Shield ? "arm" : "hand");
          item.Status |= ItemStatus.KnowCB;
        }
        else if(App.Player.CanSee(this))
        { App.IO.Print("{0} is unable to drop {1}!", TheName, item.GetFullName());
          item.Status |= ItemStatus.KnowCB;
        }
        return false;
      }

      Unequip(item);
    }
    return true;
  }

  // unequips an item. it's assumed that the item is currently equipped
  public void Unequip(Item item)
  { for(int i=0; i<Hands.Length; i++)
      if(Hands[i]==item)
      { Hands[i] = null;
        OnUnequip(item);
        item.OnUnequip(this);
        CheckFlags();
        for(int j=i+1; j<Hands.Length; j++) if(Hands[j]==item) Hands[j] = null; // handle multi-hand items
        return;
      }
    throw new ArgumentException("Not wielding "+item);
  }

  // returns a list of creatures visible from this position
  public Entity[] VisibleCreatures() { return VisibleCreatures(VisibleTiles()); }
  public Entity[] VisibleCreatures(Point[] vis) // FIXME: take into account clairvoyance, etc
  { foreach(Entity e in Map.Entities)
      if(e!=this && (!e.HasAilment(Ailment.Invisible) || HasAbility(Ability.SeeInvisible)))
        for(int j=0; j<vis.Length; j++) if(vis[j]==e.Pos) { list.Add(e); break; }
    Entity[] ret = (Entity[])list.ToArray(typeof(Entity));
    list.Clear();
    return ret;
  }

  // returns a list of tiles visible from this position
  public Point[] VisibleTiles()
  { int x=0, y=VisionRange*4, s=1-y;
    visPts = 0;
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

  // returns true if the given item is being worn
  public bool Wearing(Slot slot) { return Slots!=null && Slots[(int)slot]!=null; }
  public bool Wearing(Item item)
  { if(Slots==null) return false;
    for(int i=0; i<Slots.Length; i++) if(Slots[i]==item) return true;
    return false;
  }

  public IInventory Inv; // our pack (null = no pack [for creatures that can't carry items])
  public Map Map; // the map we're on
  public Item[] Slots, Hands; // places to equip items (null = unable to equip them)
  public SpellMemory[] Spells;

  public string Name;
  public Point Pos; // our position within the map
  public int Timer; // our turn timer. each tick, our Speed is added to it. when it reaches 100, we can take a turn

  public Intrinsic RawIntrinsics;
  public Ability RawAbilities;
  public Ailment RawAilments;
  public AIFlag AIFlags;
  public Gender Gender;

  protected void CheckFlags() // check if our flags have changed and call OnFlagsChanged if so
  { throw new NotImplementedException();
  }

  uint ApplyFlagEffect(EffectType type, uint flags)
  { for(int i=0; i<numEffects; i++)
    { if(effects[i].Type==type)
      { if(effects[i].Value==1) flags |= effects[i].Flag;
        else flags &= ~effects[i].Flag;
      }
    }
    return flags;
  }

  string Called(string baseName) { return Name==null ? baseName : baseName + " (called " + Name + ")"; }

  Item GetGold(int amount, IInventory inv)
  { Item ret = null;
    ArrayList remove = null;

    foreach(Item i in inv)
      if(i.Type==ItemType.Gold)
      { if(i.Count>amount)
        { if(ret==null) ret = i.Split(amount);
          else { ret.Count+=amount; i.Count-=amount; }
          return ret;
        }
        else
        { if(remove==null) remove = new ArrayList();
          remove.Add(i);
          if(ret==null) ret = (Item)i;
          else ret.Count += i.Count;
          amount -= i.Count;
        }
      }

    foreach(Item i in remove) inv.Remove(i);

    if(amount>0)
      foreach(Item i in inv)
        if(i.Type==ItemType.Container)
        { Item gold = GetGold(amount, (IInventory)i.Data);
          if(gold!=null)
          { if(ret==null) ret = gold;
            else ret.Count += gold.Count;
            amount -= gold.Count;
            if(amount==0) break;
          }
        }

    return ret;
  }

  int HowMuchGold(IInventory container, bool recurse)
  { int total = 0;
    foreach(Item item in container)
    { if(item.Type==ItemType.Gold) total += item.Count;
      else if(recurse && item.Type==ItemType.Container) total += HowMuchGold((IInventory)item.Data, recurse);
    }
    return total;
  }

  void Tick(IInventory container)
  { ArrayList remove = null;
    foreach(Item i in container)
    { if(i.Tick(this, container))
      { if(remove==null) remove = new ArrayList();
        remove.Add(i);
      }
      else if(i.Type==ItemType.Container) Tick((IInventory)i.Data);
    }
    if(remove!=null) foreach(Item i in remove) container.Remove(i);
  }

  void VisibleLine(int x2, int y2)
  { int x=0, y=0, dx=Math.Abs(x2), dy=Math.Abs(y2), xi=Math.Sign(x2), yi=Math.Sign(y2), r, ru, p, light=VisionRange;
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
  { x += Pos.X; y += Pos.Y;
    TileType type = Map[x, y].Type;
    if(type==TileType.Border) return false;

    if(visPts==vis.Length)
    { int[] narr = new int[visPts*2];
      Array.Copy(vis, narr, visPts);
      vis = narr;
    }

    int ti = y*Map.Width+x;
    for(int i=0; i<visPts; i++) if(vis[i]==ti) goto ret; // TODO: this seems slow
    vis[visPts++] = ti;
    ret: return Map.IsUsuallyPassable(type);
  }

  internal int expLevel;
  object data;
  Effect[] effects;
  int[] attrs = new int[(int)Attr.NumAttributes];
  int exp, index, hp, mp, food, stench, turns, numEffects;

  static float ClipAttrValue(Attr attr, float value)
  { if(value<0) value = 0;
    else
      switch(attr)
      { case Attr.Speed: case Attr.Stealth: case Attr.Sight: case Attr.Hearing: case Attr.Smell:
          if(value>100) value = 100;
          break;
        case Attr.EV: case Attr.Str: case Attr.Int: case Attr.Dex:
          if(value>25) value = 25;
          else if(value<1) value = 1;
          break;
      }
    return value;
  }

  static ArrayList list = new ArrayList(); // an arraylist used in some places (ie VisibleCreatures)
  static int[] vis = new int[128]; // vis point buffer
  static int visPts; // number of points in buffer
}
#endregion

#region SpellMemory
public struct SpellMemory
{ public SpellMemory(Spell spell, int memory) { Spell=spell; Memory=memory; }
  public readonly Spell Spell;
  public int Memory;
}
#endregion

#region XmlEntityClass
public class XmlEntityClass : EntityClass
{ protected XmlEntityClass() { }

  public XmlEntityClass(XmlNode node, Hashtable idcache)
  { XmlDocument doc = node.OwnerDocument;
    Stack stack = new Stack();
    
    stack.Push(node);
    while(node.Attributes["inherit"]!=null)
    { string bid = Xml.Attr(node, "inherit");
      node = (XmlNode)idcache[bid];
      if(node==null)
      { node = doc.SelectSingleNode("//entity[@id='"+bid+"']");
        if(node==null) throw new ArgumentException("No such entity: "+bid);
        idcache[bid] = node;
      }
      stack.Push(node);
    }
    
    Difficulty = 1;

    ArrayList attacks=new ArrayList(), resists=new ArrayList(), confers=new ArrayList(), items=new ArrayList();
    do
    { node = (XmlNode)stack.Pop();
      foreach(XmlAttribute attr in node.Attributes)
        switch(attr.Name)
        { case "abilities": Abilities = Xml.Abilities(attr); break;
          case "aiFlags": AIFlags = Xml.AIFlags(attr); break;
          case "ailments": Ailments = Xml.Ailments(attr); break;
          case "chance": SpawnChance = int.Parse(attr.Value); break;
          case "color": color = Xml.Color(attr); break;
          case "corpseChance": CorpseChance = int.Parse(attr.Value); break;
          case "difficulty": Difficulty = int.Parse(attr.Value); break;
          case "fullName": Name = attr.Value; break;
          case "gender": gender = Xml.Gender(attr); break;
          case "id": idcache[attr.Value] = node; break;
          case "inherit": break;
          case "intrinsics": Intrinsics = Xml.Intrinsics(attr); break;
          case "killExp": KillExp = int.Parse(attr.Value); break;
          case "maxSpawn": MaxSpawn = int.Parse(attr.Value); break;
          case "name": baseName = attr.Value; break;
          case "nutrition": Nutrition = int.Parse(attr.Value); break;
          case "race": race = Xml.Race(attr); break;
          case "size": Size = Xml.EntitySize(attr); break;
          case "spawnSize": GroupSize = new Range(attr); break;
          case "weight": Weight = Xml.Weight(attr); break;

          case "str": case "dex": case "int": case "maxHP": case "maxMP": case "speed": case "ac": case "ev":
          case "stealth": case "light": case "sight": case "hearing": case "smell":
            BaseAttributes[(int)(Attr)Enum.Parse(typeof(Attr), attr.Name, true)] = new Range(attr);
            break;

          default:
            if(extraAttrs==null) extraAttrs = new SortedList();
            extraAttrs[attr.Name] = attr.Value;
            break;
        }

      foreach(XmlNode child in node.ChildNodes)
        if(child.NodeType==XmlNodeType.Element)
          switch(child.LocalName)
          { case "attack":
            { Attack a = new Attack(child);
              if(a.Type==AttackType.Weapon) HasWeaponAttack = true;
              else if(a.Type==AttackType.Spell) HasSpellAttack = true;
              else attacks.Add(a);
              break;
            }
            case "confer": confers.Add(new Conference(child)); break;
            case "description": description = Xml.BlockToString(node.InnerText); break;
            case "give": items.Add(child); break;
          }
    } while(stack.Count!=0);

    InitialItems = items.Count==0 ? null : (XmlNode[])items.ToArray(typeof(XmlNode));
    Attacks = attacks.Count==0 ? null : (Attack[])attacks.ToArray(typeof(Attack));
    Confers = confers.Count==0 ? null : (Conference[])confers.ToArray(typeof(Conference));
  }

  public override void Initialize(Entity e)
  { base.Initialize(e);
    for(int i=0; i<BaseAttributes.Length; i++) e.SetBaseAttr((Attr)i, BaseAttributes[i].RandValue());
    if(InitialItems!=null) foreach(XmlNode node in InitialItems) e.Pickup(new Item(node));
  }

  public Range[] BaseAttributes = new Range[(int)Attr.NumAttributes];
  public XmlNode[] InitialItems;
  public Attack[] Attacks;
  public bool HasWeaponAttack, HasSpellAttack;

  protected string GetExtraAttr(string name) { return extraAttrs==null ? null : (string)extraAttrs[name]; }

  IDictionary extraAttrs;

  public static EntityClass Make(XmlNode node, Hashtable idcache)
  { string type = Xml.Attr(node, "type");
    if(Xml.IsEmpty(type)) return new XmlEntityClass(node, idcache);
    else throw new NotImplementedException();
  }
}
#endregion

} // namespace Chrono