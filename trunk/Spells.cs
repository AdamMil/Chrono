using System;
using System.Collections;
using System.Drawing;
using System.Reflection;

namespace Chrono
{

public enum SpellClass // remember to add these to the Skill enum as well
{ Summoning, Enchantment, Telekinesis, Translocation, Transformation, Divination, Channeling, Necromancy, Elemental,
  Poison, Restorative,

  NumClasses
}
public enum SpellTarget { Self, Item, Tile };

#region Spell
public abstract class Spell : UniqueObject
{ public void Cast(Entity user) { Cast(user, ItemStatus.None, user.Position, Direction.Self); }
  public void Cast(Entity user, ItemStatus buc) { Cast(user, buc, user.Position, Direction.Self); }
  public void Cast(Entity user, Direction dir) { Cast(user, ItemStatus.None, new RangeTarget(dir)); }
  public void Cast(Entity user, ItemStatus buc, Direction dir) { Cast(user, buc, new RangeTarget(dir)); }
  public void Cast(Entity user, RangeTarget rt) { Cast(user, ItemStatus.None, rt); }
  public void Cast(Entity user, ItemStatus buc, RangeTarget rt)
  { if(rt.Dir!=Direction.Invalid)
    { Point np = rt.Dir>=Direction.Above ? user.Position : Global.Move(user.Position, rt.Dir);
      Cast(user, buc, np, rt.Dir);
    }
    else if(rt.Point.X!=-1) Cast(user, buc, rt.Point, rt.Dir);
  }
  public void Cast(Entity user, Item target) { Cast(user, ItemStatus.None, target); }
  public virtual void Cast(Entity user, ItemStatus buc, Item target) { }
  public void Cast(Entity user, Point tile) { Cast(user, ItemStatus.None, tile, Direction.Invalid); }
  public void Cast(Entity user, ItemStatus buc, Point tile) { Cast(user, buc, tile, Direction.Invalid); }
  public void Cast(Entity user, Point tile, Direction dir) { Cast(user, ItemStatus.None, tile, dir); }
  public virtual void Cast(Entity user, ItemStatus buc, Point tile, Direction dir) { }

  public int Level { get { return (Difficulty+1)/2; } } // returns skill level (1-9)
  public Skill Skill { get { return (Skill)((int)Class+(int)Skill.MagicSkills); } }

  // (Int-7) * 100 * (1.2 - 1/(skill^1.175)) / Difficulty - 10
  public int CastChance(Entity user) // assuming the user knows it
  { int skill = (user.GetSkill(Skill.Casting)+user.GetSkill(Skill)+1)/2;
    double smul = 1.2-1/Math.Pow(1.1746189430880190059144636656919, skill);
    int chance = (int)Math.Round((user.Int-7)*100*smul/Difficulty) - 10;
    int penalty = CastPenalty(user);
    if(penalty>0) chance -= chance*penalty/100;
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }

  public int CastPenalty(Entity user)
  { int penalty = user.Shield==null ? 0 : 15; // -15% for having a shield

    bool full=true;
    foreach(Wieldable w in user.Hands)
      if(w==null) full=false;
      else if(w.AllHandWield && (w.Class!=ItemClass.Weapon || ((Weapon)w).wClass!=WeaponClass.Staff))
      { full=true;
        break;
      }
    if(full) penalty += 20; // -20% for having your hands full (except from staves)

    for(int i=0; i<armorPenalty.Length; i+=2)
      if(user.Slots[armorPenalty[i]]!=null && user.Slots[armorPenalty[i]].Material>=Material.HardMaterials)
        penalty += armorPenalty[i+1];
    penalty += (int)user.HungerLevel*10; // -10% per hunger level
    return penalty;
  }

  public bool CastTest(Entity user) { return Global.Rand(100)<CastChance(user); }

  // (Int-8) * 100 * (1.1 - 1/(skill^1.126)) / Difficulty - 20
  public int LearnChance(Entity user)
  { double smul = 1.1-1/Math.Pow(1.2589254117941672104239541063958, user.GetSkill(Skill));
    int chance = (int)Math.Round((user.Int-8)*100*smul/Difficulty) - 20;
    int penalty = (int)user.HungerLevel*10; // -10% per hunger level
    if(penalty>0) chance -= chance*penalty/100;
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }

  public virtual SpellTarget GetSpellTarget(Entity user) { return target; }

  public virtual ICollection TracePath(Entity user, Point pt) { return null; }

  public string Name, Description;
  public SpellClass Class;
  public int Difficulty; // 1-18, 1,2=level 1, 3,4=level 2, etc
  public int Memory; // decreased every turn that the spell isn't cast, forgotten at zero. -1 means never forget
  public int Power;  // MP usage
  public int Range;  // the approximate range of the wand, in tiles, if applicable
  public bool AutoIdentify;

