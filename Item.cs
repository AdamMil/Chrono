using System;
using System.Collections;
using System.Reflection;
using System.Xml;
using Point=System.Drawing.Point;

namespace Chrono
{

#region Enums
public enum Durability : sbyte { AlwaysBreaks=-1, NeverBreaks=-2, Minimum=0, Maximum=100 }

// verysoft = paper, cloth, etc
// soft = leather, etc
// medium = glass, wood, etc
// hard = metal, etc
// veryhard = diamond, etc
public enum Hardness : byte { VerySoft, Soft, Medium, Hard, VeryHard }

[Flags]
public enum ItemStatus : ushort
{
  Blessed=1, Cursed=2, AcidProof=4, FireProof=8, RotProof=16, RustProof=32,
  KnowCB=64, KnowEnchantment=128, HasData=256, HasCharges=512, Equipped=1024, Invokable=2048,
}

public enum ItemType : sbyte
{
  Invalid=-1, Any=-1,
  Gold, Amulet, Weapon, Shield, Armor, Ammo, Food, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container,
  Treasure, NumTypes
}

[Flags]
public enum ItemUse : byte { NoUse=0, Self=1, Item=2, Direction=4, Tile=Direction|8 };
#endregion

#region Materials
public abstract class Material
{
  protected Material(string name, Hardness hardness, bool burns, bool corrodes, bool rots, bool rusts)
  {
    Name=name; Hardness=hardness; Burns=burns; Corrodes=corrodes; Rots=rots; Rusts=rusts;
  }

  public readonly string Name;
  public readonly Hardness Hardness;
  public readonly bool Burns, Corrodes, Rots, Rusts;

  public static Material Get(string name)
  {
    if(materials==null)
    {
      materials = new SortedList();
      foreach(Type type in Assembly.GetExecutingAssembly().GetTypes())
        if(type.IsSubclassOf(typeof(Material)))
        {
          FieldInfo fi = type.GetField("Instance", BindingFlags.Static|BindingFlags.Public);
          if(fi!=null) materials[type.Name] = fi.GetValue(null);
        }
    }

    Material m = (Material)materials[name];
    if(m==null) throw new ArgumentException("No such material: "+name);
    return m;
  }

  static SortedList materials;
}

public sealed class Cloth : Material
{
  Cloth() : base("cloth", Hardness.VerySoft, true, true, false, false) { }
  public static readonly Cloth Instance = new Cloth();
}
public sealed class Paper : Material
{
  Paper() : base("paper", Hardness.VerySoft, true, true, false, false) { }
  public static readonly Paper Instance = new Paper();
}
public sealed class Leather : Material
{
  Leather() : base("leather", Hardness.Soft, true, false, true, false) { }
  public static readonly Leather Instance = new Leather();
}
public sealed class GoldMaterial : Material
{
  GoldMaterial() : base("gold", Hardness.Medium, false, false, false, false) { }
  public static readonly GoldMaterial Instance = new GoldMaterial();
}
public sealed class Wood : Material
{
  Wood() : base("wood", Hardness.Medium, true, false, true, false) { }
  public static readonly Wood Instance = new Wood();
}
public sealed class Metal : Material
{
  Metal() : base("metal", Hardness.Hard, false, true, false, true) { }
  public static readonly Metal Instance = new Metal();
}
public sealed class Glass : Material
{
  Glass() : base("glass", Hardness.Medium, false, false, false, false) { }
  public static readonly Glass Instance = new Glass();
}
public sealed class Diamond : Material
{
  Diamond() : base("diamond", Hardness.VeryHard, false, false, false, false) { }
  public static readonly Diamond Instance = new Diamond();
}
public sealed class UnknownMaterial : Material
{
  UnknownMaterial() : base("an unknown material", Hardness.Medium, false, false, false, false) { }
  public static readonly UnknownMaterial Instance = new UnknownMaterial();
}
#endregion

#region Item
public sealed class Item
{
  public Item(string name) : this(name, -1) { }
  public Item(string name, int count) : this(Global.GetItemIndex(name), count) { }
  public Item(ItemType type, string name) : this(type, name, -1) { }
  public Item(ItemType type, string name, int count) : this(Global.GetItemIndex(type, name), count) { }
  public Item(int itemIndex) : this(itemIndex, -1) { }
  public Item(int itemIndex, int count)
  {
    index = itemIndex;
    Class.InitializeItem(this);
    if(count!=-1) Count = count;
  }

