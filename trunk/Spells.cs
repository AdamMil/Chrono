using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chrono
{

public enum SpellTarget { Self, Item, Tile };
public enum SpellClass // remember to add these to the Skill enum as well
{ Summoning, Enchantment, Telekinesis, Translocation, Transformation, Divination, Channeling, Necromancy, Elemental,
  Poison
}

[Serializable]
public sealed class DefaultSpellProxy : ISerializable, IObjectReference
{ public DefaultSpellProxy(SerializationInfo info, StreamingContext context) { typename = info.GetString("Type"); }
  public void GetObjectData(SerializationInfo info, StreamingContext context) { } // never called
  public object GetRealObject(StreamingContext context)
  { return Type.GetType(typename).GetField("Default", BindingFlags.Static|BindingFlags.DeclaredOnly|BindingFlags.Public).GetValue(null);
  }
  string typename;
}

#region Spell
public abstract class Spell : UniqueObject
{ public Spell() { ID=Global.NextID; }
  protected Spell(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public void Cast(Entity user) { Cast(user, ItemStatus.None, user.Position, Direction.Self); }
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
  public void Cast(Entity user, Point tile, Direction dir) { Cast(user, ItemStatus.None, tile, dir); }
  public virtual void Cast(Entity user, ItemStatus buc, Point tile, Direction dir) { }

  public override void GetObjectData(SerializationInfo info, StreamingContext context)
  { if(Global.ObjHash==null && this==GetType().GetField("Default", BindingFlags.Static|BindingFlags.DeclaredOnly|BindingFlags.Public).GetValue(null))
    { info.AddValue("Type", GetType().FullName);  
      info.SetType(typeof(DefaultSpellProxy));
    }
    else base.GetObjectData(info, context);
  }

  public Skill Exercises { get { return (Skill)((int)Class+(int)Skill.WeaponSkills); } }
  public int Level { get { return (Difficulty+1)/2; } }

  // (Int-7) * 100 * (1.2 - 1/(skill^1.175)) / Difficulty - 10
  public int CastChance(Entity user) // assuming the user knows it
  { int skill = (user.GetSkill(Skill.Casting)+GetSpellSkill(user)+1)/2;
    double smul = 1.2-1/Math.Pow(1.1746189430880190059144636656919, skill);
    int chance = (int)Math.Round((user.Int-7)*100*smul/Difficulty) - 10;
    int penalty = CastPenalty(user);
    if(penalty>0) chance -= chance*penalty/100;
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }

  public int CastPenalty(Entity user)
  { int penalty = user.Shield==null ? 0 : 20; // -20% for having a shield
    for(int i=0; i<user.Hands.Length; i++)
      if(user.Hands[i]==null) break;
      else if(i==user.Hands.Length-1) penalty += 15; // -15% for having your hands full
    for(int i=0; i<armorPenalty.Length; i+=2)
      if(user.Slots[armorPenalty[i]]!=null && user.Slots[armorPenalty[i]].Material>=Material.HardMaterials)
        penalty += armorPenalty[i+1];
    penalty += (int)user.HungerLevel*10; // -10% per hunger level
    return penalty;
  }

  public bool CastTest(Entity user) { return Global.Rand(100)<CastChance(user); }

  public int GetSpellSkill(Entity user) { return user.GetSkill((Skill)((int)Class+(int)Skill.WeaponSkills)); }

  // (Int-8) * 100 * (1.1 - 1/(skill^1.126)) / Difficulty - 20
  public int LearnChance(Entity user)
  { double smul = 1.1-1/Math.Pow(1.2589254117941672104239541063958, GetSpellSkill(user));
    int chance = (int)Math.Round((user.Int-8)*100*smul/Difficulty) - 20;
    int penalty = (int)user.HungerLevel*10; // -10% per hunger level
    if(penalty>0) chance -= chance*penalty/100;
    return chance<0 ? 0 : chance>100 ? 100 : chance;
  }

  public virtual ICollection TracePath(Entity user, Point pt) { return null; }

  public string Name, Description;
  public SpellClass  Class;
  public SpellTarget Target;
  public int Difficulty; // 1-18, 1,2=level 1, 3,4=level 2, etc
  public int Memory; // memory of this spell, decreased every turn that the spell isn't cast, forgotten at zero
  public int Power;  // MP usage
  public bool AutoIdentify;
  
  protected static ArrayList path = new ArrayList();
  
  static readonly int[] armorPenalty = new int[]
  { (int)Slot.Feet, 5, (int)Slot.Hands, 10, (int)Slot.Head, 5, (int)Slot.Legs, 10, (int)Slot.Torso, 25
  };
}
#endregion

#region BeamSpell
public abstract class BeamSpell : Spell
{ protected BeamSpell() { Target=SpellTarget.Tile; }
  protected BeamSpell(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { FromStatus = buc;
    if((dir==Direction.Above || dir==Direction.Below) && !Affect(user, dir)) return;
    else if(user.Position==tile) Affect(user, user);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, 10, false, new LinePoint(ZapPoint), user);
    }
  }

  public override ICollection TracePath(Entity user, Point tile)
  { path.Clear();
    if(user.Position==tile) path.Add(tile);
    else
    { bounces=0; oldPt=user.Position;
      Global.TraceLine(oldPt, tile, 10, false, new LinePoint(TracePoint), user);
    }
    return path;
  }

  protected abstract object Hit(Entity user, Point pt);
  protected abstract void Affect(Entity user, object obj);
  protected abstract bool Affect(Entity user, Direction dir); // returns if execution should continue (in Cast)

  TraceAction TracePoint(Point pt, object context)
  { Entity user = (Entity)context;
    path.Add(pt);
    TraceAction ret;
    if(!Map.IsPassable(user.Map[pt].Type))
    { if(++bounces==3) return TraceAction.Stop;
      ret = TraceAction.Bounce;
      if(!Map.IsPassable(user.Map[oldPt.X, pt.Y].Type)) ret &= ~TraceAction.HBounce;
      if(!Map.IsPassable(user.Map[pt.X, oldPt.Y].Type)) ret &= ~TraceAction.VBounce;
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
      if(user==App.Player) App.IO.Print("The spell bounces!");
    }
    else ret=TraceAction.Go;
    oldPt=pt;
    object affected = Hit(user, pt);
    if(affected!=null) Affect(user, affected);
    return ret;
  }
  
  protected ItemStatus FromStatus;
  Point oldPt;
  int bounces;
}
#endregion

#region ForceBolt
[Serializable]
public class ForceBolt : BeamSpell
{ public ForceBolt()
  { Name="force bolt"; Class=SpellClass.Telekinesis; Difficulty=1; Power=2;
    Description = "The spell forces stuff. Yeah.";
  }
  public ForceBolt(SerializationInfo info, StreamingContext context) : base(info, context) { }

  protected override object Hit(Entity user, Point pt) { return user.Map.IsPassable(pt) ? null : (object)pt; }

  protected override bool Affect(Entity user, Direction dir)
  { if(dir==Direction.Above)
    { if(user==App.Player) App.IO.Print("Bits of stone rain down on you as the spell slams into the ceiling.");
      return false;
    }
    else if(dir==Direction.Below && user==App.Player) App.IO.Print("The bugs on the ground are crushed!");
    return false;
  }

  protected override void Affect(Entity user, object obj)
  { Damage damage = new Damage(Global.NdN(1, 6));
    damage.Direct = 2;
    user.TrySpellDamage(this, (Point)obj, damage);
  }

  public static ForceBolt Default = new ForceBolt();
}
#endregion

#region Fire
[Serializable]
public class FireSpell : BeamSpell
{ public FireSpell()
  { Name="fire"; Class=SpellClass.Elemental; Difficulty=10; Power=12; AutoIdentify=true;
    Description = "The fire spell hurls a great bolt of flames.";
  }
  public FireSpell(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public static FireSpell Default = new FireSpell();

  protected override object Hit(Entity user, Point pt)
  { Entity e = user.Map.GetEntity(pt);
    if(e==null) return pt;
    else if(Global.Coinflip()) return e;
    else
    { e.OnMissBy(user, this);
      user.OnMiss(e, this);
      return null;
    }
  }

  protected override bool Affect(Entity user, Direction dir)
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

  protected override void Affect(Entity user, object obj)
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
        for(int i=0; i<inv.Count; i++) if(AffectItem(inv, inv[i], print)) i--;
      }
    }
  }

  bool AffectItem(IInventory inv, Item i, bool print)
  { if(i.Class==ItemClass.Scroll || i.Class==ItemClass.Potion || i.Class==ItemClass.Spellbook)
    { if(print)
      { string plural = i.Count>1 ? "" : "s";
        App.IO.Print(i.Class==ItemClass.Potion ? "Your {0} heat{1} up and burst{1}!" : "Your {0} burn{1} up!",
                     i.GetFullName(App.Player), plural);
      }
      inv.Remove(i);
      return true;
    }
    return false;
  }
}
#endregion

