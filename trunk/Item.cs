using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chrono
{

#region Enums
public enum ItemClass : sbyte
{ Invalid=-1, Any=-1,
  Gold, Amulet, Weapon, Shield, Armor, Ammo, Food, Corpse, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container,
  Treasure, NumClasses
}

[Flags] public enum ItemUse : byte { NoUse=0, Self=1, UseTarget=2, UseDirection=4, UseBoth=UseTarget|UseDirection };

[Flags] public enum ItemStatus : byte
{ None=0, Identified=1, KnowCB=2, Burnt=4, Rotted=8, Rusted=16, Cursed=32, Blessed=64
};

public enum Material : byte { Paper, Leather, HardMaterials, Wood=HardMaterials, Metal, Glass }
#endregion

#region Item
public abstract class Item : UniqueObject, ICloneable
{ public Item()
  { Prefix=PluralPrefix=string.Empty; PluralSuffix="s"; Count=1; Color=Color.White; Durability=-1;
  }
  protected Item(SerializationInfo info, StreamingContext context) : base(info, context) { }
  
  public string AreIs { get { return Count>1 ? "are" : "is"; } }
  
  public bool Blessed { get { return (Status&(ItemStatus.Cursed|ItemStatus.Blessed)) == ItemStatus.Blessed; } }
  public bool CBUnknown { get { return (Status&ItemStatus.KnowCB) == 0; } }
  public bool Cursed { get { return (Status&(ItemStatus.Cursed|ItemStatus.Blessed)) == ItemStatus.Cursed; } }
  public bool Uncursed { get { return (Status&(ItemStatus.Cursed|ItemStatus.Blessed)) == 0; } }
  public bool KnownBlessed
  { get
    { return (Status&(ItemStatus.KnowCB|ItemStatus.Cursed|ItemStatus.Blessed))==(ItemStatus.KnowCB|ItemStatus.Blessed);
    }
  }
  public bool KnownCursed
  { get
    { return (Status&(ItemStatus.KnowCB|ItemStatus.Cursed|ItemStatus.Blessed))==(ItemStatus.KnowCB|ItemStatus.Cursed);
    }
  }
  public bool KnownUncursed
  { get { return (Status&(ItemStatus.KnowCB|ItemStatus.Cursed|ItemStatus.Blessed))==ItemStatus.KnowCB; }
  }

  public int FullWeight { get { return Weight*Count; } }

  public bool Identified { get { return (Status&ItemStatus.Identified)!=0; } }

  public virtual string Name { get { return name; } }

  public string ItOne { get { return Count>1 ? "one" : "it"; } }
  public string ItThem { get { return Count>1 ? "them" : "it"; } }
  
  public string StatusString
  { get
    { string ret="";
      if(!CBUnknown)
      { if(KnownUncursed) ret = "uncursed";
        else if(KnownBlessed) ret = "blessed";
        else if(KnownCursed) ret = "cursed";
      }
  
      if((Status&ItemStatus.Burnt)!=0) ret += (ret!="" ? ", " : "") + "burnt";
      if((Status&ItemStatus.Rotted)!=0) ret += (ret!="" ? ", " : "") + "rotted";
      if((Status&ItemStatus.Rusted)!=0) ret += (ret!="" ? ", " : "") + "rusted";
      return ret;
    }
  }

  public object Clone() { return Clone(false); }
  public virtual object Clone(bool force)
  { Type t = GetType();
    if(!force && t.GetCustomAttributes(typeof(NoCloneAttribute), true).Length!=0) return this;
    object o = t.GetConstructor(Type.EmptyTypes).Invoke(null), v;
    while(t!=null)
    { foreach(FieldInfo f in t.GetFields(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        if(!f.IsNotSerialized)
        { v = f.GetValue(this);
          f.SetValue(o, v!=null && f.FieldType.IsSubclassOf(typeof(Array)) ? ((ICloneable)v).Clone() : v);
        }
      t = t.BaseType;
    }
    return o;
  }

  public virtual bool CanStackWith(Item item)
  { if(Status!=item.Status || Shop!=item.Shop || item.Title!=null || Title!=null || item.GetType() != GetType())
      return false;
    if(Class==ItemClass.Food || Class==ItemClass.Potion || Class==ItemClass.Scroll || Class==ItemClass.Ammo ||
       Class==ItemClass.Weapon && Class==ItemClass.Treasure)
      return true;
    return false;
  }

  public void Bless() { Status = Status&~ItemStatus.Cursed|ItemStatus.Blessed; }
  public void Curse() { Status = Status&~ItemStatus.Blessed|ItemStatus.Cursed; }
  public void Uncurse() { Status &= ~(ItemStatus.Blessed|ItemStatus.Cursed); }

  public string GetAName(Entity e) { return Global.WithAorAn(GetFullName(e, false)); }
  public string GetAName(Entity e, bool forceSingluar) { return Global.WithAorAn(GetFullName(e, forceSingluar)); }
  public string GetFullName(Entity e) { return GetFullName(e, false); }
  public virtual string GetFullName(Entity e, bool forceSingular)
  { string status = StatusString;
    if(status!="") status += ' ';
    string ret = !forceSingular && Count>1 ? Count.ToString()+' '+status+PluralPrefix+Name+PluralSuffix
                                           : status+Prefix+Name;
    if(Title!=null) ret += " named "+Title;
    return ret;
  }
  public virtual string GetInvName(Entity e)
  { string s = Global.WithAorAn(GetFullName(e));
    if(Shop!=null) s += " (unpaid)";
    return s;
  }
  public string GetThatName(Entity e) { return (Count>1 ? "those " : "that ") + GetFullName(e); }

  public byte GetNoise(Entity e) { return (byte)Math.Max(Math.Min(Noise*15-e.Stealth*8, 255), 0); }

  // item is thrown at or bashed on an entity. returns true if item should be destroyed
  public virtual bool Hit(Entity user, Point pos) { return false; }
  public virtual bool Hit(Entity user, Entity hit) { return false; }

  public virtual bool Invoke(Entity user)
  { if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return false;
  }
  
  public bool Is(ItemStatus status) { return (Status&status)!=0; }

  public virtual bool Think(Entity holder) { Age++; return false; }

  public bool Use(Entity user) { return Use(user, Direction.Self); } // returns true if item should be consumed
  public virtual bool Use(Entity user, Direction dir)
  { if((Usability&ItemUse.UseDirection)==0 && (int)dir<8) return Use(user, Global.Move(user.Position, dir));
    if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return false;
  }
  public virtual bool Use(Entity user, System.Drawing.Point target) // returns true item should be consumed
  { if(user==App.Player) App.IO.Print("Nothing seems to happen.");
    return false;
  }

  public virtual void OnDrop  (Entity holder) { }
  public virtual void OnPickup(Entity holder) { }
  public virtual void OnMap() { } // put on the map

  public Item Split(int toRemove)
  { if(toRemove>=Count) throw new ArgumentOutOfRangeException("toRemove", toRemove, "is >= than Count");
    Item newItem = (Item)Clone(true);
    newItem.Count = toRemove;
    Count -= toRemove;
    return newItem;
  }

  public string Title, Prefix, PluralPrefix, PluralSuffix;
  public Shop   Shop;
  public int Age, Count, Weight; // age in map ticks, number in the stack, weight (5 = approx. 1 pound)
  public short Durability;       // 0 - 100 (chance of breaking if thrown), or -1, which uses the default
  public char Char;
  public ItemClass Class;
  public byte Noise;             // noise made when using the item (0-10)
  public Color Color;
  public ItemUse Usability;
  public ItemStatus Status;

  protected string name;
}
#endregion

#region Modifying
public abstract class Modifying : Item
{ protected Modifying() { }
  protected Modifying(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override bool CanStackWith(Item item) { return false; }

  public int AC { get { return Mods[(int)Attr.AC]; } set { Mods[(int)Attr.AC]=value; } }
  public int ColdRes { get { return Mods[(int)Attr.ColdRes]; } set { Mods[(int)Attr.ColdRes]=value; } }
  public int Dex { get { return Mods[(int)Attr.Dex]; } set { Mods[(int)Attr.Dex]=value; } }
  public int ElectricityRes
  { get { return Mods[(int)Attr.ElectricityRes]; }
    set { Mods[(int)Attr.ElectricityRes]=value; }
  }
  public int EV { get { return Mods[(int)Attr.EV]; } set { Mods[(int)Attr.EV]=value; } }
  public int HeatRes { get { return Mods[(int)Attr.HeatRes]; } set { Mods[(int)Attr.HeatRes]=value; } }
  public int Int { get { return Mods[(int)Attr.Int]; } set { Mods[(int)Attr.Int]=value; } }
  public int MaxHP { get { return Mods[(int)Attr.MaxHP]; } set { Mods[(int)Attr.MaxHP]=value; } }
  public int MaxMP { get { return Mods[(int)Attr.MaxMP]; } set { Mods[(int)Attr.MaxMP]=value; } }
  public int PoisonRes { get { return Mods[(int)Attr.PoisonRes]; } set { Mods[(int)Attr.PoisonRes]=value; } }
  public int Speed { get { return Mods[(int)Attr.Speed]; } set { Mods[(int)Attr.Speed]=value; } }
  public int Stealth { get { return Mods[(int)Attr.Stealth]; } set { Mods[(int)Attr.Stealth]=value; } }
  public int Str { get { return Mods[(int)Attr.Str]; } set { Mods[(int)Attr.Str]=value; } }

  public int  GetAttr(Attr attribute) { return Mods[(int)attribute]; }
  public void SetAttr(Attr attribute, int val) { Mods[(int)attribute]=val; }

  public bool GetFlag(Entity.Flag flag) { return (FlagMods&flag)!=0; }
  public void SetFlag(Entity.Flag flag, bool on) { if(on) FlagMods |= flag; else FlagMods &= ~flag; }

  public int[] Mods = new int[(int)Attr.NumModifiable];
  public Entity.Flag FlagMods;
}
#endregion

#region Wearable, Wieldable, Readable, Chargeable
public abstract class Wearable : Modifying
{ public Wearable() { Slot=Slot.Invalid; }
  protected Wearable(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override string GetInvName(Entity e)
  { string ret = GetAName(e);
    if(worn) ret += " (worn)";
    return ret;
  }

  public override void OnMap() { worn = false; }
  public virtual void OnRemove(Entity equipper) { worn=false;  }
  public virtual void OnWear  (Entity equipper) { worn=true; }

  public Slot Slot;
  public Material Material;
  bool worn;
}

public abstract class Wieldable : Modifying
{ protected Wieldable() { }
  protected Wieldable(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override string GetInvName(Entity e)
  { string ret = GetAName(e);
    if(equipped) ret += " (equipped)";
    return ret;
  }

  public virtual void OnEquip  (Entity equipper) { equipped=true;  }
  public virtual void OnUnequip(Entity equipper) { equipped=false; }
  public override void OnMap() { equipped=false; }

  public Attr Exercises;
  public bool AllHandWield;
  bool equipped;
}

public abstract class Readable : Item
{ protected Readable() { }
  protected Readable(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

public abstract class Chargeable : Item
{ public Chargeable() { }
  protected Chargeable(SerializationInfo info, StreamingContext context) : base(info, context) { }
  public int Charges, Recharged;
}
#endregion

[Serializable]
public class Gold : Item
{ public Gold() { name="gold piece"; Color=Color.Yellow; Class=ItemClass.Gold; }
  public Gold(int count) : this() { Count=count; }
  public Gold(SerializationInfo info, StreamingContext context) : base(info, context) { }

  public override bool CanStackWith(Item item) { return item.Class==ItemClass.Gold; }
  
  public static readonly int SpawnChance=200; // 2% chance
  public static readonly int SpawnMin=3, SpawnMax=16; // 2% chance
  public static readonly int ShopValue=1;
}

} // namespace Chrono