  public static Spell Get(string name)
  { if(spells==null)
    { spells = new Hashtable();

      Type[] types = Assembly.GetExecutingAssembly().GetTypes();
      foreach(Type t in types)
        if(!t.IsAbstract && t.IsSubclassOf(typeof(Spell)))
        { string sname = t.Name;
          if(sname.EndsWith("Spell")) sname = sname.Substring(0, sname.Length-5);
          FieldInfo fi = t.GetField("Default", BindingFlags.Public|BindingFlags.Static);
          spells[sname] = (Spell)(fi==null ? t.GetConstructor(Type.EmptyTypes).Invoke(null) : fi.GetValue(null));
        }
    }

    Spell spell = (Spell)spells[name];
    if(spell==null) throw new ArgumentException("No such spell: "+name);
    return spell;
  }

  protected bool TryHit(Entity user, Entity target)
  { int skill = user.GetSkill(Skill);
    int toHit = (user.Int+skill+1)/2;
    int toEvade = target.EV;
    
    if(skill==0) toHit = (toHit+1)/2;
    else if(skill==1) toHit -= (toHit+2)/4;
    else toHit = (int)Math.Round(toHit*Entity.SkillModifier(skill));
    
    toEvade += (toEvade*target.GetSkill(Skill.Dodge)*10+50)/100;

    toHit   -= (toHit  *((int)user.HungerLevel*10)+99)/100; // effects of hunger -10% per hunger level (rounded up)
    toEvade -= (toEvade*((int)target.HungerLevel*10)+99)/100;

    if(toHit<0) toHit=0;
    if(toEvade<0) toEvade=0;

    int n = Global.Rand(toHit+toEvade);
    App.IO.Print(Color.DarkGrey, "SpHIT: (toHit: {0}, EV: {1}, roll: {2} = {3})", toHit, toEvade, n, n>=toEvade ? "hit" : "miss");
    if(n<toEvade)
    { target.Exercise(Skill.Dodge);
      target.Exercise(Attr.EV);
      return false;
    }
    return true;
  }

  protected SpellTarget target;

  protected static ArrayList path = new ArrayList();

  // TODO: move all these and others into XML too!
  static readonly int[] armorPenalty = new int[]
  { (int)Slot.Feet, 5, (int)Slot.Hands, 10, (int)Slot.Head, 5, (int)Slot.Legs, 10, (int)Slot.Torso, 25
  };
  