  public Item(XmlNode node)
  {
    index = Global.GetItemIndex(Xml.Attr(node, "type"));
    Class.InitializeItem(this, node);
  }

  public string AreIs { get { return Count>1 ? "are" : "is"; } }
  public string ItOne { get { return Count>1 ? "one"  : "it"; } }
  public string ItThem { get { return Count>1 ? "them" : "it"; } }
  public string ItThey { get { return Count>1 ? "they" : "it"; } }
  public string ThatThose { get { return Count>1 ? "those" : "that"; } }
  public string VerbS { get { return Count>1 ? "" : "s"; } }

  public object Data
  {
    get
    {
      if(!Is(ItemStatus.HasData))
      {
        data = Class.InitializeData(this);
        Set(ItemStatus.HasData, true);
      }
      return data;
    }

    set
    {
      Set(ItemStatus.HasData, true);
      data = value;
    }
  }

  public string StatusString
  {
    get
    {
      string ret = null;

      Material m = Class.Material;
      if(m.Burns || m.Corrodes || m.Rots || m.Rusts) // if the material degrades
      {
        if((m.Burns    ? (Status&ItemStatus.FireProof)!=0 : true) &&
         (m.Corrodes ? (Status&ItemStatus.AcidProof)!=0 : true) &&
         (m.Rots     ? (Status&ItemStatus.RotProof)!=0  : true) &&
         (m.Rusts    ? (Status&ItemStatus.RustProof)!=0 : true))
          ret = "protected";
        else
        {
          if(m.Corrodes && AcidProof) ret = "acidproof";
          if(m.Burns && FireProof) ret += (ret==null ? null : ", ") + "fireproof";
          if(m.Rots && RotProof) ret += (ret==null ? null : ", ") + "treated";
          if(m.Rusts && RustProof) ret += (ret==null ? null : ", ") + "rustproof";
        }
      }

      if(Damage!=0)
      {
        if(ret!=null) ret += ", ";
        switch(Damage)
        {
          case 1: ret += "lightly "; break;
          case 2: break;
          case 3: ret += "heavily "; break;
          case 4: ret += "critically "; break;
          default: throw new NotSupportedException("unsupported damage value");
        }
        ret += "damaged";
      }

      if(KnowEnchantment && Class.ShowEnchantment)
        ret = ret + (ret==null ? null : " ") + (Enchantment<0 ? "" : "+") + Enchantment.ToString();

      if(KnowCB && Class.ShowCB)
      {
        string cb = Uncursed ? "uncursed" : Blessed ? "blessed" : "cursed";
        ret = ret + (ret==null ? null : " ") + cb;
      }

      return ret==null ? "" : ret;
    }
  }

  public ItemClass Class { get { return Global.GetItemClass(index); } }
  public ItemType Type { get { return Class.Type; } }
  public int Index { get { return index; } }

  public bool IsEquipped { get { return Is(ItemStatus.Equipped); } }

  public int FullWeight { get { return Count*Weight; } }
  public int Weight { get { return Class.GetWeight(this); } }

  public bool Blessed { get { return (Status&(ItemStatus.Cursed|ItemStatus.Blessed)) == ItemStatus.Blessed; } }
  public bool Cursed { get { return (Status&(ItemStatus.Cursed|ItemStatus.Blessed)) == ItemStatus.Cursed; } }
  public bool Uncursed { get { return !Is(ItemStatus.Cursed|ItemStatus.Blessed); } }

  public int BlessedSign
  {
    get
    {
      ItemStatus status = Status & (ItemStatus.Cursed|ItemStatus.Blessed);
      return status==0 ? 0 : status==ItemStatus.Cursed ? -1 : 1;
    }
  }

  public bool KnownBlessed
  {
    get
    {
      return (Status&(ItemStatus.KnowCB|ItemStatus.Cursed|ItemStatus.Blessed))==(ItemStatus.KnowCB|ItemStatus.Blessed);
    }
  }

