using System;
using System.Collections.Generic;
using System.Reflection;
using Point=System.Drawing.Point;

namespace Chrono
{

public enum SpellTarget { Self, Item, Tile };

#region Spell
public abstract class Spell
{
  public void Cast(Entity user) { Cast(user, null, user.Pos, Direction.Self); }
  public void Cast(Entity user, Item item) { Cast(user, item, user.Pos, Direction.Self); }
  public void Cast(Entity user, Direction dir) { Cast(user, null, new RangeTarget(dir)); }
  public void Cast(Entity user, Item item, Direction dir) { Cast(user, item, new RangeTarget(dir)); }
  public void Cast(Entity user, RangeTarget rt) { Cast(user, null, rt); }
  public void Cast(Entity user, Item item, RangeTarget rt)
  {
    if(rt.Dir!=Direction.Invalid) Cast(user, item, Global.Move(user.Pos, rt.Dir), rt.Dir);
    else if(rt.Point.X!=-1) Cast(user, item, rt.Point, rt.Dir);
  }
  public void Cast(Entity user, Point target) { Cast(user, null, target, Direction.Invalid); }
  public void Cast(Entity user, Item item, Point target) { Cast(user, item, target, Direction.Invalid); }
  public virtual void Cast(Entity user, Item item, Item target) { }

  // TODO: implement this. returns chance (0-100) of casting from memory
  public int CastChance(Entity user) { throw new NotImplementedException(); }
  public bool CastTest(Entity user) { return Global.Rand(100)<CastChance(user); }

  public virtual SpellTarget GetSpellTarget(Entity user, Item item) { return target; }

  // TODO: implement this. returns chance (0-100) of learning from a spellbook
  public int LearnChance(Entity user) { throw new NotImplementedException(); }

  public string Name, Description;
  public int Difficulty, MP, Range; // difficulty is 0-100. MP is MP used per cast. Range is in tiles (-1 = infinite)
  public Skill Skill; // what casting skill does this spell use?
  public bool AutoIdentify; // is the effect obvious enough that the spell should be autoidentified to the player?

  public static int CastPenalty(Entity user) // TODO: implement this. returns spellcasting penalty based on user's equipment, etc
  {
    throw new NotImplementedException();
  }

  public static Spell Get(string name)
  {
    if(spells == null)
    {
      spells = new Dictionary<string,Spell>();
      foreach(Type type in Assembly.GetExecutingAssembly().GetTypes())
      {
        if(type.IsSubclassOf(typeof(Spell)))
        {
          FieldInfo fi = type.GetField("Instance", BindingFlags.Static|BindingFlags.Public);
          if(fi!=null)
          {
            string typename = type.Name;
            if(typename.EndsWith("Spell")) typename = typename.Substring(0, typename.Length-5);
            spells[typename] = (Spell)fi.GetValue(null);
          }
        }
      }
    }

    Spell spell;
    if(!spells.TryGetValue(name, out spell)) throw new ArgumentException("No such spell: "+name);
    return spell;
  }

  protected virtual void Cast(Entity user, Item item, Point target, Direction dir) { }

  protected virtual bool TryHit(Entity user, Item item, Entity target)
  {
    throw new NotImplementedException();
  }

  protected SpellTarget target;

  static Dictionary<string,Spell> spells;
}
#endregion

#region BeamSpell
// implements spells that act as beams (that can possibly bounce and be reflected)
public abstract class BeamSpell : Spell
{
  protected BeamSpell() { target=SpellTarget.Tile; Range=10; Bouncy=true; }

  const int MaxBounces=3;

  public bool Bouncy;

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(dir==Direction.Above || dir==Direction.Below) return;
    else if(dir==Direction.Self || target==user.Pos) Affect(user, item, user);
    else
    {
      bounces = Bouncy ? 0 : MaxBounces;
      lastPt  = user.Pos;
      Global.TraceLine(lastPt, target, Range, false, new TracePoint(ZapPoint), new BeamContext(user, item));
    }
  }

  // affects an entity. assumes the entity doesn't have reflection
  protected virtual void Affect(Entity user, Item item, Entity target) { Affect(user, item, target.Pos); }
  // affect a map tile (not the entity on it). returns what the trace should do
  protected virtual TraceAction Affect(Entity user, Item item, Point target) { return TraceAction.Go; }

  sealed class BeamContext
  {
    public BeamContext(Entity user, Item item) { User=user; Item=item; }
    public Entity User;
    public Item Item;
  }