  static Hashtable spells;
}
#endregion

#region BeamSpell
public abstract class BeamSpell : Spell
{ protected BeamSpell() { target=SpellTarget.Tile; Range=10; }

  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { itemStatus = buc;
    if((dir==Direction.Above || dir==Direction.Below) && !Affect(user, buc, dir)) return;
    else if(user.Position==tile) Affect(user, buc, user);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, Range, false, new LinePoint(ZapPoint), user);
    }
  }

  public override ICollection TracePath(Entity user, Point tile)
  { path.Clear();
    if(user.Position==tile) path.Add(tile);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, Range, false, new LinePoint(TracePoint), user);
    }
    return path;
  }

  protected virtual object Hit(Entity user, Point pt)
  { Entity e = user.Map.GetEntity(pt);
    if(e!=null)
    { if(TryHit(user, e)) return e;
      else
      { e.OnMissBy(user, this);
        user.OnMiss(e, this);
        return null;
      }
    }
    else return Map.IsPassable(user.Map[pt].Type) ? null : (object)pt;
  }

  protected abstract void Affect(Entity user, ItemStatus buc, object obj);
  protected abstract bool Affect(Entity user, ItemStatus buc, Direction dir); // returns if execution should continue (in Cast)

  TraceAction TracePoint(Point pt, object context)
  { Entity user = (Entity)context;
    path.Add(pt);
    TraceAction ret;
    if(!Map.IsPassable(user.Map[pt].Type))
    { if(++bounces==3) return TraceAction.Stop;
      ret = TraceAction.Bounce;
      if(!Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret &= ~TraceAction.HBounce;
      if(!Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret &= ~TraceAction.VBounce;
      if(ret==0) ret=TraceAction.Bounce;
    }
    else ret = TraceAction.Go;
    oldPt=pt;
    return ret;
  }

  TraceAction ZapPoint(Point pt, object context)
  { Entity user = (Entity)context;
    TraceAction ret;
    if(!Map.IsPassable(user.Map[pt].Type))
    { if(++bounces==3) return TraceAction.Stop;
      ret = TraceAction.Bounce;
      if(!Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret &= ~TraceAction.HBounce;
      if(!Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret &= ~TraceAction.VBounce;
      if(ret==0) ret=TraceAction.Bounce;
      if(user==App.Player) App.IO.Print("The spell bounces!");
    }
    else ret=TraceAction.Go;
    oldPt=pt;
    object affected = Hit(user, pt);
    if(affected!=null) Affect(user, itemStatus, affected);
    return ret;
  }
  
  ItemStatus itemStatus;
  Point oldPt;
  int bounces;
}
#endregion

#region ForceBolt
public class ForceBolt : BeamSpell
{ public ForceBolt()
  { Name="force bolt"; Class=SpellClass.Telekinesis; Difficulty=1; Power=2;
    Description="The spell forces stuff. Yeah.";
  }

  protected override bool Affect(Entity user, ItemStatus buc, Direction dir)
  { if(dir==Direction.Above)
    { if(user==App.Player) App.IO.Print("Bits of stone rain down on you as the spell slams into the ceiling.");
      return false;
    }
    else if(dir==Direction.Below && user==App.Player) App.IO.Print("The bugs on the ground are crushed!");
    return false;
  }

  protected override void Affect(Entity user, ItemStatus buc, object obj)
  { Entity e = obj as Entity;
    Damage damage = new Damage(Global.NdN(1, 6));
    damage.Direct = 2;
    user.TrySpellDamage(this, e!=null ? e.Position : (Point)obj, damage);
  }

  public static readonly ForceBolt Default = new ForceBolt();
}
#endregion

#region Fire
public class FireSpell : BeamSpell
{ public FireSpell()
  { Name="fire"; Class=SpellClass.Elemental; Difficulty=10; Power=12; AutoIdentify=true;
    Description = "The fire spell hurls a great bolt of flames.";
  }

  protected override object Hit(Entity user, Point pt)
  { Entity e = user.Map.GetEntity(pt);
    if(e==null) return pt;
    else if(TryHit(user, e)) return e;
    else
    { e.OnMissBy(user, this);
      user.OnMiss(e, this);
      return null;
    }
  }

  protected override bool Affect(Entity user, ItemStatus buc, Direction dir)
  { if(dir==Direction.Above)
    { if(user==App.Player) App.IO.Print("The spell bounces back down onto your head!");
      return true;
    }
    else if(dir==Direction.Below)
    { if(user==App.Player) App.IO.Print("The bugs on the ground are incinerated!");
      IInventory inv = user.Map[user.Position].Items;
      if(inv!=null)
      { bool print = App.Player.CanSee(user.Position);
        for(int i=0; i<inv.Count; i++) if(AffectItem(inv, inv[i], user==App.Player)) i--;
      }
    }
    return false;
  }

  protected override void Affect(Entity user, ItemStatus buc, object obj)
  { Damage damage = new Damage();
    damage.Heat = (ushort)Global.NdN(4, 10);
    Entity e = obj as Entity;
    bool print;
    if(e!=null)
    { user.TrySpellDamage(this, e.Position, damage);
      print = App.Player.CanSee(e);
      for(int i=0; i<e.Inv.Count; i++) if(Global.Rand(100)<30 && AffectItem(e.Inv, e.Inv[i], print)) i--;
    }
    else
    { Point pt = (Point)obj;
      user.TrySpellDamage(this, pt, damage);
      IInventory inv = user.Map[pt].Items;
      if(inv!=null)
      { print = App.Player.CanSee(pt);
        for(int i=0; i<inv.Count; i++)
        { Item item = inv[i];
          if(AffectItem(inv, item, print))
          { i--;
            if(user==App.Player && item.Shop!=null && item.Shop.Shopkeeper!=null) user.Use(item, true);
          }
        }
      }
    }
  }

  bool AffectItem(IInventory inv, Item i, bool print)
  { if(i.Class==ItemClass.Scroll || i.Class==ItemClass.Potion || i.Class==ItemClass.Spellbook)
    { if(print)
        App.IO.Print(i.Class==ItemClass.Potion ? "{0} heat{1} up and burst{1}!" : "{0} burn{1} up!",
                     (inv==App.Player.Inv ? "Your "+i.GetFullName() : Global.Cap1(i.GetAName())),
                     i.VerbS);
      inv.Remove(i);
      return true;
    }
    return false;
  }

  public readonly static FireSpell Default = new FireSpell();
}
#endregion

#region Heal
public sealed class HealSpell : Spell
{ public HealSpell()
  { Name="heal"; Class=SpellClass.Restorative; Difficulty=3; Power=5;
    Description = "This spell will cure the caster of poisons, and rejuvenate him as well.";
  }
  
  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { Entity e = dir==Direction.Self ? user : user.Map.GetEntity(tile);
    if(e==null) return;

    if((buc&ItemStatus.Cursed)!=0)
    { e.DoDamage(this, Death.Sickness, Damage.FromPoison(1));
      if(user==App.Player) App.IO.Print("Eww, this tastes putrid!"); // FIXME: we don't know it's a potion...
    }
    else if(user.MaxHP-user.HP>0)
    { e.HP += Global.NdN(4, 6) * ((buc&ItemStatus.Blessed)!=0 ? 2 : 1);
      if(e==App.Player) App.IO.Print("You feel better.");
      else if(App.Player.CanSee(e)) App.IO.Print("{0} looks better.", e.TheName);
    }
    else if(e==App.Player) App.IO.Print("Nothing seems to happen.");
  }
}
#endregion

#region RemoveScent
public sealed class RemoveScentSpell : Spell
{ public RemoveScentSpell()
  { Name="remove scent"; Class=SpellClass.Restorative; Difficulty=2; Power=8;
    Description = "This spell will make the caster smell as fresh as a rose.";
  }
  
  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { Entity e = dir==Direction.Self ? user : user.Map.GetEntity(tile);
    if(e==null) return;
    e.Smell = (buc&ItemStatus.Cursed)==0 ? 0 : Map.MaxScent;
    if(e==App.Player) App.IO.Print("You smell much better.");
  }

}
#endregion

#region Teleport
public class TeleportSpell : Spell
{ public TeleportSpell()
  { Name="teleport"; Class=SpellClass.Translocation; Difficulty=6; Power=9; AutoIdentify=true;
    Description = "This spell will teleport the caster to a random location.";
  }

  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { Point telTo = user.Is(Entity.Flag.TeleportControl) ? tile : user.Map.FreeSpace();
    if(user!=App.Player)
    { if(App.Player.CanSee(user))
        App.IO.Print("{0} {1}.", user.TheName, App.Player.CanSee(telTo) ? "teleports" : "disappears");
      else
      { user.OnMove(telTo);
        if(App.Player.CanSee(user)) App.IO.Print("{0} appears out of nowhere!", user.AName);
        return;
      }
    }
    user.OnMove(telTo);
  }

  public override SpellTarget GetSpellTarget(Entity user)
  { return user.Is(Entity.Flag.TeleportControl) ? SpellTarget.Tile : SpellTarget.Self;
  }

  public readonly static TeleportSpell Default = new TeleportSpell();
}
#endregion

#region Amnesia
public class AmnesiaSpell : Spell
{ public AmnesiaSpell()
  { Name="amnesia"; Class=SpellClass.Divination; Difficulty=2; Power=2; target=SpellTarget.Self;
    Description = "This spell scrambles the caster's memory.";
  }
  
  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { if(user==App.Player && App.Player.Memory!=null)
    { App.Player.Memory = Wipe(App.Player.Memory, buc);
      if((buc&ItemStatus.Cursed)!=0)
      { int index = user.Map.Index;
        if(index>0 && user.Map.Section[index-1].Memory!=null)
          user.Map.Section[index-1].Memory = Wipe(user.Map.Section[index-1].Memory, buc);
        if(index<user.Map.Section.Count-1 && user.Map.Section[index+1].Memory!=null)
          user.Map.Section[index+1].Memory = Wipe(user.Map.Section[index+1].Memory, buc);
      }
      App.IO.Print("You feel your mind being twisted!");
    }
  }

  public static readonly AmnesiaSpell Default = new AmnesiaSpell();

  Map Wipe(Map good, ItemStatus buc)
  { Map bad = new Map(good.Width, good.Height, TileType.Border, false);
    int count = good.Width*good.Height/20;
    if((buc&ItemStatus.Blessed)!=0) count *= 2;
    for(int i=0; i<count; i++) // put some of the old tiles in there
    { int x = Global.Rand(good.Width), y = Global.Rand(good.Height);
      if(good[x, y].Type!=TileType.Border)
      { bad.SetType(x, y, good[x, y].Type);
        bad.SetFlag(x, y, Tile.Flag.Seen, true);
      }
    }
    for(int i=0; i<count; i++) // put some random tiles in there
    { int x = Global.Rand(good.Width), y = Global.Rand(good.Height);
      bad.SetType(x, y, (TileType)Global.Rand((int)TileType.NumTypes));
    }
    return bad;
  }
}
#endregion

#region Identify
public class IdentifySpell : Spell
{ public IdentifySpell()
  { Name="identify"; Class=SpellClass.Divination; Difficulty=5; Power=3; target=SpellTarget.Item; AutoIdentify=true;
    Description = "This spell provides the caster full knowledge of an item.";
  }

  public override void Cast(Entity user, ItemStatus buc, Item target)
  { if(user!=App.Player) return;

    if((buc&ItemStatus.Cursed)!=0 && Global.Coinflip()) App.IO.Print("Nothing seems to happen.");
    else
    { App.Player.AddKnowledge(target, true);
      App.IO.Print("{0} - {1}", target.Char, target.GetAName());
    }
  }

  public static readonly IdentifySpell Default = new IdentifySpell();
}
#endregion

} // namespace Chrono