  public bool KnownCursed
  {
    get
    {
      return (Status&(ItemStatus.KnowCB|ItemStatus.Cursed|ItemStatus.Blessed))==(ItemStatus.KnowCB|ItemStatus.Cursed);
    }
  }

  public bool KnownUncursed
  {
    get { return (Status&(ItemStatus.KnowCB|ItemStatus.Cursed|ItemStatus.Blessed))==ItemStatus.KnowCB; }
  }

  public bool KnowCB
  {
    get { return Is(ItemStatus.KnowCB); }
    set { Set(ItemStatus.KnowCB, value); }
  }

  public bool KnowEnchantment
  {
    get { return Is(ItemStatus.KnowEnchantment); }
    set { Set(ItemStatus.KnowEnchantment, value); }
  }

  public void Bless() { Status = Status&~ItemStatus.Cursed|ItemStatus.Blessed; }
  public void Curse() { Status = Status&~ItemStatus.Blessed|ItemStatus.Cursed; }
  public void Uncurse() { Status &= ~(ItemStatus.Blessed|ItemStatus.Cursed); }

  public bool AcidProof { get { return Is(ItemStatus.AcidProof) || !Class.Material.Corrodes; } }
  public bool FireProof { get { return Is(ItemStatus.FireProof) || !Class.Material.Burns; } }
  public bool RotProof { get { return Is(ItemStatus.RotProof)  || !Class.Material.Rots; } }
  public bool RustProof { get { return Is(ItemStatus.RustProof) || !Class.Material.Rusts; } }

  // returns true if item should be destroyed
  public bool Burn() { return !FireProof && Damage++==MaxDamage; }
  public bool Corrode() { return !AcidProof && Damage++==MaxDamage; }
  public bool Rot() { return !RotProof  && Damage++==MaxDamage; }
  public bool Rust() { return !RustProof && Damage++==MaxDamage; }

  public bool CanStackWith(Item item) { return Class.CanStack(this, item); }

  public Item Clone() { return (Item)MemberwiseClone(); }

  public string GetAName() { return GetAName(false); }
  public string GetAName(bool forceSingular) { return Class.GetAName(this, forceSingular); }
  public string GetFullName() { return GetFullName(false); }
  public string GetFullName(bool forceSingular) { return Class.GetFullName(this, forceSingular); }
  public string GetInvName() { return Class.GetInvName(this); }
  public string GetThatName() { return ThatThose + " " + GetFullName(); }

  // returns true if the item should be destroyed
  public bool Hit(Entity thrower, Point pt) { return Class.Hit(this, thrower, pt); }
  public bool Hit(Entity thrower, Entity victim) { return Class.Hit(this, thrower, victim); }

  public void Identify()
  {
    KnowCB = KnowEnchantment = true;
    Class.Identified = true;
  }

  public void Unidentify(bool forgetClass)
  {
    KnowCB = KnowEnchantment = false;
    if(forgetClass) Class.Identified = false;
  }

  // returns true if the item should be destroyed
  public bool Invoke(Entity user) { return Class.Invoke(this, user); }

  public bool Is(ItemStatus status) { return (Status&status)!=0; }
  public bool IsAll(ItemStatus status) { return (Status&status)==status; }

  public void Set(ItemStatus status, bool on)
  {
    if(on) Status |= status;
    else Status &= ~status;
  }

  public float Modify(Attr attr, float value) { return Class.Modify(this, attr, value); }
  public Ability Modify(Ability flags) { return Class.Modify(this, flags); }
  public AIFlag Modify(AIFlag flags) { return Class.Modify(this, flags); }
  public Ailment Modify(Ailment flags) { return Class.Modify(this, flags); }
  public Intrinsic Modify(Intrinsic flags) { return Class.Modify(this, flags); }

  public void OnDrop(Entity holder) { Class.OnDrop(this, holder); }

  public void OnEquip(Entity e)
  {
    Set(ItemStatus.Equipped, true);
    Class.OnEquip(this, e);
  }

  public void OnPickup(Entity holder) { Class.OnPickup(this, holder); }
  public void OnPlace(Map map, Point pt) { Class.OnPlace(this, map, pt); }