  TraceAction ZapPoint(Point pt, object context)
  {
    BeamContext bc = (BeamContext)context;
    TraceAction ret;

    if(Map.IsUsuallyPassable(bc.User.Map[pt].Type))
    {
      Entity target = bc.User.Map.GetEntity(pt);
      if(target!=null && TryHit(bc.User, bc.Item, target))
      {
        if(!target.HasAbility(Ability.Reflection))
        {
          ret = TraceAction.Go;
          Affect(bc.User, bc.Item, target);
        }
        else
        {
          ret = TraceAction.Bounce;
          if(bc.User==App.Player)
          {
            if(!App.Player.IsOrSees(target)) App.IO.Print("The spell reflects unexpectedly!");
            else App.IO.Print("The spell reflects from {0}!", target==App.Player ? "your body" : target.theName);
          }
        }
      }
      else ret = Affect(bc.User, bc.Item, pt);
    }
    else ret = Affect(bc.User, bc.Item, pt);

    if(ret==TraceAction.Bounce)
    {
      if(++bounces>=MaxBounces) ret = TraceAction.Stop;
      else
      {
        if(App.Player.CanSee(pt)) App.IO.Print("The spell bounces!");
        if(!Map.IsUsuallyPassable(bc.User.Map[lastPt.X, pt.Y].Type)) ret &= ~TraceAction.HBounce;
        if(!Map.IsUsuallyPassable(bc.User.Map[pt.X, lastPt.Y].Type)) ret &= ~TraceAction.VBounce;
        if(ret==0) ret = TraceAction.Bounce;
      }
    }

    lastPt = pt;
    return ret;
  }

  static Point lastPt;
  static int bounces;
}
#endregion

#region DirectionalSpell
public abstract class DirectionalSpell : Spell
{
  protected DirectionalSpell() { target=SpellTarget.Tile; Range=1; }

  // affects an entity. assumes the entity doesn't have reflection
  protected virtual void Affect(Entity user, Item item, Entity target) { Affect(user, item, target.Pos); }
  // affect a map tile (not the entity on it)
  protected virtual void Affect(Entity user, Item item, Point target) { }

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(dir==Direction.Up || dir==Direction.Down) return;
    else if(dir==Direction.Self || target==user.Pos) Affect(user, item, user);
    else
    {
      Point pt = Global.Move(user.Pos, dir);
      Entity hit = user.Map.GetEntity(user.Pos);
      if(hit!=null && TryHit(user, item, hit)) Affect(user, item, hit);
      else Affect(user, item, pt);
    }
  }
}
#endregion

#region Amnesia
public sealed class AmnesiaSpell : Spell
{
  AmnesiaSpell()
  {
    Name="amnesia"; Skill=Skill.Divination; Difficulty=5; MP=2; target=SpellTarget.Self;
    Description = "This spell scrambles the caster's memory.";
  }

  public static readonly AmnesiaSpell Instance = new AmnesiaSpell();

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(user==App.Player)
    {
      if(App.Player.Memory!=null) App.Player.Memory = Wipe(App.Player.Memory, item);
      if(item!=null && item.Cursed)
      { // TODO: wipe adjacent maps as well

        // forget about some item classes
        int forget = Global.Rand(4, 10);
        {
          int[] classes = new int[forget];
          foreach(int classIndex in App.Player.Knowledge)
          {
            classes[--forget] = classIndex;
            if(forget == 0) break;
          }
          foreach(int classIndex in classes)
          {
            App.Player.Knowledge.Remove(classIndex);
          }
        }

        // forget about some items in the inventory
        forget = Math.Min(App.Player.Inv.Count, Global.Rand(4, 10));
        for(int tri=forget*10, half=tri/2; forget!=0 && tri!=0; tri--)
        {
          Item i = App.Player.Inv[Global.Rand(App.Player.Inv.Count)];
          if(i.KnowEnchantment || (tri>=half && i.KnowCB))
          {
            i.Unidentify(true);
            forget--;
          }
        }
      }
    }
  }

  static Map Wipe(Map good, Item item)
  {
    Map bad = new Map(good.Width, good.Height, TileType.Border, false);

    // put some of the old tiles in there
    int count = good.Width*good.Height / (item.Blessed ? 10 : 20);
    for(int i=0; i<count; i++)
    {
      int x = Global.Rand(good.Width), y = Global.Rand(good.Height);
      bad.SetType(x, y, good[x, y].Type);
    }

    // put some random tiles in there
    count = good.Width*good.Height / (item.Cursed ? 10 : 20);
    for(int i=0; i<count; i++)
    {
      int x = Global.Rand(good.Width), y = Global.Rand(good.Height);
      bad.SetType(x, y, (TileType)Global.Rand((int)TileType.NumTypes));
    }

    return bad;
  }
}
#endregion

