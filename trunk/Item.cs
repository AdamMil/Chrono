using System;
using System.Drawing;
using System.Reflection;
using System.Xml;

namespace Chrono
{

#region Enums
public enum ItemClass : sbyte
{ Invalid=-1, Any=-1,
  Gold, Amulet, Weapon, Shield, Armor, Ammo, Food, Corpse, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container,
  Treasure, NumClasses
}

[Flags] public enum ItemUse : byte { NoUse=0, Self=1, Target=2, Direction=4, Both=Target|Direction };

[Flags] public enum ItemStatus : byte
{ None=0, Identified=1, KnowCB=2, Burnt=4, Rotted=8, Rusted=16, Cursed=32, Blessed=64
};

public enum Material : byte { Paper, Leather, HardMaterials, Wood=HardMaterials, Metal, Glass }
#endregion

#region Item
public abstract class Item : UniqueObject
{ public Item()
  { Prefix=PluralPrefix=string.Empty; PluralSuffix="s"; Count=1; Color=Color.White; Durability=-1;
  }
  
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

  public virtual string Name
  { get { return name; }
    set { name=value; }
  }

  public string ItOne  { get { return Count>1 ? "one"  : "it"; } }
  public string ItThem { get { return Count>1 ? "them" : "it"; } }
  public string ItThey { get { return Count>1 ? "they" : "it"; } }
  public string ThatThose { get { return Count>1 ? "those" : "that"; } }
  public string VerbS  { get { return Count>1 ? "" : "s"; } }
  
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