  public void OnUnequip(Entity e)
  {
    Class.OnUnequip(this, e);
    Set(ItemStatus.Equipped, false);
  }

  public Item Split(int toRemove)
  {
    if(toRemove>=Count) throw new ArgumentOutOfRangeException("toRemove", toRemove, "is >= Count");
    Item newItem = Clone();
    newItem.Count = toRemove;
    Count -= toRemove;
    return newItem;
  }

  public bool Tick(Entity holder, IInventory container)
  {
    Age++;
    return Class.Tick(this, holder, container);
  }

  public bool Use(Entity user) { return Use(user, Direction.Self); }
  public bool Use(Entity user, Direction dir) { return Class.Use(this, user, dir); }
  public bool Use(Entity user, Point pt) { return Class.Use(this, user, pt); }

  public string Named;
  public Shop Shop; // the shop that owns this item
  public int Age, Count;
  public char Char;
  public ItemStatus Status;
  public byte Damage, Charges, Recharges;
  public sbyte Enchantment;

  const int MaxDamage = 4;

  object data;
  int index;
}
#endregion

#region ItemClass
public abstract class ItemClass
{
  public ItemClass()
  {
    prefix=pluralPrefix=""; pluralSuffix="s"; Color=Color.White; Durability=Durability.NeverBreaks; RandomName=true;
    name="UNNAMED ITEM"; shortDesc="UNDESCRIBED ITEM"; Material=UnknownMaterial.Instance; Type=ItemType.Invalid;
    SpawnCount=new Range(1); BlessedChance=CursedChance=10; NumHands=1; ShowCB=true;
  }

  public string ShortDesc { get { return Identified && idShortDesc!=null ? idShortDesc : shortDesc; } }
  public string LongDesc
  {
    get
    {
      string desc = Identified && idLongDesc!=null ? idLongDesc : longDesc;
      return desc!=null ? desc : ShortDesc;
    }
  }

  public virtual bool CanStack(Item a, Item b)
  {
    return a.Index==b.Index && a.Type==b.Type && a.Status==b.Status && a.Named==b.Named &&
         (a.Type==ItemType.Food || a.Type==ItemType.Potion || a.Type==ItemType.Scroll || a.Type==ItemType.Ammo ||
          a.Type==ItemType.Weapon || a.Type==ItemType.Treasure);
  }

  public string GetAName(Item item) { return GetAName(item, false); }
  public string GetAName(Item item, bool forceSingular)
  {
    string name = GetFullName(item, forceSingular);
    return item.Count==1 || forceSingular ? Global.WithAorAn(name) : name;
  }

  public virtual string GetBaseName(Item item) { return Identified && idName!=null ? idName : name; }

  public string GetFullBaseName(Item item) { return GetFullBaseName(item, false); }
  public string GetFullBaseName(Item item, bool forceSingular)
  {
    bool plural = !forceSingular && item.Count!=1;
    string ret = plural ? (Identified && idPluralPrefix!=null ? idPluralPrefix : pluralPrefix)
                      : (Identified && idPrefix!=null ? idPrefix : prefix);
    ret += GetBaseName(item);
    if(plural) ret += (Identified && idPluralSuffix!=null ? idPluralSuffix : pluralSuffix);
    return Called==null ? ret : ret+" called "+Called;
  }

  public string GetFullName(Item item) { return GetFullName(item, false); }
  public virtual string GetFullName(Item item, bool forceSingular)
  {
    bool plural = !forceSingular && item.Count!=1;
    string ret = (plural ? item.Count.ToString() : null), status=item.StatusString;
    if(status!="") ret = ret==null ? item.StatusString : ret+" "+item.StatusString;
    ret = ret + (ret=="" ? null : " ") + GetFullBaseName(item, forceSingular);
    if(item.Is(ItemStatus.HasCharges)) ret = ret+" ("+item.Charges.ToString()+":"+item.Recharges.ToString()+")";
    return item.Named==null ? ret : ret+" named "+item.Named;
  }

  public virtual string GetInvName(Item item) { return item.GetAName(); }

  public virtual int GetWeight(Item item) { return weight; }

  // returns true if item should be destroyed
  public virtual bool Hit(Item item, Entity thrower, Point pt) { return false; }
  public virtual bool Hit(Item item, Entity thrower, Entity victim) { return false; }

