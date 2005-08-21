using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chrono
{

public enum AIState : byte
{ Wandering, Idle, Asleep, Patrolling, Guarding, Attacking, Escaping, Working, Following
};

public sealed class Attack // sync with entities.xsd
{ public Attack(XmlNode node)
  { Name = Xml.Attr(node, "name");
    if(Name!=null)
    { ArrayList list = new ArrayList();
      foreach(XmlNode child in node.ChildNodes)
        if(child.NodeType==XmlNodeType.Element)
          list.Add(new Attack(child));
      Parts = (Attack[])list.ToArray(typeof(Attack));
    }
    else
    { Type = (AttackType)Enum.Parse(typeof(AttackType), Xml.Attr(node, "type"));
      Damage = (DamageType)Enum.Parse(typeof(DamageType), Xml.Attr(node, "damage", "Physical"));
      Amount = new Range(node.Attributes["amount"]);
    }

    Chance = (byte)Xml.Int(node, "chance");
  }

  public string Name;
  public Attack[] Parts;

  public Range Amount;
  public AttackType Type;
  public DamageType Damage;
  public byte Chance; // 0-100%
}

public enum AttackType : byte // sync with entities.xsd
{ Bite, Breath, Explosion, Gaze, Kick, Spell, Spit, Sting, Touch, Weapon
}

public struct Conference // sync with entities.xsd
{ public Conference(XmlNode node)
  { Type = (DamageType)Enum.Parse(typeof(DamageType), Xml.Attr(node, "type"));
    Chance = (byte)Xml.Int(node, "chance", 100);
  }

  public DamageType Type;
  public byte Chance; // 0-100%
}

public enum DamageType : byte // sync with common.xsd
{ Acid, Cold, Direct, DrainDex, DrainInt, DrainStr, Electricity, Fire, Magic, Physical, Poison, Slow,
  Stun, Paralyse, Petrify, Teleport, StealGold, StealItem, Blind
}

public struct Resistance // sync with common.xsd
{ public Resistance(XmlNode node)
  { Type = (DamageType)Enum.Parse(typeof(DamageType), Xml.Attr(node, "type"));
    Amount = new Range(node.Attributes["amount"], 100);
  }

  public Range Amount; // 0-100%
  public DamageType Type;
}

#region AI
public abstract class AI : Entity
{ protected AI() { }

  public bool IsAlert { get { return state==AIState.Attacking || state==AIState.Following; } }
  public bool IsHostile
  { get { return alwaysHostile || (SocialGroup!=-1 && Global.GetSocialGroup(SocialGroup).Hostile); }
  }
  public AIState State { get { return state; } }

  public void Attack(Entity target) { this.target=target; GotoState(AIState.Attacking); }

  public override void Die(object killer, Death cause)
  { if(App.Player.CanSee(this))
    { if(cause==Death.Poison || cause==Death.Sickness || cause==Death.Starvation)
        App.IO.Print(TheName+" falls to the ground, dead.");
      if(killer!=App.Player) App.IO.Print("{0} is killed!", TheName);
    }
    Die();
  }

  public bool HostileTowards(Entity e)
  { return e==attacker || e==target || e.SocialGroup==App.Player.SocialGroup && IsHostile;
  }

  public override void OnDrink(Potion potion) { Does("drinks", potion); }
  public override bool OnDrop(Item item) { Does("drops", item); return true; }

  public override void OnEquip(Wieldable item)
  { if(App.Player.CanSee(this))
    { App.IO.Print("{0} equips {1}.", TheName, item.GetAName());
      if(item.Cursed)
      { App.IO.Print("The {0} welds itself to {1}'s {2}!", item.Name, TheName,
                     item.Class==ItemClass.Shield ? "arm" : "hand");
        item.Status |= ItemStatus.KnowCB;
      }
    }
  }

  public override void OnExpLevelChange(int diff)
  { if(App.Player.CanSee(this))
      App.IO.Print("{0} suddenly looks {1} experienced.", TheName, diff<0 ? "less" : "more");
  }

  public override void OnFlagsChanged(Chrono.Entity.Flag oldFlags, Chrono.Entity.Flag newFlags)
  { if(!App.Player.CanSee(Position)) return;

    Flag diff = oldFlags ^ newFlags;
    bool seeInvis = (App.Player.Flags&Flag.SeeInvisible)!=0;

    if((diff&Flag.Invisible)!=0)
    { if((newFlags&Flag.Invisible)!=0 && !seeInvis) App.IO.Print(TheName+" vanishes from sight.");
      else if((newFlags&Flag.Invisible)==0 && !seeInvis) App.IO.Print(TheName+" reappears!");
    }
    else if(!seeInvis && (newFlags&Flag.Invisible)!=0) return;

    if((diff&Flag.Sleep)!=0) App.IO.Print(TheName + ((newFlags&Flag.Sleep)==0 ? " wakes up." : " falls asleep."));
    if((diff&Flag.Levitate)!=0)
      App.IO.Print(TheName + ((newFlags&Flag.Levitate)==0 ? " floats back down to the floor."
                                                          : " floats up from the floor."));
  }

  public override void OnHitBy(Entity attacker, object item, Damage damage)
  { if(CanSee(attacker)) this.attacker=attacker;
    if(state!=AIState.Attacking && state!=AIState.Escaping)
    { GotoState(AIState.Attacking);
      hitDir = LookAt(attacker.Position);
    }
    if(SocialGroup!=-1 && attacker==App.Player) Global.UpdateSocialGroup(SocialGroup, true);
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

    if(!IsAlert)
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
        case AIState.Guarding: thresh = thresh*9/10; break;            // 11/10 as likely to notice if guarding
      }

      if(volume>Map.MaxSound*thresh/100)
      { if(state==AIState.Asleep && App.Player.CanSee(this)) App.IO.Print("{0} wakes up.", TheName);
        if(source.SocialGroup==App.Player.SocialGroup && IsHostile)
        { Attack(source);
          shout = false;
        }
        else if(state==AIState.Asleep) GotoState(defaultState);
      }
    }

    if(IsAlert)
      for(int i=0; i<8; i++)
      { Tile t = Map[Global.Move(Position, i)];
        if(t.Sound>maxNoise) { maxNoise=t.Sound; noiseDir=(Direction)i; }
      }
  }

  public override void OnPickup(Item item, IInventory inv)
  { item.Shop = null;
    Does("picks up", item);
  }

  public override void OnReadScroll(Scroll scroll) { Does("reads", scroll); }
  public override void OnRemove(Wearable item) { Does("removes", item); }

  public override void OnSick(string howSick)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} looks {1}.", TheName, howSick);
  }

  public override void OnSkillUp(Skill skill)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} looks more experienced.", TheName);
  }

  public override void OnUnequip(Wieldable item) { Does("unequips", item); }
  public override void OnUse(Item item) { Does("uses", item); }
  public override void OnWear(Wearable item) { Does("puts on", item); }

  public new void Pickup(IInventory inv, Item item)
  { if(items==null) items=new ArrayList();
    items.Add(base.Pickup(inv, item));
  }

  public override void TalkTo()
  { if(canSpeak)
    { if(script==null || !script.TryExecute("onSpeak", this))
        Say(GetQuip(HostileTowards(App.Player) ? Quips.Attacking : Quips.Other));
    }
    else App.IO.Print(TheName+" grunts (you're not sure if it can speak).");
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

  public int EntityIndex; // index into global entity type array
  public byte CorpseChance=30;   // chance of leaving a corpse, 0-100%

  public static AI Make(AI ent, int level, Race race, EntityClass entClass)
  { ent.Class = entClass;
    ent.Race  = race;
    ent.ExpLevel = level;

    int si = (int)race*3;
    ent.Eyesight=raceSenses[si]; ent.Hearing=raceSenses[si+1]; ent.Smelling=raceSenses[si+2];

    ent.OnMake();

    ent.HP = ent.MaxHP;
    ent.MP = ent.MaxMP;
    return ent;
  }

  public static AI Make(XmlNode node)
  { AI ent = null;
    int level = 1;
    Race race = Race.Random;
    EntityClass entClass = EntityClass.Random;

    for(XmlNode n=node; ; n=Global.GetEntityByID(Xml.Attr(n, "inherit")))
    { if(!Xml.IsEmpty(n, "type"))
      { ent = (AI)Type.GetType("Chrono."+Xml.Attr(n, "type")).GetConstructor(Type.EmptyTypes).Invoke(null);
        if(race==Race.Random) race = ent.Race;
        if(entClass==EntityClass.Random) entClass = ent.Class;
      }
      if(!Xml.IsEmpty(n, "class")) entClass = Xml.EntityClass(n, "class");
      if(!Xml.IsEmpty(n, "level")) level = Xml.Int(n, "level");
      if(!Xml.IsEmpty(n, "race")) race = Xml.Race(n, "race");

      if(Xml.IsEmpty(n, "inherit")) break;
    }
    if(ent==null) throw new ArgumentException("Entity does not contain a 'type' attribute.");

    if(entClass==EntityClass.Random) entClass = (EntityClass)Global.Rand((int)EntityClass.NumClasses);
    if(race==Race.Random) race = (Race)Global.Rand((int)Race.NumRaces);
    Make(ent, node);
    Make(ent, level, race, entClass);
    return ent;
  }

  public static AI MakeNpc(XmlNode npc)
  { AI e = Make(Global.GetEntity(Xml.Attr(npc, "entity")));
    e.EntityID = Xml.Attr(npc, "id");
    foreach(XmlNode item in npc.SelectNodes("give")) e.Inv.Add(Item.ItemDef(item));
    return e;
  }

  #region AIScript
  protected sealed class AIScript
  { public AIScript(XmlElement node)
    { ai=node;

      XmlNode n = node.SelectSingleNode("declare");
      if(n!=null)
      { n = n.FirstChild;
        while(n!=null)
        { if(n.LocalName=="quest") App.Player.DeclareQuest(n);
          else if(n.LocalName=="var")
          { string vname=n.Attributes["name"].Value, type, value=Xml.Attr(n, "value", "");
            ParseVarName(ref vname, out type);

            if(type=="global") Global.DeclareVar(vname, value);
            else if(type=="script")
            { XmlElement var = node.OwnerDocument.CreateElement("var");
              var.SetAttribute("name", vname);
              var.SetAttribute("value", value);
              ai.PrependChild(var);
            }
          }
          else throw new ArgumentException("Unknown declaration: "+n.LocalName);
          n = n.NextSibling;
        }
      }
    }

    public XmlNode XML { get { return ai; } }

    public void Initialize(AI me)
    { XmlNodeList vars = ai.SelectNodes("declare/var[starts-with(@name, 'local:')]");
      if(vars.Count>0)
      { me.vars = new System.Collections.Specialized.HybridDictionary(vars.Count);
        foreach(XmlNode var in vars)
        { string name = var.Attributes["name"].Value;
          name = name.Substring(name.IndexOf(':')+1);
          me.vars[name] = Xml.Attr(var, "value", "");
        }
      }
    }

    public bool TryExecute(string nodeName, AI me)
    { XmlNode node = ai.SelectSingleNode(nodeName);
      return node!=null && TryActionBlock(node, me);
    }

    public static AIScript Load(string name)
    { if(name.IndexOf('/')==-1) name = "ai/"+name;
      if(name.IndexOf('.')==-1) name += ".xml";

      AIScript script = (AIScript)scripts[name];
      if(script==null)
      { XmlDocument doc = Global.LoadXml(name);
        script = new AIScript(doc.DocumentElement);
        scripts[name] = script;
      }
      return script;
    }

    string Evaluate(XmlNode expr, AI me)
    { throw new NotImplementedException("expression evaluation");
    }

    string GetText(string name, XmlNode local, Entity me)
    { string selector = "text[@id='"+name+"']";
      XmlNode node = local==null ? null : local.SelectSingleNode(selector);
      if(node==null) node = ai.SelectSingleNode(selector);
      if(node==null) throw new ArgumentException("No such text node: "+name);
      return repRe.Replace(Xml.BlockToString(node.InnerText), new MatchEvaluator(TextReplacer));
    }

    string GetVar(string name, AI me)
    { string type;
      ParseVarName(ref name, out type);

      if(type=="script") return ai.SelectSingleNode("var[@name='"+name+"']/@value").Value;
      if(type=="global") return Global.GetVar(name);

      // assuming "local" here
      if(me==null) throw new InvalidOperationException("Can't use a local variable outside an AI context");
      if(me.vars.Contains(name)) return (string)me.vars[name];
      throw new ArgumentException("variable "+name+" not declared");
    }

    static void ParseVarName(ref string name, out string type)
    { int pos = name.IndexOf(':');
      if(pos==-1) type="script";
      else { type=name.Substring(0, pos); name=name.Substring(pos+1); }
    }

    void SetVar(string name, string value, AI me)
    { string type;
      ParseVarName(ref name, out type);
      
      if(type=="script") ai.SelectSingleNode("var[@name='"+name+"']/@value").Value = value;
      else if(type=="global") Global.SetVar(name, value);
      else // assuming "local" here
      { if(me==null) throw new InvalidOperationException("Can't use a local variable outside an AI context");
        if(me.vars.Contains(name)) me.vars[name]=value;
        else throw new ArgumentException("variable "+name+" not declared");
      }
    }

    string TextReplacer(Match m)
    { switch(m.Groups[1].Value)
      { case "name": return App.Player.Name;
        case "man":
          switch(App.Player.Gender)
          { case Gender.Male: return "man";
            case Gender.Female: return "woman";
            case Gender.Neither: return "person";
            default: throw new NotImplementedException("unhandled gender");
          }
        case "boy":
          switch(App.Player.Gender)
          { case Gender.Male: return "boy";
            case Gender.Female: return "girl";
            case Gender.Neither: return "child";
            default: throw new NotImplementedException("unhandled gender");
          }
        default: return m.Value;
      }
    }
    
    bool TryAction(XmlNode node, AI me)
    { switch(node.LocalName)
      { case "else": return false;
        case "end": dialogOver=true; return false;

        case "give":
        { string spawn = Xml.Attr(node, "spawn", "maybe");
          Item item=null;
          if(me!=null && spawn!="no") item = me.FindItem(node);
          if(item==null) item = Item.ItemDef(node);
          bool failed = App.Player.Inv.Add(item)==null;
          if(failed) App.Player.Map.AddItem(App.Player.Position, item);
          App.IO.Print("{0} gives you {1}{2}.", me==null ? "A magic fairy" : me.TheName, item.GetAName(),
                        failed ? ", but you can't carry "+item.ItThem+", so "+item.ItThey+
                                " fall"+item.VerbS+" to the ground" : "");
          return true;
        }
        
        case "giveQuest":
        { Player.Quest quest = App.Player.GetQuest(Xml.Attr(node, "name"));
          bool has = quest.Received;
          quest.StateName = Xml.Attr(node, "state", "during");
          quest.Received  = true;
          if(!has) App.IO.Print("You have been given a new quest! ({0})", quest.Title);
          return true;
        }

        case "goto": nextDialogNode = node.Attributes["name"].Value; return false;

        case "if": return TryIf(node, me);

        case "joinPlayer":
          me.SocialGroup  = App.Player.SocialGroup;
          me.defaultState = AIState.Following;
          me.GotoState(AIState.Following);
          return false;

        case "quipGroup":
        { if(me==null) throw new InvalidOperationException("'quipGroup' not valid outside of an AI context");
          if(node.ChildNodes.Count>0)
          { string text = Xml.BlockToString(node.ChildNodes[Global.Rand(node.ChildNodes.Count)].InnerText);
            me.Say(text);
            return true;
          }
          return false;
        }

        case "say":
        { if(me==null) throw new InvalidOperationException("'say' not valid outside of an AI context");
          string dialog = Xml.Attr(node, "dialog");
          if(dialog!=null) return TryDialog(ai.SelectSingleNode("dialog[@id='"+dialog+"']"), me);
          else me.Say(Xml.BlockToString(node.InnerText));
          return true;
        }

        case "set":
        { string name=Xml.Attr(node, "var"), value=Xml.Attr(node, "value");
          if(value==null) value = Evaluate(node.SelectSingleNode("value"), me);
          SetVar(name, value, me);
          return false;
        }

        default: throw new ArgumentException("unknown action: "+node.LocalName);
      }
    }

    bool TryActionBlock(XmlNode node, AI me)
    { node = node.FirstChild;
      bool didSomething=false;
      while(node!=null)
      { if(TryAction(node, me)) didSomething=true;
        node = node.NextSibling;
      }
      return didSomething;
    }

    bool TryDialog(XmlNode dialog, AI me)
    { dialogOver = false;
      nextDialogNode = "Start";
      
      XmlNode node = dialog.SelectSingleNode("onDialog");
      if(node!=null)
      { TryActionBlock(node, me);
        if(dialogOver) return false;
      }

      do
      { node = dialog.SelectSingleNode("simpleNode[@name='"+nextDialogNode+"']");
        if(node!=null)
        { string[] options = node.Attributes["options"].Value.Split(',');
          string[] choices = new string[options.Length];
          for(int i=0; i<options.Length; i++)
            choices[i] = GetText(options[i].Substring(0, options[i].IndexOf(':')), dialog, me);
          int choice = App.IO.ConversationChoice(me, GetText(node.Attributes["text"].Value, dialog, me), choices);
          nextDialogNode = options[choice].Substring(options[choice].IndexOf(':')+1);
          if(nextDialogNode=="*END*") dialogOver=true;
        }
        else
        { node = dialog.SelectSingleNode("node[@name='"+nextDialogNode+"']");
          XmlNodeList options = node.SelectNodes("option");
          string[] choices = new string[options.Count];
          for(int i=0; i<choices.Length; i++) choices[i] = GetText(options[i].Attributes["text"].Value, dialog, me);
          int choice = App.IO.ConversationChoice(me, GetText(node.Attributes["text"].Value, dialog, me), choices);
          TryActionBlock(options[choice], me);
        }        
      } while(!dialogOver);
      App.IO.EndConversation();

      return true;
    }

    bool TryIf(XmlNode node, AI me)
    { string av, lhs, rhs;
      XmlNode child;
      bool isTrue;
      
      if((av=Xml.Attr(node, "var"))            != null)  lhs=GetVar(av, me);
      else if((av=Xml.Attr(node, "haveQuest")) != null)  lhs=App.Player.GetQuest(av).Received ? "1" : "";
      else if((av=Xml.Attr(node, "quest"))     != null)  lhs=App.Player.GetQuest(av).StateName;
      else if((av=Xml.Attr(node, "questDone")) != null)  lhs=App.Player.GetQuest(av).Done ? "1" : "";
      else if((av=Xml.Attr(node, "questNotDone"))!=null) lhs=App.Player.GetQuest(av).Done ? "" : "1";
      else if((av=Xml.Attr(node, "questSuccess")) !=null) lhs=App.Player.GetQuest(av).Succeeded ? "1" : "";
      else if((av=Xml.Attr(node, "questFailed")) !=null) lhs=App.Player.GetQuest(av).Failed ? "1" : "";
      else if((av=Xml.Attr(node, "lhs")) != null) lhs=av;
      else if((child=node.SelectSingleNode("lhs")) != null) lhs=Evaluate(child, me);
      else throw new ArgumentException("conditional: left hand side is not defined");

      if((av=Xml.Attr(node, "rhs")) != null) rhs=av;
      else if((av=Xml.Attr(node, "var2")) != null) rhs=GetVar(av, me);
      else if((child=node.SelectSingleNode("rhs")) != null) rhs=Evaluate(child, me);
      else
      { isTrue = Xml.Attr(node, "op")=="!" ? !Xml.IsTrue(lhs) : Xml.IsTrue(lhs);
        goto execute;
      }

      switch(Xml.Attr(node, "op"))
      { case "==": isTrue = int.Parse(lhs)==int.Parse(rhs); break;
        case "!=": isTrue = int.Parse(lhs)!=int.Parse(rhs); break;
        case "eq": isTrue = lhs==rhs; break;
        case "ne": isTrue = lhs!=rhs; break;
        case "<":  isTrue = int.Parse(lhs)< int.Parse(rhs); break;
        case "<=": isTrue = int.Parse(lhs)<=int.Parse(rhs); break;
        case ">":  isTrue = int.Parse(lhs)> int.Parse(rhs); break;
        case ">=": isTrue = int.Parse(lhs)>=int.Parse(rhs); break;
        default: throw new ArgumentException("invalid operator: "+Xml.Attr(node, "op"));
      }
      execute:
      if(isTrue) return TryActionBlock(node, me);
      else
      { node = node.SelectSingleNode("else");
        return node==null ? false : TryActionBlock(node, me);
      }
    }
    
    Regex repRe = new Regex(@"{([^}]+)}", RegexOptions.Singleline);
    XmlElement ai;
    string nextDialogNode;
    bool   dialogOver;
    
    static readonly Hashtable scripts = new Hashtable();
  }
  #endregion

  protected enum Combat { None, Melee, Ranged };

  protected AIScript Script
  { get { return script; }
    set
    { if(vars!=null) vars.Clear();
      script = value;
      if(script!=null) script.Initialize(this);
    }
  }

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
    { timeout=attackTimeout; lastDir=dir; // we know where the enemy is! our vigor is renewed!
      // if we know exactly where the target is and we want to use a ranged attack
      if(dist>=rangedThresh || bestWand!=null || Spells!=null)
      { if(bestWand!=null && bestWand.Spell.Range>=dist) // use a wand if possible
        { bool discard = bestWand.Charges==0;
          if(print) App.IO.Print("{0} zaps {1}!", App.Player.CanSee(this) ? TheName : "Something",
                                 bestWand.GetAName(true));
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
        { if(print) App.IO.Print("{0} attacks with {1}.", TheName, w.GetAName(true));
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
      if(IsHostile)
        foreach(Entity e in Global.GetSocialGroup(App.Player.SocialGroup).Entities)
          if(e!=target && TryAttack(e, dir, pdir)) return true;

      hitDir=Direction.Invalid;
      if(timeout==0) { lastDir=Direction.Invalid; GotoState(defaultState); return false; } // we give up

      if(TrySmartMove(dir)) return true; // we couldn't find anything to attack, so we try to move towards target
      else if(TrySmartMove(dir-1)) { lastDir--; return true; }
      else if(TrySmartMove(dir+1)) { lastDir++; return true; }
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
  { if(App.Player.CanSee(this)) App.IO.Print("{0} {1} {2}.", TheName, verb, item.GetAName(true));
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
    { Spell spell = GetSpell(item);
      if(spell==null) continue;

      int iq;

      if(spell is TeleportSpell) iq = 5;
      else continue;

      if(i.KnownCursed) iq -= 4;
      else if(i.KnownBlessed) iq++;

      if(quality<iq) { item=i; quality=iq; found=true; }
    }
    return found;
  }

  protected virtual bool FindHealItem(IInventory inv, ref Item item, ref int quality)
  { bool found=false;
    foreach(Item i in inv)
    { Spell spell = GetSpell(item);
      if(spell==null) continue;

      int iq;
      if(spell is HealSpell) iq = 3;
      else continue;

      if(i.KnownCursed) iq -= 4;
      else if(i.KnownBlessed) iq += 2;
      else if(i.KnownUncursed) iq++;

      if(quality<iq) { item=i; quality=iq; found=true; }
    }
    return found;
  }

  protected virtual bool Follow(Entity entity)
  { Direction dir = LookAt(entity);
    if(dir!=Direction.Invalid)
    { lastDir = dir;
      timeout = followTimeout;
      int dist = DistanceTo(entity);
      if(dist>2 || dist==2 && Global.Coinflip()) return TrySmartMove(dir);
    }
    else
    { if(timeout>0)
      { SenseEntity(entity, false);
        dir = sightDir!=Direction.Invalid ? sightDir : hitDir!=Direction.Invalid ? hitDir :
              noiseDir!=Direction.Invalid ? noiseDir : scentDir!=Direction.Invalid ? scentDir : lastDir;
        if(sightDir==Direction.Invalid && noiseDir==Direction.Invalid) timeout--;
        if(dir!=Direction.Invalid)
        { if(TrySmartMove(dir)) return true;
          else if(TrySmartMove(dir-1)) { lastDir--; return true; }
          else if(TrySmartMove(dir+1)) { lastDir++; return true; }
        }
      }
      else if(timeout>-10)
      { if(timeout--==0)
        { Say("Hey, wait for me!");
          lastDir = Direction.Invalid;
        }
      }
    }
    return false;
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
      timeout = attackTimeout; // we'll be in the attack state for at least N turns
      lastDir = Direction.Invalid; // force us to calculate a new direction
    }
    else if(newstate==AIState.Following) timeout = followTimeout;
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
      case AIState.Idle: case AIState.Guarding: return DoIdleStuff();
      case AIState.Following: return Follow(App.Player) || DoIdleStuff() || (timeout<-followTimeout ? Wander() : false);
      case AIState.Patrolling: case AIState.Wandering: return DoIdleStuff() || Wander(); // TODO: implement a better patrol
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

  protected virtual void OnMake()
  { Skill[] skills = classSkills[(int)Class];
    AddSkills(100*ExpLevel, skills);

    if(Class==EntityClass.Fighter) // then do more general skills
    { skills = new Skill[(int)WeaponClass.NumClasses];
      for(int i=0; i<skills.Length; i++) skills[i] = (Skill)i; // first N weapon classes are mapped directly to skills
    }
    else if(Class==EntityClass.Wizard)
    { skills = new Skill[(int)SpellClass.NumClasses];
      for(int i=0; i<skills.Length; i++) skills[i] = (Skill)i + (int)Skill.MagicSkills;
    }
    else skills=null;
    AddSkills(300*ExpLevel, skills);
  }

  protected virtual bool PickupItems()
  { if(!hasInventory || Inv.IsFull || !Map.HasItems(Position)) return false;
    if(state!=AIState.Attacking) // don't pick up items in shops, unless we're fighting or the shop is abandoned
    { Shop shop = Map.GetShop(Position);
      if(shop!=null && shop.Shopkeeper!=null) return false;
    }

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
  { if(!Global.Entities[EntityIndex].HasWeaponAttack) return false;
    Item item=null;
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
  { if(!Global.Entities[EntityIndex].HasWeaponAttack) return false;
    Item item=null;
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

    if(ring.KnownCursed) score -= 6; // blessed/cursed status (uses player's knowledge)
    else if(ring.KnownBlessed) score += 2;
    else if(ring.KnownUncursed) score++;
    
    // TODO: make this more comprehensive, taking into account attribute mods as well
    if(ring.GetFlag(Entity.Flag.Invisible))
    { if(Is(Flag.Invisible)) return -10;
      score += 8;
    }
    else if(ring.GetFlag(Entity.Flag.SeeInvisible))
    { if(Is(Flag.SeeInvisible)) return -10;
      score += 3;
    }

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
  { if(target!=null && SenseEntity(target, true)) return true;     // first try last target
    if(attacker!=null && SenseEntity(attacker, true)) return true; // then attacker
    if(SocialGroup==App.Player.SocialGroup)
    { foreach(Entity e in VisibleCreatures())
        if(e is AI && ((AI)e).IsHostile && SenseEntity(e, true)) return true;
    }
    else if(IsHostile) // then any party member
      foreach(Entity e in Global.GetSocialGroup(App.Player.SocialGroup).Entities)
        if(SenseEntity(e, true)) return true;
    return false;
  }

  protected bool SenseEntity(Entity e, bool attack)
  { bool dontignore = IsAlert || Global.Rand(100)>=e.Stealth*10-3; // ignore stealthy entities
    sightDir = dontignore && Global.Rand(100)<Eyesight ? LookAt(e) : Direction.Invalid; // eyesight
    if(attack) target = sightDir!=Direction.Invalid ? e : null;
    if(e==App.Player && Smelling>0) // try smell (not dampened by stealth, sound is handled elsewhere)
    { int maxScent=0, scent;
      for(int i=0; i<8; i++)
      { Point np = Global.Move(Position, i);
        Tile t = Map[np];
        if(Map.IsPassable(t.Type) && (scent=Map.GetScent(np))>maxScent) { maxScent=scent; scentDir=(Direction)i; }
      }
      if(maxScent!=100) maxScent = maxScent*Smelling/100; // scale by our smelling ability
      if(!IsAlert && maxScent<(Map.MaxScent/10)) scentDir=Direction.Invalid; // ignore light scents if not alerted
    }
    else scentDir=Direction.Invalid;
    return sightDir!=Direction.Invalid || noiseDir!=Direction.Invalid || scentDir!=Direction.Invalid;
  }

  protected virtual int SpellScore(Spell spell)
  { if(spell is FireSpell) return 10;
    if(spell is ForceBolt) return 2;
    return 0;
  }

  protected bool TryOnSpeak() { return script!=null && script.TryExecute("onSpeak", this); }

  protected bool TrySmartMove(Direction d) { return TrySmartMove(Global.Move(Position, d)); }
  protected bool TrySmartMove(Point pt)
  { if(TryMove(pt)) return true;

    Tile t = Map[pt];
    if(canOpenDoors && t.Type==TileType.ClosedDoor && !t.GetFlag(Tile.Flag.Locked))
    { Map.SetType(pt, TileType.OpenDoor);
      return true;
    }
    return false;
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
    return SpellScore(wand.Spell);
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

  protected const int attackTimeout=5, followTimeout=10;
  protected Entity attacker;      // the entity that last attacked us
  protected Entity target;        // the entity we're attacking
  protected Wand bestWand;        // the best wand for attacking
  protected ArrayList items;      // items we've pickup up but haven't considered yet
  protected AIScript script;      // our AI script
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
  protected byte Eyesight, Hearing, Smelling; // effectiveness of these senses, 0-100%
  protected bool alwaysHostile=true;  // true if we should attack the player's party regardless of social group considerations
  protected bool canOpenDoors =false; // most creatures can't open doors (by default)
  protected bool canSpeak     =false; // most creatures can't speak
  protected bool hasInventory =true;  // true if the creature can pick up and use items
  protected bool shout;               // true if we will shout on our next turn to alert others

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

  Item FindItem(XmlNode inode)
  { Type type = Type.GetType("Chrono."+inode.Attributes["class"].Value);
    int count = Xml.Int(inode.Attributes["count"], 0);

    foreach(Item i in Inv)
      if(i.GetType()==type && i.Count>=count)
      { if(count==0 || i.Count==count) { Inv.Remove(i); return i; }
        else return i.Split(count);
      }
    return null;
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
        timeout = attackTimeout; // we attacked something, so our vigor is renewed!
        return true; // but our turn is up
      }
    }
    return false;
  }

  System.Collections.Specialized.HybridDictionary vars;

  static Spell GetSpell(Item item)
  { if(item is Scroll) return ((Scroll)item).Spell;
    else if(item is Wand)
    { Wand w = (Wand)item;
      if(w.Charges!=0) return w.Spell;
    }
    else if(item is XmlTool) return ((XmlTool)item).Spell;
    else if(item is XmlChargedTool)
    { XmlChargedTool xc = (XmlChargedTool)item;
      if(xc.Charges!=0) return xc.Spell;
    }
    return null;
  }

  static void Make(AI ent, XmlNode node)
  { if(!Xml.IsEmpty(node, "inherit")) Make(ent, Global.GetEntityByID(Xml.Attr(node, "inherit")));

    foreach(XmlAttribute attr in node.Attributes)
      switch(attr.Name)
      { case "ai": ent.Script = AIScript.Load(attr.Value); break;
        case "color": ent.Color = Xml.Color(attr); break;
        case "corpseChance": ent.CorpseChance = byte.Parse(attr.Value); break;
        case "name": ent.Name = attr.Value; break;
        case "socialGroup": ent.SocialGroup = Global.GetSocialGroup(attr.Value); break;
        case "flies": ent.SetRawFlag(Entity.Flag.Levitate, Xml.IsTrue(attr.Value)); break;
        case "gender": ent.Gender = Xml.Gender(attr); break;

        case "ac": case "dex": case "ev": case "int": case "light": case "maxHP": case "maxMP":
        case "speed": case "stealth": case "str": 
          ent.SetBaseAttr((Attr)Enum.Parse(typeof(Attr), attr.Name, true), Xml.RangeInt(attr.Value));
          break;
        
        case "inherit": case "type": case "race": case "class": case "level": break;

        default: Global.SetObjectValue(ent, attr.Name, attr.Value); break;
      }

    foreach(XmlNode item in node.SelectNodes("give")) ent.Inv.Add(Item.ItemDef(item));
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
#endregion

} // namespace Chrono