  public Item Clone() { return Clone(false); }
  public virtual Item Clone(bool force)
  { Type type = GetType();
    if(!force && type.GetCustomAttributes(typeof(NoCloneAttribute), true).Length!=0) return this;
    Item item = (Item)type.GetConstructor(Type.EmptyTypes).Invoke(null);
    do
    { foreach(FieldInfo f in type.GetFields(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        if(!f.IsNotSerialized)
        { object v = f.GetValue(this);
          f.SetValue(item, v!=null && f.FieldType.IsSubclassOf(typeof(Array)) ? ((ICloneable)v).Clone() : v);
        }
      type = type.BaseType;
    } while(type!=typeof(object));
    return item;
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

  public string GetAName() { return Global.WithAorAn(GetFullName(false)); }
  public string GetAName(bool forceSingluar) { return Global.WithAorAn(GetFullName(forceSingluar)); }
  public string GetFullName() { return GetFullName(false); }
  public virtual string GetFullName(bool forceSingular)
  { string status = StatusString;
    if(status!="") status += ' ';
    string ret = !forceSingular && Count>1 ? Count.ToString()+' '+status+PluralPrefix+Name+PluralSuffix
                                           : status+Prefix+Name;
    if(Title!=null) ret += " named "+Title;
    return ret;
  }
  public virtual string GetInvName()
  { string s = Global.WithAorAn(GetFullName());
    if(Shop!=null) s += " (unpaid)";
    return s;
  }
  public string GetThatName() { return ThatThose + ' ' + GetFullName(); }

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
  { if((Usability&ItemUse.Direction)==0 && (int)dir<8) return Use(user, Global.Move(user.Position, dir));
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
    Item newItem = Clone(true);
    newItem.Count = toRemove;
    Count -= toRemove;
    return newItem;
  }

  public string Title, Prefix, PluralPrefix, PluralSuffix, ShortDesc, LongDesc;
  public Shop   Shop;
  public int Age, Count, Weight, ShopValue; // age in map ticks, number in the stack, weight (5 = approx. 1 pound)
  public short Durability;       // 0 - 100 (chance of breaking if thrown), or -1, which uses the default
  public char Char;
  public ItemClass Class;
  public byte Noise;             // noise made when using the item (0-10)
  public Color Color;
  public ItemUse Usability;
  public ItemStatus Status;

  public static Item ItemDef(XmlNode node)
  { Item item = Make(Xml.Attr(node, "class"));
    foreach(XmlAttribute attr in node.Attributes)
      switch(attr.Name)
      { case "class": break;
        case "value": item.ShopValue = int.Parse(attr.Value); break;
        default: Global.SetObjectValue(item, attr.Name, attr.Value); break;
      }
    return item;
  }

  public static Item Make(string item)
  { int pos = item.IndexOf(':');
    return (pos==-1 ? Global.GetItem("builtin", item)
                    : Global.GetItem(item.Substring(0, pos), item.Substring(pos+1))).MakeItem();
  }

  public static Item Make(XmlNode node)
  { switch(node.LocalName)
    { case "armor": return new XmlArmor(node);
      case "food": return new XmlFood(node);
      case "potion": return new XmlPotion(node);
      case "ring": return new XmlRing(node);
      case "scroll": return new XmlScroll(node);
      case "shield": return new XmlShield(node);
      case "spellbook": return new XmlSpellbook(node);
      case "tool": return Xml.IsEmpty(node, "charges") ? new XmlTool(node) : (Item)new XmlChargedTool(node);
      case "wand": return new XmlWand(node);
      default: throw new NotImplementedException("unknown xml item type: "+node.LocalName);
    }
  }
  
  public static Item Make(ItemClass itemClass, string name) { return Global.GetItem(itemClass, name).MakeItem(); }

  protected string name;
}
#endregion

#region Modifying
public abstract class Modifying : Item
{ protected Modifying() { }

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

  public override string GetInvName()
  { string ret = "";
    if(worn)       ret += (ret!="" ? ", " : "(") + "worn";
    if(Shop!=null) ret += (ret!="" ? ", " : "(") + "unpaid";
    return GetAName() + (ret!="" ? ' '+ret+')' : "");
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

  public override string GetInvName()
  { string ret = "";
    if(equipped)   ret += (ret!="" ? ", " : "(") + "equipped";
    if(Shop!=null) ret += (ret!="" ? ", " : "(") + "unpaid";
    return GetAName() + (ret!="" ? ' '+ret+')' : "");
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
}

public abstract class Chargeable : Item
{ public Chargeable() { }
  public int Charges, Recharged;
}
#endregion

#region Gold
public class Gold : Item
{ public Gold() { name="gold piece"; Color=Color.Yellow; Class=ItemClass.Gold; ShopValue=1; }
  public Gold(int count) : this() { Count=count; }

  public override bool CanStackWith(Item item) { return item.Class==ItemClass.Gold; }
  
  public static readonly int SpawnChance=200; // 2% chance
  public static readonly int SpawnMin=3, SpawnMax=16; // 3-16 pieces
}
#endregion

#region XmlItem
public sealed class XmlItem
{ public static void Init(Item item, XmlNode node)
  { item.Name = Xml.String(node, "name");
    if(!Xml.IsEmpty(node, "prefix")) item.Prefix = Xml.String(node, "prefix");
    if(!Xml.IsEmpty(node, "pluralPrefix")) item.PluralPrefix = Xml.String(node, "pluralPrefix");
    if(!Xml.IsEmpty(node, "pluralSuffix")) item.PluralSuffix = Xml.String(node, "pluralSuffix");
    if(!Xml.IsEmpty(node, "shortDesc")) item.ShortDesc = Xml.String(node, "shortDesc");
    if(!Xml.IsEmpty(node, "longDesc")) item.LongDesc = Xml.String(node, "longDesc");
    XmlNode child = node.SelectSingleNode("longDesc");
    if(child!=null) item.LongDesc = Xml.BlockToString(child.Value);
    if(!Xml.IsEmpty(node, "color")) item.Color = GetColor(node);
    if(!Xml.IsEmpty(node, "weight")) item.Weight = Xml.Int(node, "weight");
    if(!Xml.IsEmpty(node, "material")) ((Wearable)item).Material = GetMaterial(node);
    if(!Xml.IsEmpty(node, "value")) item.ShopValue = Xml.Int(node, "value");
  }

  public static void InitModifying(Modifying item, XmlNode node)
  { Init(item, node);
    foreach(string attr in attrs)
      if(!Xml.IsEmpty(node, attr)) item.SetAttr((Attr)Enum.Parse(typeof(Attr), attr, true), Xml.Int(node, attr));
    if(!Xml.IsEmpty(node, "effects")) item.FlagMods = GetEffect(node);
  }

  public static Color GetColor(XmlNode node)
  { return (Color)Enum.Parse(typeof(Color), Xml.Attr(node, "color"));
  }

  public static Entity.Flag GetEffect(XmlNode node)
  { string[] effects = Xml.List(node, "effects");
    Entity.Flag flags = Entity.Flag.None;
    foreach(string s in effects) flags |= (Entity.Flag)Enum.Parse(typeof(Entity.Flag), s);
    return flags;
  }

  public static Material GetMaterial(XmlNode node)
  { return (Material)Enum.Parse(typeof(Material), Xml.Attr(node, "material"));
  }

  public static Slot GetSlot(XmlNode node)
  { return (Slot)Enum.Parse(typeof(Slot), Xml.Attr(node, "slot"));
  }

  public static Spell GetSpell(XmlNode node) { return Spell.Get(Xml.Attr(node, "spell")); }

  static readonly string[] attrs = { "ac", "dex", "ev", "int", "light", "maxHP", "maxMP", "speed", "stealth", "str" };
}
#endregion

} // namespace Chrono