  public virtual object InitializeData(Item item) { return null; }

  public virtual void InitializeItem(Item item)
  {
    if(Charges.R!=0)
    {
      item.Set(ItemStatus.HasCharges, true);
      item.Charges = (byte)Charges.RandValue();
    }
    else item.Count = SpawnCount.RandValue();

    int bc = Global.Rand(100);
    if(bc<CursedChance) item.Curse();
    else if(bc>=100-BlessedChance) item.Bless();
  }

  public virtual void InitializeItem(Item item, XmlNode node)
  {
    InitializeItem(item);

    if(!Xml.IsEmpty(node, "charges")) item.Charges = (byte)Xml.RangeInt(node, "charges");
    if(!Xml.IsEmpty(node, "count")) item.Count = Xml.RangeInt(node, "count");
    if(!Xml.IsEmpty(node, "enchant")) item.Enchantment = (sbyte)Xml.Int(node, "enchant");

    if(!Xml.IsEmpty(node, "status"))
    {
      foreach(string s in Xml.List(node, "status"))
      {
        switch(s)
        {
          case "LightlyDamaged": item.Damage = 1; break;
          case "Damaged": item.Damage = 2; break;
          case "HeavilyDamaged": item.Damage = 3; break;
          case "CriticallyDamaged": item.Damage = 4; break;
          default: item.Status |= (ItemStatus)Enum.Parse(typeof(ItemStatus), s); break;
        }
      }
    }
  }

  // returns true if item should be destroyed
  public virtual bool Invoke(Item item, Entity user)
  {
    if(App.Player.IsOrSees(user)) App.IO.Print("Nothing happens.");
    return false;
  }

  public virtual float Modify(Item item, Attr attr, float value) { return value; }
  public virtual Ability Modify(Item item, Ability flags) { return flags; }
  public virtual AIFlag Modify(Item item, AIFlag flags) { return flags; }
  public virtual Ailment Modify(Item item, Ailment flags) { return flags; }
  public virtual Intrinsic Modify(Item item, Intrinsic flags) { return flags; }

  public virtual void OnDrop(Item item, Entity holder) { }
  public virtual void OnEquip(Item item, Entity holder) { }
  public virtual void OnPickup(Item item, Entity holder) { }
  public virtual void OnPlace(Item item, Map map, Point pt) { }
  public virtual void OnUnequip(Item item, Entity holder) { }

  public virtual void Recharge(Item item)
  {
    if(Charges.R!=0)
    {
      item.Set(ItemStatus.HasCharges, true);
      item.Charges = (byte)Charges.RandValue();
      item.Recharges++;
    }
  }

  public virtual bool Tick(Item item, Entity holder, IInventory container) { return false; }

  public virtual bool Use(Item item, Entity user, Item usedOn) { throw new NotImplementedException(); }
  public virtual bool Use(Item item, Entity user, Direction dir) { throw new NotImplementedException(); }
  public virtual bool Use(Item item, Entity user, Point pt) { throw new NotImplementedException(); }

  public Material Material;
  public string Called;
  public int Index, Price, SpawnChance, NumHands;
  public Range SpawnCount, Charges;
  public Color Color;
  public ItemType Type;
  public ItemUse Usability;
  public Durability Durability;
  public byte Noise; // noise is how noisy the item is to use (0-255)
  public byte BlessedChance, CursedChance; // 0-100% chance of being generated blessed/cursed
  public bool Identified, RandomName, ShowEnchantment, ShowCB;

  public static ItemClass FromXml(XmlNode node)
  {
    switch(node.LocalName)
    {
      case "armor": return new XmlArmor(node);
      case "food": return new XmlFood(node);
      case "potion": return new XmlPotion(node);
      case "ring": return new XmlRing(node);
      case "scroll": return new XmlScroll(node);
      case "shield": return new XmlShield(node);
      case "spellbook": return new XmlSpellbook(node);
      case "tool": return new XmlTool(node);
      case "treasure": return new XmlTreasure(node);
      case "wand": return new XmlWand(node);
      case "weapon": return new XmlWeapon(node);
      default: throw new NotImplementedException("unknown xml item type: "+node.LocalName);
    }
  }

