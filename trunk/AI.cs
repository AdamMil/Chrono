using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

public enum AIState  { Wandering, Idle, Asleep, Patrolling, Attacking, Escaping, Working };

public abstract class AI : Entity
{ protected AI() { }
  protected AI(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public AIState State { get { return state; } }

  public override void Die(object killer, Death cause)
  { if(App.Player.CanSee(this))
    { if(cause==Death.Poison || cause==Death.Sickness || cause==Death.Starvation)
        App.IO.Print(TheName+" falls to the ground, dead.");
      if(killer!=App.Player) App.IO.Print("{0} is killed!", TheName);
    }
    Die();
  }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);

    int si = (int)Race*3;
    Eyesight = raceSenses[si]; Hearing = raceSenses[si+1]; Smelling = raceSenses[si+2];

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

  public bool HostileTowards(Entity e)
  { return e==attacker || e==target || e.SocialGroup==App.Player.SocialGroup &&
                                       (alwaysHostile || SocialGroup!=-1 && Global.GetSocialGroup(SocialGroup).Hostile);
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
  { if(CanSee(attacker)) this.attacker=attacker;
    if(state!=AIState.Attacking && state!=AIState.Escaping)
    { GotoState(AIState.Attacking);
      hitDir = LookAt(attacker.Position);
    }
  }
  public override void OnMissBy(Entity attacker, object item)
  { if(state==AIState.Asleep) return;
    if(CanSee(attacker)) this.attacker=attacker;
    if(state!=AIState.Attacking && state!=AIState.Escaping && Global.Coinflip())
    { GotoState(AIState.Attacking);
      hitDir = LookAt(attacker.Position);
    }
  }
  public override void OnNoise(Entity source, Noise type, int volume)
  { if(state==AIState.Escaping) return;
    volume = volume * Hearing / 100;
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

      if(volume>Map.MaxSound*thresh/100)
      { if(state==AIState.Asleep && App.Player.CanSee(this)) App.IO.Print("{0} wakes up.", TheName);
        if(source.SocialGroup==App.Player.SocialGroup &&
           (alwaysHostile || SocialGroup!=-1 && Global.GetSocialGroup(SocialGroup).Hostile))
        { GotoState(AIState.Attacking);
          shout = false;
        }
        else if(state==AIState.Asleep) GotoState(defaultState);
      }
    }

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
  
  public new void Pickup(IInventory inv, Item item)
  { if(items==null) items=new ArrayList();
    items.Add(base.Pickup(inv, item));
  }

  public override void Think()
  { base.Think();
    if(HP<=0) return;
    if(attacker!=null && attacker.HP<=0) attacker=null;
    if(target!=null && target.HP<=0) target=null;
    if(shout)
    { Map.MakeNoise(Position, this, Noise.Alert, 150);
      shout=false;
    }
    HandleState(state);
    noiseDir=scentDir=Direction.Invalid; maxNoise=0;
  }

  public byte Eyesight, Hearing, Smelling; // effectiveness of these senses, 0-100%
  public byte CorpseChance=30; // chance of leaving a corpse, 0-100%
  public bool HasInventory=true; // whether the monster will pick up and use items
  
  protected enum Combat { None, Melee, Ranged };

  protected Item AddItem(Item item)
  { if(items==null) items=new ArrayList();
    item = Inv.Add(item);
    items.Add(item);
    return item;
  }

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

  protected virtual bool Attack()
  { const int rangedThresh = 3;
    
    Direction dir;
    int dist = target==null ? 0 : Math.Max(Math.Abs(X-target.X), Math.Abs(Y-target.Y));
    bool print = target==App.Player || App.Player.CanSee(Position);

    if(wakeup>0) // if we have a wakeup delay, don't attack yet
    { wakeup--;
      if(dist>=rangedThresh) // but we can prepare for combat
      { if(combat!=Combat.Ranged && PrepareRanged(true)) return true;
      }
      else if(combat!=Combat.Melee && PrepareMelee(true)) return true;
      return false;
    }

    if(target!=null && (dir=LookAt(target.Position))!=Direction.Invalid) // we have a line of sight to the enemy
    { timeout=5; lastDir=dir; // we know where the enemy is! our vigor is renewed!
      // if we know exactly where the target is and we want to use a ranged attack
      if(dist>=rangedThresh || bestWand!=null || Spells!=null)
      { if(bestWand!=null && bestWand.Spell.Range>=dist) // use a wand if possible
        { bool discard = bestWand.Charges==0;
          if(print) App.IO.Print("{0} zaps {1}!", App.Player.CanSee(this) ? TheName : "Something",
                                 bestWand.GetAName(App.Player));
          if(bestWand.Zap(this, target.Position)) { Inv.Remove(bestWand); bestWand=null; }
          else if(discard) bestWand=null;
          return true;
        }

        // if we don't have a wand and the target is close, attack with a melee weapon if we have it. otherwise, if
        // we have no melee weapon (just our fists), consider using a spell or ranged weapon
        if(dist<rangedThresh)
        { if(combat!=Combat.Melee && PrepareMelee(true)) return true;
          else if(combat==Combat.Melee && Weapon!=null) goto melee;
        }

        Spell bestSpell = SelectSpell(dist, target.Position);
        if(bestSpell!=null)
        { if(print) App.IO.Print("{0} casts a spell!", App.Player.CanSee(this) ? TheName : "Something");
          bestSpell.Cast(this, target.Position, Direction.Invalid);
          MP -= bestSpell.Power;
          // FIXME: exercise attributes and skills
          return true;
        }

        if(combat!=Combat.Ranged && PrepareRanged(true)) return true; // switch to ranged weapon if possible
        // TODO: check to make sure the enemy is in range of our weapon
        Weapon w = Weapon;
        Ammo   a = SelectAmmo(w);
        if(a!=null || w!=null && w.wClass==WeaponClass.Thrown)
        { if(print) App.IO.Print("{0} attacks with {1}.", TheName, w.GetAName(App.Player, true));
          Attack(w, a, target.Position); return true;
        }
      }
      if(combat!=Combat.Melee && PrepareMelee(true)) return true; // it's close or we have no ranged attack. use melee.
    }

    melee:
    // try our senses in the orders that are most reliable (sight, sound, scent, memory)
    dir = sightDir!=Direction.Invalid ? sightDir : hitDir!=Direction.Invalid ? hitDir :
          noiseDir!=Direction.Invalid ? noiseDir : scentDir!=Direction.Invalid ? scentDir : lastDir;

    if(dir!=Direction.Invalid) // we have some clue as to the target's direction
    { Direction pdir = lastDir; // lastDir is going to change on the next line, and we need the old value
      lastDir = dir;

      if(target!=null && TryAttack(target, dir, pdir)) return true;
      if(alwaysHostile || SocialGroup!=-1 && Global.GetSocialGroup(SocialGroup).Hostile)
        foreach(Entity e in Global.GetSocialGroup(App.Player.SocialGroup).Entities)
          if(e!=target && TryAttack(e, dir, pdir)) return true;

      hitDir=Direction.Invalid;
      if(timeout==0) { lastDir=Direction.Invalid; GotoState(defaultState); return false; } // we give up

      Point np = Global.Move(Position, dir); // we couldn't find anything to attack, so we try to move towards target
      if(TryMove(np)) return true;
      else if(TryMove(dir-1)) { lastDir--; return true; }
      else if(TryMove(dir+1)) { lastDir++; return true; }
      if(sightDir==Direction.Invalid && noiseDir==Direction.Invalid) timeout--;
    }
    return false;
  }

  protected virtual void Die()
  { attacker=target=null; // FIXME: remove this after we revamp saving/loading
    if(Global.Rand(100)<CorpseChance) Map.AddItem(Position, new Corpse(this));
    for(int i=0; i<Inv.Count; i++) Map.AddItem(Position, Inv[i]);
    Inv.Clear();
    Map.Entities.Remove(this);
    if(SocialGroup!=-1) Global.RemoveFromSocialGroup(SocialGroup, this);
  }

  protected virtual bool DoIdleStuff() // returns true if our turn was used up
  { if(SenseEnemy())
    { if(GotoState(AIState.Attacking)) return HandleState(state);
    }
    int healthpct = HP*100/MaxHP;
    if(healthpct<40 && Heal()) return true;
    if(healthpct<20 && GotoState(AIState.Escaping)) return HandleState(state);
    return PrepareRanged() || ProcessItems() || PickupItems();
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
  { if(w==null || !w.Ranged) return false;
    int score = RangedScore(w);
    if(score>quality)
    { item = w;
      quality = score;
      return true;
    }
    return false;
  }
  
  protected virtual bool EvaluateSpell(Spell s, ref Spell spell, ref int quality)
  { int score = SpellScore(s);
    if(score>quality)
    { spell = s;
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
    if(state!=AIState.Attacking) hitDir=Direction.Invalid;
    timeout = 0;

    if(newstate==AIState.Asleep && App.Player.CanSee(this)) App.IO.Print(TheName+" appears to have fallen asleep.");
    else if(newstate==AIState.Escaping)
    { if(App.Player.CanSee(this)) App.IO.Print(TheName+" turns to flee!");
      lastDir = Direction.Invalid; // force us to calculate a new direction
    }
    else if(newstate==AIState.Attacking)
    { wakeup = state==AIState.Idle || state==AIState.Wandering ? 1 : state==AIState.Asleep ? 2 : 0;
      if(sightDir!=Direction.Invalid || hitDir!=Direction.Invalid) shout=true;
      if(state==AIState.Escaping && App.Player.CanSee(this)) App.IO.Print(TheName+" rejoins the battle!");
      timeout = 5; // we'll be in the attack state for at least 5 turns
      lastDir = Direction.Invalid; // force us to calculate a new direction
    }
    state = newstate;
    return true;
  }

  protected virtual bool HandleState(AIState state) // returns true if our turn was used up
  { switch(state)
    { case AIState.Asleep: return true;
      case AIState.Attacking:
      { SenseEnemy();
        int healthpct = HP*100/MaxHP;
        if(healthpct<40 && Heal()) return true;
        if(healthpct<20 && GotoState(AIState.Escaping)) return HandleState(this.state);
        return ProcessItems() || Attack() || PickupItems();
      }
      case AIState.Escaping:
        bool enemy = SenseEnemy();
        if(HP*100/MaxHP>=75 && GotoState(enemy ? AIState.Attacking : defaultState)) return HandleState(this.state);
        return Heal() || Escape() || Attack();
      case AIState.Idle: return DoIdleStuff();
      case AIState.Patrolling: case AIState.Wandering: return DoIdleStuff() || Wander(); // TODO: implement AIState.Patrol
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
  { if(!HasInventory || Inv.IsFull || !Map.HasItems(Position)) return false;
    Shop shop = Map.GetShop(Position);
    if(shop!=null && shop.Shopkeeper!=null) return false; // don't pick up items in shops (unless the shop is abandoned)

    int weight = CarryWeight, burdenedAt = BurdenedAt*CarryMax/100;
    bool pickup=false;
    if(weight<burdenedAt)
    { ItemPile items = Map[Position].Items;
      for(int i=0; i<items.Count; i++)
      { Item item = items[i];
        if(weight+item.FullWeight<burdenedAt && IsUseful(item))
        { Pickup(items, item); pickup=true;
          weight += item.FullWeight;
          i--; // since Pickup() removes it from the inventory, we need to adjust our index back one
        }
      }
    }
    return pickup;
  }

  protected bool Prepare(Combat type, bool forceEval)
  { return type==Combat.Ranged ? PrepareRanged(forceEval) : PrepareMelee(forceEval);
  }

  protected bool PrepareMelee() { return PrepareMelee(false); }
  protected virtual bool PrepareMelee(bool forceEval)
  { Item item=null;
    int quality=0;
    Weapon w = Weapon;
    if(forceEval || w==null || w.Ranged)
    { foreach(Item i in Inv) if(i.Class==ItemClass.Weapon) EvaluateMelee((Weapon)i, ref item, ref quality);
      EvaluateMelee(null, ref item, ref quality);
      if(item!=w && (item!=null && TryEquip((Wieldable)item) || item==null && (w==null || TryUnequip(w))))
      { combat=Combat.Melee; return true;
      }
    }
    return false;
  }

  protected bool PrepareRanged() { return PrepareRanged(false); }
  protected virtual bool PrepareRanged(bool forceEval)
  { Item item=null;
    int quality=0;
    Weapon w = Weapon;
    if(forceEval || w==null || !w.Ranged)
    { foreach(Item i in Inv) if(i.Class==ItemClass.Weapon) EvaluateRanged((Weapon)i, ref item, ref quality);
      if(item!=w && item!=null && TryEquip((Wieldable)item)) { combat=Combat.Ranged; return true; }
    }
    return false;
  }

  protected virtual bool ProcessItem(Item i) // returns true if our turn was used
  { if(i.Class==ItemClass.Armor || i.Class==ItemClass.Shield)
    { Modifying cur = i.Class==ItemClass.Shield ? (Modifying)Shield : (Modifying)Slots[(int)((Armor)i).Slot];
      int score = cur==null ? 0 : ArmorScore(cur);
      if(ArmorScore((Modifying)i)>score && (cur==null || (i.Class==ItemClass.Shield && TryUnequip(cur) ||
                                                          i.Class!=ItemClass.Shield && TryRemove(cur))))
      { if(i.Class==ItemClass.Shield) Equip((Shield)i);
        else Wear((Armor)i);
        return true;
      }
    }
    else if(i.Class==ItemClass.Weapon)
    { Weapon w = (Weapon)i;
      if(combat==Combat.Ranged && w.Ranged) return PrepareRanged(true);
      else if(combat!=Combat.Ranged && !w.Ranged) return PrepareMelee(true);
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
      if(RingScore((Ring)i)>score) { if(worst!=null) Remove(worst); Wear((Ring)i); return true; }
    }
    else if(i.Class==ItemClass.Wand)
    { Wand ni = (Wand)i;
      int score = bestWand==null ? 0 : WandScore(bestWand);
      if(WandScore(ni)>score) bestWand = ni;
    }
    return false;
  }

  protected bool ProcessItems()
  { if(items==null) return false;
    int i;
    bool processed=false;
    for(i=0; i<items.Count; i++) if(ProcessItem((Item)items[i])) { processed=true; i++; break; }
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

  protected Spell SelectSpell(int distance, Point target)
  { if(Spells==null || Spells.Count==0) return null;
    Spell spell = null;
    int score = 0;
    foreach(Spell s in Spells) if(s.Range>=distance && MP>=s.Power) EvaluateSpell(s, ref spell, ref score);
    return spell;
  }

  protected bool SenseEnemy()
  { if(target!=null && SenseEnemy(target)) return true;     // first try last target
    if(attacker!=null && SenseEnemy(attacker)) return true; // then attacker
    if(alwaysHostile || SocialGroup!=-1 && Global.GetSocialGroup(SocialGroup).Hostile) // then any party member
      foreach(Entity e in Global.GetSocialGroup(App.Player.SocialGroup).Entities)
        if(SenseEnemy(e)) return true;
    return false;
  }

  protected bool SenseEnemy(Entity e)
  { bool dontignore = state==AIState.Attacking || Global.Rand(100)>=e.Stealth*10-3; // ignore stealthy entities
    sightDir = dontignore && Global.Rand(100)<Eyesight ? LookAt(e) : Direction.Invalid; // eyesight
    target = sightDir!=Direction.Invalid ? e : null;
    if(e==App.Player && Smelling>0) // try smell (not dampened by stealth, sound is handled elsewhere)
    { int maxScent=0, scent;
      for(int i=0; i<8; i++)
      { Point np = Global.Move(Position, i);
        Tile t = Map[np];
        if(Map.IsPassable(t.Type) && (scent=Map.GetScent(np))>maxScent) { maxScent=scent; scentDir=(Direction)i; }
      }
      if(maxScent!=100) maxScent = maxScent*Smelling/100; // scale by our smelling ability
      if(state!=AIState.Attacking && maxScent<(Map.MaxScent/10)) // ignore light scents if not alerted
        scentDir=Direction.Invalid;
    }
    else scentDir=Direction.Invalid;
    return sightDir!=Direction.Invalid || noiseDir!=Direction.Invalid || scentDir!=Direction.Invalid;
  }

  protected virtual int SpellScore(Spell spell)
  { if(spell is FireSpell) return 10;
    if(spell is ForceBolt) return 2;
    return 0;
  }

  protected bool UseItem(IInventory inv, Item i)
  { switch(i.Class)
    { case ItemClass.Potion: ((Potion)i).Drink(this); break;
      case ItemClass.Scroll:
      { Scroll s = (Scroll)i;
        OnReadScroll(s);
        s.Spell.Cast(this);
        break;
      }
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

  protected virtual int WandScore(Wand wand)
  { if(wand.Charges==0) return 0;
    if(wand is WandOfFire) return 10;
    return 0;
  }

  protected virtual bool Wander()
  { if(lastDir==Direction.Invalid)
    { for(int i=0; i<8; i++) if(Map.IsPassable(Global.Move(Position, i))) { lastDir=(Direction)i; goto move; }
      return false;
    }
    else if(Global.OneIn(3))
    { if(Global.Coinflip()) lastDir++;
      else lastDir--;
    }

    if(!Map.IsPassable(Global.Move(Position, lastDir)))
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

  protected Entity attacker;      // the entity that last attacked us
  protected Entity target;        // the entity we're attacking
  protected Wand bestWand;        // the best wand for attacking
  protected ArrayList items;      // items we've pickup up but haven't considered yet
  protected int timeout;          // how long we keep trying the current action
  protected int wakeup;           // how long it takes us to get into attack mode
  protected Direction noiseDir=Direction.Invalid;   // the direction of the last noise we heard
  protected Direction sightDir=Direction.Invalid;   // the direction we saw the enemy in
  protected Direction scentDir=Direction.Invalid;   // the direction in which we smell the enemy
  protected Direction hitDir=Direction.Invalid;     // the direction from which we were hit
  protected Direction lastDir=Direction.Invalid;    // the direction we moved/acted in last time
  protected AIState defaultState=AIState.Wandering; // our default state (the one we go to when we have nothing to do)
  protected AIState state=AIState.Wandering;        // our current state
  protected Combat combat;        // the type of combat we're prepared for
  protected byte maxNoise;        // the loudest noise we've heard so far (reset at the end of every turn)
  protected bool alwaysHostile=true; // true if we should attack the player's party regardless of social group considerations
  protected bool shout;           // true if we will shout on our next turn to alert others

  void AddSkills(int points, Skill[] skills)
  { if(skills==null) return;
    int[] skillTable = RaceSkills[(int)Race];
    int max=0, avg=0, range;

    for(int i=0; i<skills.Length; i++)
    { int val = skillTable[(int)skills[i]];
      avg += val;
      if(val>max) max=val;
    }
    avg /= skills.Length;
    range = max+avg;

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
  
  bool TryAttack(Entity e, Direction dir, Direction prevDir)
  { bool usedSight=sightDir!=Direction.Invalid, usedHit=hitDir!=Direction.Invalid,
         usedSound=noiseDir!=Direction.Invalid;
    for(int i=-1; i<=1; i++) // if enemy is in front, attack.
    { Direction nd = (Direction)((int)dir+i);
      if(Map.GetEntity(Global.Move(Position, nd))==e)
      { if(combat!=Combat.Melee && PrepareMelee(true)) return true; // there's something close. goto melee
        if(usedSight || prevDir==nd || Global.Rand(100)<(usedHit?85:usedSound?75:50))
          Attack(Weapon, null, lastDir=nd);
        else if(!usedSight && prevDir!=nd && Map.GetEntity(Global.Move(Position, prevDir))==null)
        { Attack(Weapon, null, prevDir);
          if(App.Player.CanSee(this)) App.IO.Print(TheName+" attacks empty space.");
        }
        timeout = 5; // we attacked something, so our vigor is renewed!
        return true; // but our turn is up
      }
    }
    return false;
  }

  static readonly Skill[][] classSkills = new Skill[(int)EntityClass.NumClasses][]
  { new Skill[] { Skill.Fighting, Skill.Armor, Skill.Dodge, Skill.Shields, Skill.MagicResistance }, // Fighter
    new Skill[] // Wizard
    { Skill.Casting, Skill.Elemental, Skill.Telekinesis, Skill.Translocation, Skill.MagicResistance, Skill.Staff
    },
    null // Worker
  };

  static readonly byte[] raceSenses = new byte[(int)Race.NumRaces*3]
  { // Eyesight, Hearing, Smelling
    95, 70, 25, // Human
    90, 85, 75, // Orc
  };
}

} // namespace Chrono
