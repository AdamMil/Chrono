using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

public enum AIState  { Wandering, Idle, Asleep, Patrolling, Attacking, Escaping };

public abstract class AI : Entity
{ public AI() { defaultState=state=AIState.Wandering; Eyesight=Hearing=Smelling=100; }
  protected AI(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public AIState State { get { return state; } }

  public override void Die(object killer, Death cause)
  { if((cause==Death.Poison || cause==Death.Sickness || cause==Death.Starvation) && App.Player.CanSee(this))
      App.IO.Print(TheName+" falls to the ground, dead.");
    Die();
  }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);
    if(--level==0) return;

    Skill[] skills = classSkills[(int)myClass];
    AddSkills(100*level, skills);

    if(myClass==EntityClass.Fighter) // then do more general skills
    { skills = new Skill[(int)WeaponClass.NumClasses];
      for(int i=0; i<skills.Length; i++) skills[i] = (Skill)i; // first N weapon classes are mapped directly to skills
    }
    else if(myClass==EntityClass.Wizard)
    { skills = new Skill[(int)SpellClass.NumClasses];
      for(int i=0; i<skills.Length; i++) skills[i] = (Skill)i + (int)Skill.MagicSkills;
    }
    else skills=null;
    AddSkills(300*level, skills);
  }

  public override void OnDrink(Potion potion) { Does("drinks", potion); }
  public override void OnDrop(Item item) { Does("drops", item); }
  public override void OnEquip(Wieldable item)
  { if(App.Player.CanSee(this))
    { App.IO.Print("{0} equips {1}.", TheName, item.GetAName(App.Player));
      if(item.Cursed)
      { App.IO.Print("The {0} welds itself to {1}'s {2}!", item.Name, TheName,
                     item.Class==ItemClass.Shield ? "arm" : "hand");
        item.Status |= ItemStatus.KnowCB;
      }
    }
  }
  public override void OnFlagsChanged(Chrono.Entity.Flag oldFlags, Chrono.Entity.Flag newFlags)
  { Flag diff = oldFlags ^ newFlags;
    if((diff&(Flag.Asleep|Flag.Invisible))!=0 && App.Player.CanSee(this))
    { if((diff&newFlags&Flag.Asleep)!=0) App.IO.Print(TheName+" wakes up.");
      if((diff&Flag.Invisible)!=0) // invisibility changed
      { bool seeInvis = (App.Player.Flags&Flag.SeeInvisible)!=0;
        if((newFlags&Flag.Invisible)!=0 && !seeInvis) App.IO.Print(TheName+" vanishes from sight.");
        else if((newFlags&Flag.Invisible)==0 && !seeInvis) App.IO.Print(TheName+" reappears!");
      }
    }
  }
  public override void OnHitBy(Entity attacker, object item, Damage damage)
  { base.OnHitBy(attacker, item, damage);
    if(state!=AIState.Attacking) GotoState(AIState.Attacking);
  }
  public override void OnMissBy(Entity attacker, object item)
  { base.OnMissBy(attacker, item);
    if(state!=AIState.Asleep && state!=AIState.Attacking && Global.Coinflip()) GotoState(AIState.Attacking);
  }
  public override void OnNoise(Entity source, Noise type, int volume)
  { volume = volume * Hearing / 100;
    if(volume==0) return;

    if(state!=AIState.Attacking)
    { int thresh; // volume threshold above which the sound will be noticed (in percent of maximum sound level)
      switch(type)
      { case Noise.Alert: case Noise.NeedHelp: thresh=4; break;
        case Noise.Walking: thresh=8; break;
        case Noise.Item: thresh=6; break;
        default: thresh=5; break;
      }
      switch(state)
      { case AIState.Asleep: thresh *= 4; break;                       // 1/4 as likely to notice if asleep
        case AIState.Idle: case AIState.Wandering: thresh *= 2; break; // 1/2 as likely to notice if unalert
        case AIState.Patrolling: thresh += thresh/2; break;            // 2/3 as likely to notice if patrolling
      }
      if(volume>Map.MaxSound*thresh/100) GotoState(AIState.Attacking);
    }

    byte maxNoise = 0;
    if(state==AIState.Attacking)
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(t.Sound>maxNoise) { maxNoise=t.Sound; noiseDir=(Direction)i; }
      }
  }
  public override void OnPickup(Item item) { Does("picks up", item); }
  public override void OnReadScroll(Scroll scroll) { Does("reads", scroll); }
  public override void OnRemove(Wearable item) { Does("removes", item); }
  public override void OnSick(string howSick)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} looks {1}.", TheName, howSick);
  }
  public override void OnSkillUp(Skill skill)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} looks more experienced.", TheName);
  }
  public override void OnUnequip(Wieldable item) { Does("unequips", item); }
  public override void OnWear(Wearable item) { Does("puts on", item); }
  
  public new void Pickup(Item item)
  { if(items==null) items=new ArrayList();
    items.Add(base.Pickup(item));
  }

  public override void Think()
  { base.Think();
    HandleState(state);
  }

  public byte Eyesight, Hearing, Smelling; // effectiveness of these senses, 0-100%
  public byte CorpseChance; // chance of leaving a corpse, 0-100%
  public bool HasInventory; // whether the monster will pick up and use items
  
  protected enum Combat { Ranged, Melee };

  protected virtual int AmuletScore(Amulet amulet)
  { int score = 0;

    if(amulet.KnownCursed) score -= 4; // blessed/cursed status (uses player's knowledge)
    else if(amulet.KnownBlessed) score += 2;
    else if(amulet.KnownUncursed) score++;
    
    return score;
  }

  protected virtual int ArmorScore(Modifying armor)
  { int score = 0;

    if(armor.KnownCursed) score -= 4; // blessed/cursed status (uses player's knowledge)
    else if(armor.KnownBlessed) score += 2;
    else if(armor.KnownUncursed) score++;

    score += armor.AC + armor.EV;
    
    if(Entity.IsSpellCaster(Class)) // we don't want penalties
    { if(armor.Class==ItemClass.Armor && ((Armor)armor).Material>=Material.HardMaterials) score -= 10;
      else if(armor.Class==ItemClass.Shield) score -= 3;
    }

    return score;
  }

  protected virtual void Die()
  { if(Global.Rand(100)<CorpseChance) Map.AddItem(Position, new Corpse(this));
    for(int i=0; i<Inv.Count; i++) Map.AddItem(Position, Inv[i]);
    Map.Entities.Remove(this);
  }

  protected virtual bool DoIdleStuff() // returns true if our turn was used up
  { if(SenseEnemy())
    { if(GotoState(AIState.Attacking)) return HandleState(AIState.Attacking);
    }
    int healthpct = HP*100/MaxHP;
    if(healthpct<40 && Heal()) return true;
    if(healthpct<20 && GotoState(AIState.Escaping)) return HandleState(AIState.Escaping);
    if(PrepareRanged()) return true;
    if(ProcessItems()) return true;
    return PickupItems();
  }

  protected void Does(string verb, Item item)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} {1} {2}.", TheName, verb, item.GetAName(App.Player));
  }

  protected virtual bool Escape()
  { IInventory inv=null;
    Item item=null;
    int quality=0;
    if(FindEscapeItem(Inv, ref item, ref quality)) inv=Inv;
    if(Map.HasItems(Position) && FindEscapeItem(Map[Position].Items, ref item, ref quality))
      inv=Map[Position].Items;
    if(item!=null) { UseItem(inv, item); return true; }
    return RunAway();
  }

  protected virtual bool EvaluateMelee(Weapon w, ref Item item, ref int quality)
  { int score = MeleeScore(w);
    if(score>quality)
    { item = w;
      quality = score;
      return true;
    }
    return false;
  }

  protected virtual bool EvaluateRanged(Weapon w, ref Item item, ref int quality)
  { int score = w!=null && w.Ranged ? RangedScore(w) : MeleeScore(w)/3;
    if(score>quality)
    { item = w;
      quality = score;
      return true;
    }
    return false;
  }

  protected virtual bool FindEscapeItem(IInventory inv, ref Item item, ref int quality)
  { bool found=false;
    foreach(Item i in inv)
    { if(quality<5)
      { if(i is TeleportScroll) { item=i; quality=5; found=true; }
      }
    }
    return found;
  }

  protected virtual bool FindHealItem(IInventory inv, ref Item item, ref int quality)
  { bool found=false;
    foreach(Item i in inv)
    { if(i is HealPotion) { item=i; quality=1; found=true; }
    }
    return found;
  }

  protected virtual bool GotoState(AIState newstate) // returns true if we switched to the specified state
  { if(newstate==state) return true;
    if(newstate==AIState.Asleep && App.Player.CanSee(this)) App.IO.Print(TheName+" appears to have fallen asleep.");
    else if(newstate==AIState.Escaping)
    { if(App.Player.CanSee(this)) App.IO.Print(TheName+" turns to flee!");
      lastDir = Direction.Invalid; // force us to calculate a new direction
    }
    else if(newstate==AIState.Attacking && state==AIState.Escaping && App.Player.CanSee(this))
      App.IO.Print(TheName+" rejoins the battle!");
    state = newstate;
    return true;
  }

  protected virtual bool HandleState(AIState state) // returns true if our turn was used up
  { switch(state)
    { case AIState.Asleep: return true;
      case AIState.Idle:   return DoIdleStuff();
      case AIState.Patrolling: case AIState.Wandering: return DoIdleStuff() ? true : Wander(); // TODO: implement AIState.Patrol
    }
    return false;
  }

  protected virtual bool Heal()
  { IInventory inv=null;
    Item item=null;
    int quality=0;
    if(FindHealItem(Inv, ref item, ref quality)) inv=Inv;
    if(Map.HasItems(Position) && FindHealItem(Map[Position].Items, ref item, ref quality)) inv=Map[Position].Items;
    if(item!=null) { UseItem(inv, item); return true; }
    return false;
  }

  protected bool IsUseful(Item i)
  { return i.Class!=ItemClass.Container && i.Class!=ItemClass.Corpse && i.Class!=ItemClass.Food &&
           i.Class!=ItemClass.Spellbook;
  }

  protected virtual int MeleeScore(Weapon w)
  { if(w==null) return StrBonus/4 + GetSkill(Skill.UnarmedCombat)*2;

    int score = 0;
    if(w.Ranged) score = 1;
    else
    { if(w.KnownCursed) score -= 4; // blessed/cursed status (uses player's knowledge)
      else if(w.KnownBlessed) score += 2;
      else if(w.KnownUncursed) score++;

      score += w.ToHitMod+w.DamageMod; // weapon modifiers
      
      switch(w.wClass) // base damage
      { case WeaponClass.Dagger: score += 1; break;
        case WeaponClass.Axe:    score += 3; break;
        case WeaponClass.LongBlade: case WeaponClass.MaceFlail: score += 4; break;
        case WeaponClass.PoleArm: score += 5; break;
        default: score += 2; break;
      }
    }

    score += GetSkill((Skill)w.wClass); // skill with the weapon. first N skills map to weapon classes

    return score;
  }

  protected virtual bool PickupItems()
  { if(Inv.IsFull || !Map.HasItems(Position)) return false;

    int weight = CarryWeight;
    bool pickup=false;
    if(weight<BurdenedAt)
      foreach(Item i in Map[Position].Items)
        if(weight+i.FullWeight<BurdenedAt && IsUseful(i)) { Pickup(i); pickup=true; weight += i.FullWeight; }
    return pickup;
  }

  protected virtual bool PrepareMelee()
  { Item item=null;
    int quality=0;
    Weapon w = Weapon;
    if(w==null || w.Ranged)
    { EvaluateMelee(null, ref item, ref quality);
      foreach(Item i in Inv) if(i.Class==ItemClass.Weapon) EvaluateMelee((Weapon)i, ref item, ref quality);
      if(item!=null && TryEquip((Wieldable)item)) return true;
      if(w!=null && TryUnequip(w)) return true;
    }
    return false;
  }
  
  protected virtual bool PrepareRanged()
  { Item item=null;
    int quality=0;
    Weapon w = Weapon;
    if(w==null || !w.Ranged)
    { foreach(Item i in Inv) if(i.Class==ItemClass.Weapon) EvaluateRanged((Weapon)i, ref item, ref quality);
      if(item!=null && TryEquip((Wieldable)item)) return true;
      if(w!=null && TryUnequip(w)) return true;
    }
    return false;
  }

  protected virtual bool ProcessItem(Item i) // returns true if our turn was used
  { if(i.Class==ItemClass.Armor || i.Class==ItemClass.Shield)
    { Modifying cur = i.Class==ItemClass.Shield ? (Modifying)Shield : (Modifying)Slots[(int)((Armor)i).Slot];
      int score = cur==null ? 0 : ArmorScore(cur);
      if(ArmorScore((Modifying)i)>score && (i.Class==ItemClass.Shield && TryUnequip(cur) ||
                                            i.Class!=ItemClass.Shield && TryRemove(cur)))
      { if(i.Class==ItemClass.Shield) Equip((Shield)cur);
        else Wear((Armor)cur);
        return true;
      }
    }
    else if(i.Class==ItemClass.Amulet)
    { Amulet cur = (Amulet)Slots[(int)Slot.Neck];
      int score  = cur==null ? 0 : AmuletScore(cur);
      if(AmuletScore((Amulet)i)>score && TryRemove(cur)) { Wear((Amulet)i); return true; }
    }
    else if(i.Class==ItemClass.Ring)
    { Ring worst = (Ring)Slots[(int)Slot.LRing], ring2 = (Ring)Slots[(int)Slot.RRing];
      int score  = worst==null ? 0 : CanRemove(worst) ? int.MaxValue : RingScore(worst),
          score2 = ring2==null ? 0 : CanRemove(ring2) ? int.MaxValue : RingScore(ring2);
      if(score2<score) worst=ring2;
      if(RingScore((Ring)i)>score) { Remove(worst); Wear((Ring)i); return true; }
    }
    return false;
  }

  protected bool ProcessItems()
  { int i;
    bool processed=false;
    for(i=0; i<items.Count; i++) if(ProcessItem((Item)items[i])) { processed=true; break; }
    items.RemoveRange(0, i);
    return processed;
  }

  protected virtual int RangedScore(Weapon w)
  { if(w==null) return GetSkill(Skill.UnarmedCombat)+1;
    if(w is FiringWeapon && SelectAmmo(w)==null) return 0;

    int score = 0;
    if(w.KnownCursed) score -= 4; // blessed/cursed status (uses player's knowledge)
    else if(w.KnownBlessed) score += 2;
    else if(w.KnownUncursed) score++;

    score += w.ToHitMod+w.DamageMod; // weapon modifiers

    switch(w.wClass) // base damage
    { case WeaponClass.Bow: score += 2; break;
      case WeaponClass.Crossbow: score += 3; break;
      default: score += 1; break;
    }
    
    score += GetSkill((Skill)w.wClass); // skill with the weapon. first N skills map to weapon classes

    return score;
  }

  protected virtual int RingScore(Ring ring)
  { int score = 0;

    if(ring.KnownCursed) score -= 4; // blessed/cursed status (uses player's knowledge)
    else if(ring.KnownBlessed) score += 2;
    else if(ring.KnownUncursed) score++;
    
    if(ring is InvisibilityRing) score += 8; // specific rings
    else if(ring is SeeInvisibleRing) score += 3;

    return score;
  }

  protected virtual bool RunAway() // attempts to run away from our attacker
  { if(attacker!=null)
    { Direction dir = LookAt(attacker);
      if(dir!=Direction.Invalid) lastDir = (Direction)((int)(dir+4)%8); // run in the opposite direction
    }
    if(lastDir==Direction.Invalid)
    { for(int i=0; i<8; i++) if(Map.IsPassable(Global.Move(Position, i))) { lastDir=(Direction)i; goto move; }
      return false;
    }
    else if(!Map.IsPassable(Global.Move(Position, lastDir)))
    { for(int i=1; i<=4; i++)
      { if(Map.IsPassable(Global.Move(Position, (int)lastDir+i))) { lastDir=(Direction)((int)lastDir+i); goto move; }
        else if(Map.IsPassable(Global.Move(Position, (int)lastDir-i)))
        { lastDir=(Direction)((int)lastDir-i);
          goto move;
        }
      }
      return false;
    }
    move: Position = Global.Move(Position, lastDir);
    return true;
  }

  protected bool UseItem(IInventory inv, Item i)
  { switch(i.Class)
    { case ItemClass.Potion: ((Potion)i).Drink(this); break;
      case ItemClass.Scroll: ((Scroll)i).Spell.Cast(this); break;
      default:
        if(i.Usability==ItemUse.Self)
        { i.Use(this);
          break;
        }
        return false;
    }
    if(i.Count>1) i.Split(1);
    else inv.Remove(i);
    return true;
  }

  protected virtual bool Wander()
  { attacker = null;
    return RunAway();
  }
  
  protected Entity attacker;      // the entity that last attacked us
  protected Direction noiseDir;   // the direction of the last noise we heard
  protected Direction lastDir;    // the direction we moved/acted in last time
  protected AIState defaultState; // our default state (the one we go to when we have nothing to do)
  protected AIState state;        // our current state
  protected ArrayList items;      // items we've pickup up but haven't considered yet

  void AddSkills(int points, Skill[] skills)
  { int[] skillTable = RaceSkills[(int)Race];
    int min=int.MaxValue, max=0, range;

    for(int i=0; i<skills.Length; i++)
    { int val = skillTable[(int)skills[i]];
      if(val<min) min=val; if(val>max) max=val;
    }
    range = max*2-min;

    do
      for(int i=0; i<skills.Length; i++)
      { int si=(int)skills[i], need = skillTable[si], ti=Math.Min(points, 100);
        if(Global.Rand(range)>need)
        { points -= ti;
          if((SkillExp[si]+=ti) >= need)
          { SkillExp[si] -= need;
            Skills[si]++;
          }
          if(points==0) break;
        }
      }
    while(points>0);
  }
  
  static readonly Skill[][] classSkills = new Skill[(int)EntityClass.NumClasses][]
  { new Skill[] { Skill.Fighting, Skill.Armor, Skill.Dodge, Skill.Shields }, // Fighter
    new Skill[] { Skill.Casting, Skill.Elemental, Skill.Telekinesis, Skill.Translocation, Skill.Staff }, // Wizard
  };
}

} // namespace Chrono