  public static string GetVerbS(Item item) { return item.Count==1 ? "s" : ""; }

  protected string name, idName, prefix, pluralPrefix, pluralSuffix, idPrefix, idPluralPrefix, idPluralSuffix,
                   shortDesc, longDesc, idShortDesc, idLongDesc;
  protected int weight; // in grams

  protected static void Init(ItemClass ic, XmlNode node)
  {
    ic.SpawnChance = Xml.Int(node, "chance", 1000000); // default to a big number so we hopefully notice if we forget to specify it

    ic.name = Xml.Attr(node, "name");
    if(!Xml.IsEmpty(node, "idName")) ic.idName = Xml.Attr(node, "idName");

    if(!Xml.IsEmpty(node, "prefix")) ic.prefix = Xml.Attr(node, "prefix");
    if(!Xml.IsEmpty(node, "pluralPrefix")) ic.pluralPrefix = Xml.Attr(node, "pluralPrefix");
    if(!Xml.IsEmpty(node, "pluralSuffix")) ic.pluralSuffix = Xml.Attr(node, "pluralSuffix");
    if(!Xml.IsEmpty(node, "shortDesc")) ic.shortDesc = Xml.Attr(node, "shortDesc");
    if(!Xml.IsEmpty(node, "longDesc")) ic.longDesc = Xml.Attr(node, "longDesc");
    if(!Xml.IsEmpty(node, "color")) ic.Color = Xml.Color(node, "color");
    if(!Xml.IsEmpty(node, "weight")) ic.weight = Xml.Weight(node, "weight");
    if(!Xml.IsEmpty(node, "material")) ic.Material = Material.Get(Xml.Attr(node, "material"));
    if(!Xml.IsEmpty(node, "price")) ic.Price = Xml.RangeInt(node, "price");

    if(!Xml.IsEmpty(node, "idPrefix")) ic.idPrefix = Xml.Attr(node, "idPrefix");
    if(!Xml.IsEmpty(node, "idPluralPrefix")) ic.idPluralPrefix = Xml.Attr(node, "idPluralPrefix");
    if(!Xml.IsEmpty(node, "idPluralSuffix")) ic.idPluralSuffix = Xml.Attr(node, "idPluralSuffix");
    if(!Xml.IsEmpty(node, "idShortDesc")) ic.idShortDesc = Xml.Attr(node, "idShortDesc");
    if(!Xml.IsEmpty(node, "idLongDesc")) ic.idLongDesc = Xml.Attr(node, "idLongDesc");

    XmlNode child = node.SelectSingleNode("longDesc");
    if(child!=null) ic.longDesc = Xml.BlockToString(child.InnerText);

    child = node.SelectSingleNode("idLongDesc");
    if(child!=null) ic.idLongDesc = Xml.BlockToString(child.InnerText);
  }

  protected static void InitWearable(Wearable ic, XmlNode node)
  {
    Init(ic, node);

    string slot = Xml.Attr(node, "slot");
    if(!Xml.IsEmpty(slot)) ic.Slot = (Slot)Enum.Parse(typeof(Slot), slot);
  }
}
#endregion

#region Wearable
public abstract class Wearable : ItemClass
{
  protected Wearable() { Slot=Slot.Invalid; }

  public override string GetInvName(Item item) { return GetAName(item) + (IsWorn(item) ? " (worn)" : null); }

  public override object InitializeData(Item item) { return false; }
  public bool IsWorn(Item item) { return (bool)item.Data; }

  public override void OnPlace(Item item, Map map, Point pt)
  {
    base.OnPlace(item, map, pt);
    item.Data = false;
  }

  public virtual void OnRemove(Item item, Entity equipper) { item.Data = false; }
  public virtual void OnWear(Item item, Entity equipper) { item.Data = true; }

  public Slot Slot;
}
#endregion

#region Gold
public sealed class Gold : ItemClass
{
  public Gold()
  {
    name="gold piece"; Material=GoldMaterial.Instance; Color=Color.Yellow; Type=ItemType.Gold; Price=1; ShowCB=false;
  }

  public override bool CanStack(Item a, Item b) { return b.Type==ItemType.Gold; }
}
#endregion

} // namespace Chrono