#region Fire
public sealed class FireSpell : BeamSpell
{
  FireSpell()
  {
    Name="fire"; Skill=Skill.Attack; Difficulty=60; MP=12; AutoIdentify=true;
    Description = "The fire spell hurls a great bolt of flames.";
  }

  public static readonly FireSpell Instance = new FireSpell();

  protected override void Affect(Entity user, Item item, Entity target)
  {
    Damage damage = new Damage();
    damage.Heat += Global.NdN(4, 10);
    target.TrySpellDamage(this, user, item, ref damage);
    if(target.Inv!=null)
    {
      bool print = App.Player.IsOrSees(target);
      for(int i=0; i<target.Inv.Count; i++)
        if(Global.Rand(100)<30 && BurnItem(target, target.Inv, i, print)) i--;
    }
  }

  protected override TraceAction Affect(Entity user, Item item, Point target)
  {
    Tile tile = user.Map[target];
    if(tile.Type==TileType.ClosedDoor) // if it hits a closed door
    {
      if(Global.OneIn(10)) return TraceAction.Bounce; // there's a 10% chance of the spell bouncing
      if(App.Player.CanSee(target)) App.IO.Print("A door burns down!"); // and a 90% chance of it destroying the door
      user.Map.SetType(target, TileType.RoomFloor);
    }
    else if(tile.Type==TileType.Ice) user.Map.SetType(target, TileType.ShallowWater);
    else if(tile.Type==TileType.DeepIce) user.Map.SetType(target, TileType.DeepWater); // TODO: make the items fall into the water rather than burn

    if(tile.Items!=null)
    {
      bool print = App.Player.CanSee(target);
      for(int i=0; i<tile.Items.Count; i++) if(BurnItem(null, tile.Items, i, print)) i--;
    }
    return TraceAction.Go;
  }

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(dir==Direction.Above)
    {
      if(user==App.Player) App.IO.Print("The spell bounces back down onto your head!");
      Affect(user, item, user);
    }
    else if(dir==Direction.Below)
    {
      ItemPile items = user.Map[target].Items;
      bool print = user==App.Player;
      if(items!=null) for(int i=0; i<items.Count; i++) if(BurnItem(null, items, i, print)) i--;
      if(print) App.IO.Print("The bugs on the ground are incinerated!");
    }
    else base.Cast(user, item, target, dir);
  }

  bool BurnItem(Entity user, IInventory inv, int itemIndex, bool print)
  {
    Item i = inv[itemIndex];

    // destroy these outright
    if(i.Type==ItemType.Scroll || i.Type==ItemType.Potion || i.Type==ItemType.Spellbook || i.Burn())
    {
      if(!i.Blessed || Global.Coinflip()) // blessed items have a 50% chance of being unaffected
      {
        if(print)
          App.IO.Print(i.Type==ItemType.Potion ? "{0} heat{1} up and burst{1}!" : "{0} burn{1} up!",
                       user==App.Player ? "Your "+i.GetFullName() : Global.Cap1(i.GetAName()), i.VerbS);
        inv.RemoveAt(itemIndex);
        return true;
      }
      else if(print) App.IO.Print("{0} is miraculously unharmed.",
                                  user==App.Player ? "Your "+i.GetFullName() : Global.Cap1(i.GetAName()), i.VerbS);
    }
    else if(!i.FireProof)
    {
      if(print) App.IO.Print("{0} smoulder{1}.",
                             user==App.Player ? "Your "+i.GetFullName() : Global.Cap1(i.GetAName()), i.VerbS);
    }
    else if(print) App.IO.Print("{0} {1} seem to be affected.",
                                user==App.Player ? "Your "+i.GetFullName() : Global.Cap1(i.GetAName()),
                                i.Count==1 ? "doesn't" : "don't");
    return false;
  }
}
#endregion

#region ForceBolt
public sealed class ForceBoltSpell : BeamSpell
{
  ForceBoltSpell()
  {
    Name="force bolt"; Skill=Skill.Attack; Difficulty=5; MP=5; Bouncy=false;
    Description = "This spell projects a massive shockwave in a given direction.";
  }

  public static readonly ForceBoltSpell Instance = new ForceBoltSpell();

