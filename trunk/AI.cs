using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Chrono
{

public enum AIState  { Wandering, Idle, Asleep, Patrolling, Attacking, Escaping };

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

      if(volume>Map.MaxSound*thresh/100) GotoState(AIState.Attacking);
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
    HandleState(state);
    noiseDir=scentDir=Direction.Invalid; maxNoise=0;
  }

  public byte Eyesight=100, Hearing=100, Smelling=100; // effectiveness of these senses, 0-100%
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
  { int dist = targetPoint.X==-1 ? 0 : Math.Max(X-targetPoint.X, Y-targetPoint.Y);

    if(wakeup>0) // if we have a wakeup delay, don't attack yet
    { wakeup--;
      if(dist>=3) // but we can prepare for combat
      { if(combat!=Combat.Ranged && PrepareRanged(true)) return true;
      }
      else if(combat!=Combat.Melee && PrepareMelee(true)) return true;
      return false;
    }

    if(targetPoint.X!=-1 && CanSee(targetPoint)) // we have a line of sight to the enemy
    { timeout=5; // we know where the enemy is! our vigor is renewed!
      if(dist>=3 || bestWand!=null) // we know exactly where the target is and we want to use a ranged attack
      { if(bestWand!=null && bestWand.Spell.Range>=dist) // use a wand if possible
        { bool discard = bestWand.Charges==0;
          App.IO.Print("{0} zaps {1}!", TheName, bestWand.GetAName(App.Player));
          if(bestWand.Zap(this, targetPoint)) { Inv.Remove(bestWand); bestWand=null; }
          else if(discard) bestWand=null;
          return true;
        }
        if(combat!=Combat.Ranged && PrepareRanged(true)) return true; // switch to ranged weapon
        // TODO: check to make sure the enemy is in range of our weapon
        Weapon w = Weapon;
        Ammo   a = SelectAmmo(w);
        if(a!=null || w!=null && w.wClass==WeaponClass.Thrown)
        { App.IO.Print("{0} attacks with {1}.", TheName, w.GetAName(App.Player, true));
          Attack(w, a, targetPoint); return true;
        }
      }
      if(combat!=Combat.Melee && PrepareMelee(true)) return true; // it's close or we have no ranged attack. use melee.
    }
    
    // try our senses in the orders that are most reliable (sight, sound, scent, memory)
    Direction dir = sightDir!=Direction.Invalid ? sightDir : hitDir!=Direction.Invalid ? hitDir :
                    noiseDir!=Direction.Invalid ? noiseDir : scentDir!=Direction.Invalid ? scentDir : lastDir;

    if(dir!=Direction.Invalid) // we have some clue as to the target's direction
    { bool usedSight=sightDir!=Direction.Invalid, usedHit=hitDir!=Direction.Invalid,
           usedSound=noiseDir!=Direction.Invalid;
      Direction pdir = lastDir, nd; // lastDir is going to change on the next line, and we need the old value
      lastDir = dir;

      for(int i=-1; i<=1; i++) // if enemy is in front, attack. // TODO: prefer target, then any enemy
      { nd = (Direction)((int)dir+i);
        if(Map.GetEntity(Global.Move(Position, nd))==App.Player)
        { if(combat!=Combat.Melee && PrepareMelee(true)) return true; // there's something close. goto melee
          if(usedSight || pdir==nd || Global.Rand(100)<(usedHit?85:usedSound?75:50))
            Attack(Weapon, null, lastDir=nd);
          else if(!usedSight && pdir!=nd && Map.GetEntity(Global.Move(Position, pdir))==null)
          { Attack(Weapon, null, pdir);
            App.IO.Print(TheName+" attacks empty space.");
          }
          timeout = 5; // we attacked something, so our vigor is renewed!
          return true; // but our turn is up
        }
      }

      hitDir=Direction.Invalid;
      if(timeout==0) { lastDir=Direction.Invalid; GotoState(defaultState); return false; } // we give up

      Point np = Global.Move(Position, dir); // we couldn't find anything to attack, so we try to move towards target
      if(TryMove(np)) return true;
      else if(TryMove(dir-1)) { lastDir--; return true; }
      else if(TryMove(dir+1)) { lastDir++; return true; }
      if(!usedSight && !usedSound) timeout--;
    }
    return false;
  }

  protected virtual void Die()
  { attacker=target=null;
    if(Global.Rand(100)<CorpseChance) Map.AddItem(Position, new Corpse(this));
    for(int i=0; i<Inv.Count; i++) Map.AddItem(Position, Inv[i]);
    Inv.Clear();
    Map.Entities.Remove(this);
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
    if(state!=AIState.Attacking) hitDir=Direction.Invalid;
    timeout = 0;

    if(newstate==AIState.Asleep && App.Player.CanSee(this)) App.IO.Print(TheName+" appears to have fallen asleep.");
    else if(newstate==AIState.Escaping)
    { if(App.Player.CanSee(this)) App.IO.Print(TheName+" turns to flee!");
      lastDir = Direction.Invalid; // force us to calculate a new direction
    }
    else if(newstate==AIState.Attacking)
    { wakeup = state==AIState.Idle || state==AIState.Wandering ? 1 : state==AIState.Asleep ? 2 : 0;
      if(sightDir!=Direction.Invalid || hitDir!=Direction.Invalid) Map.MakeNoise(Position, this, Noise.Alert, 150);
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
      case AIState.Idle:   return DoIdleStuff();
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
    { EvaluateMelee(null, ref item, ref quality);
      foreach(Item i in Inv) if(i.Class==ItemClass.Weapon) EvaluateMelee((Weapon)i, ref item, ref quality);
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

  protected bool SenseEnemy()
  { if(target==null) target = App.Player; // TODO: look for any enemy in the area (when we have entity<->entity relationships)
    bool dontignore = state==AIState.Attacking || Global.Rand(100)>=target.Stealth*10-3; // ignore stealthy entities

    sightDir = dontignore && Global.Rand(100)<Eyesight ? LookAt(target) : Direction.Invalid; // eyesight
    targetPoint = sightDir!=Direction.Invalid ? target.Position : new Point(-1, -1);
    if(Smelling>0) // try smell (not dampened by stealth, sound is handled elsewhere)
    { int maxScent=0;
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(Map.IsPassable(t.Type) && t.Scent>maxScent) { maxScent=t.Scent; scentDir=(Direction)i; }
      }
      if(maxScent!=100) maxScent = maxScent*Smelling/100; // scale by our smelling ability
      if(state!=AIState.Attacking && maxScent<(Map.MaxScent/10)) // ignore light scents if not alerted
        scentDir=Direction.Invalid;
    }
    else scentDir=Direction.Invalid;
    return sightDir!=Direction.Invalid || noiseDir!=Direction.Invalid || scentDir!=Direction.Invalid;
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
  { attacker = null;
    return RunAway();
  }

  protected Entity attacker;      // the entity that last attacked us
  protected Entity target;        // the entity we're attacking
  protected Wand bestWand;        // the best wand for attacking
  protected ArrayList items;      // items we've pickup up but haven't considered yet
  protected Point targetPoint;    // where the target is (if we know) or -1,-1 if we don't
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