#region Teleport
[Serializable]
public class TeleportSpell : Spell
{ public TeleportSpell()
  { Name="teleport"; Class=SpellClass.Translocation; Difficulty=6; Power=9; Target=SpellTarget.Self; AutoIdentify=true;
    Description = "This spell will teleport the caster to a random location.";
  }
  public TeleportSpell(SerializationInfo info, StreamingContext context) : base(info, context) { }


  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { user.Position = user.Map.FreeSpace();
    if(user!=App.Player) App.IO.Print("{0} disappears.", user.TheName);
  }

  public static TeleportSpell Default = new TeleportSpell();
}
#endregion

#region Amnesia
[Serializable]
public class AmnesiaSpell : Spell
{ public AmnesiaSpell()
  { Name="amnesia"; Class=SpellClass.Divination; Difficulty=2; Power=2; Target=SpellTarget.Self;
    Description = "This spell scrambles the caster's memory.";
  }
  public AmnesiaSpell(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public override void Cast(Entity user, ItemStatus buc, Point tile, Direction dir)
  { if(user.Memory!=null)
    { user.Memory = Wipe(user.Memory, buc);
      if((buc&ItemStatus.Cursed)!=0)
      { int index = user.Map.Index;
        if(index>0 && App.Dungeon[index-1].Memory!=null)
          App.Dungeon[index-1].Memory = Wipe(App.Dungeon[index-1].Memory, buc);
        if(index<App.Dungeon.Count-1 && App.Dungeon[index+1].Memory!=null)
          App.Dungeon[index+1].Memory = Wipe(App.Dungeon[index+1].Memory, buc);
      }

      if(user==App.Player) App.IO.Print("You feel your mind being twisted!");
    }
  }
  
  public static AmnesiaSpell Default = new AmnesiaSpell();
  
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
[Serializable]
public class IdentifySpell : Spell
{ public IdentifySpell()
  { Name="identify"; Class=SpellClass.Divination; Difficulty=5; Power=3; Target=SpellTarget.Item; AutoIdentify=true;
    Description = "This spell provides the caster full knowledge of an item.";
  }
  public IdentifySpell(SerializationInfo info, StreamingContext context) : base(info, context) { }

  
  public override void Cast(Entity user, ItemStatus buc, Item target)
  { if(user==App.Player)
    { user.AddKnowledge(target, true);
      App.IO.Print("{0} - {1}", target.Char, target.GetAName(user));
    }
  }

  public static IdentifySpell Default = new IdentifySpell();
}
#endregion

} // namespace Chrono