  protected override void Affect(Entity user, Item item, Entity target)
  {
    Damage damage = new Damage(Global.NdN(1, 6));
    target.TrySpellDamage(this, user, item, ref damage);
  }

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(dir==Direction.Above && user==App.Player)
      App.IO.Print("Bits of stone rain down on you as the spell slams into the ceiling.");
    else if(dir==Direction.Below && user==App.Player)
      App.IO.Print("The bugs on the ground are crushed!");
    else base.Cast(user, item, target, dir);
  }
}
#endregion

#region Heal
public sealed class HealSpell : DirectionalSpell
{
  HealSpell()
  {
    Name="heal"; Skill=Skill.Healing; Difficulty=15; MP=5; target=SpellTarget.Tile;
    Description = "This spell will cure the caster's target of poisons, and rejuvinate it as well.";
  }

  public static readonly HealSpell Instance = new HealSpell();

  protected override void Affect(Entity user, Item item, Entity target)
  {
    int toHeal = item==null || item.Uncursed ? Global.Rand(6, 24)
                                           : item.Cursed ? Global.Rand(4, 16) : Global.Rand(8, 32);
    if(item!=null && item.Blessed && toHeal>target.MaxHP-target.HP)
    {
      target.AlterBaseAttr(Attr.MaxHP, 1);
      target.HP = target.MaxHP;
    }
    else target.HP += toHeal;

    target.Exercise(Attr.Str);

    if(target==App.Player) App.IO.Print("You feel better.");
    else if(!App.Player.HasAilment(Ailment.Blind) && App.Player.CanSee(target))
      App.IO.Print("{0} looks better.", target.TheName);
  }

  protected override bool TryHit(Entity user, Item item, Entity target) { return true; } // always hits
}
#endregion

#region Identify
public sealed class IdentifySpell : Spell
{
  IdentifySpell()
  {
    Name="identify"; Skill=Skill.Divination; Difficulty=25; MP=10; target=SpellTarget.Item; AutoIdentify=true;
    Description = "This spell provides the caster with full knowledge of an item.";
  }

  public override void Cast(Entity user, Item item, Item target)
  {
    if(user!=App.Player) return;
    App.Player.AddKnowledge(target.Class);
    App.IO.Print("{0} - {1}", target.Char, target.GetAName());
  }

  public static readonly IdentifySpell Instance = new IdentifySpell();
}
#endregion

#region RemoveScent
public sealed class RemoveScentSpell : Spell
{
  RemoveScentSpell()
  {
    Name="remove scent"; Skill=Skill.Enchantment; Difficulty=10; MP=8; target=SpellTarget.Self; AutoIdentify=true;
    Description = "This spell will make the caster small as fresh as a rose.";
  }

  public static readonly RemoveScentSpell Instance = new RemoveScentSpell();

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(item!=null && item.Cursed)
    {
      user.Stench = Map.MaxScent;
      if(user==App.Player) App.IO.Print("Wow, now you smell /really/ bad!");
    }
    else
    {
      user.Stench = 0;
      if(user==App.Player) App.IO.Print("You smell much better.");
    }
  }
}
#endregion

#region TeleportSelf
public sealed class TeleportSelfSpell : Spell
{
  TeleportSelfSpell()
  {
    Name="teleport"; Skill=Skill.Escape; Difficulty=35; MP=9; AutoIdentify=true; Range=-1;
    Description = "This spell will teleport the caster to a new location.";
  }

  public static readonly TeleportSelfSpell Instance = new TeleportSelfSpell();

  protected override void Cast(Entity user, Item item, Point target, Direction dir)
  {
    if(!user.HasAbility(Ability.TeleportControl) && (item==null || !item.Blessed)) target = user.Map.FreeSpace();

    if(user!=App.Player)
    {
      bool canSeeDest = App.Player.HasAilment(Ailment.Blind) && App.Player.HasAbility(Ability.Clairvoyant) ||
                      App.Player.CanSee(target);
      if(App.Player.CanSee(user)) App.IO.Print("{0} {1}.", user.TheName, canSeeDest ? "teleports" : "disappears");
      else
      {
        user.Pos = target;
        if(App.Player.CanSee(user)) App.IO.Print("{0} appears out of nowhere!", user.AName);
        return;
      }
    }
    user.Pos = target;
  }

  public override SpellTarget GetSpellTarget(Entity user, Item item)
  {
    return user!=null && user.HasAbility(Ability.TeleportControl) || item!=null && item.Blessed
          ? SpellTarget.Tile : SpellTarget.Self;
  }
}
#endregion

} // namespace Chrono