using System;
using System.Xml;

namespace Chrono
{

// carnivores eat meat, herbivores eat plants
public enum FoodType : byte { Meat, Plant, Other }

#region Food
public abstract class Food : ItemClass
{
  public Food() { Type=ItemType.Food; eatTime=1; FoodType=FoodType.Other; }

  public int EatTime { get { return eatTime; } }
  public int Nutrition { get { return nutrition; } }

  public override bool CanStack(Item a, Item b)
  {
    if(!base.CanStack(a, b) || a.Age!=b.Age) return false;
    FoodData ad=(FoodData)a.Data, bd=(FoodData)b.Data;
    return ad==bd || ad!=null && bd!=null && ad.Equals(bd);
  }

  public override string GetBaseName(Item item)
  {
    string name = base.GetBaseName(item);
    FoodData fd = (FoodData)item.Data;
    if(fd!=null && fd.Nutrition<GetNutrition(item)) name = "partially eaten ";
    if(fd!=null && fd.Rotting) name = "rotting "+name;
    return name;
  }

  public virtual int GetEatTime(Item item)
  {
    FoodData fd = (FoodData)item.Data;
    return fd==null ? eatTime : (eatTime*fd.Nutrition+nutrition-1)/nutrition;
  }

  public virtual int GetNutrition(Item item)
  {
    FoodData fd = (FoodData)item.Data;
    return fd==null ? nutrition : fd.Nutrition;
  }

  public override int GetWeight(Item item)
  {
    int weight=base.GetWeight(item), nutrition=GetNutrition(item);
    FoodData fd = (FoodData)item.Data;
    return fd==null ? weight : (weight*fd.Nutrition+nutrition-1)/nutrition;
  }

  public virtual void OnEat(Item item, Entity eater) { }

  // returns amount of nutrition the user should get from the chunk
  public virtual int RemoveChunk(Item item)
  {
    FoodData fd = GetFoodData(item);
    int eaten = Math.Min(fd.Nutrition, GetNutrition(item)/GetEatTime(item));
    fd.Nutrition -= eaten;

    if(fd.Tainted) eaten = 0;
    else if(fd.Rotting) eaten = (eaten+1)/2;

    return eaten;
  }

  public override bool Tick(Item item, Entity holder, IInventory container)
  {
    base.Tick(item, holder, container);

    FoodData fd = (FoodData)item.Data;
    int decay = fd==null ? DecayTime : fd.DecayTime;
    if(decay!=0)
    {
      if(item.Age>=DecayTime*2)
      {
        if(holder==App.Player) App.IO.Print("Your {0} rot{1} away.", GetFullName(item), GetVerbS(item));
        return true;
      }
      else if(item.Age>=DecayTime && (fd==null || !fd.Rotting)) Rot(item, holder);
    }
    return false;
  }

  public int DecayTime;
  public FoodType FoodType;

  protected FoodData GetFoodData(Item item)
  {
    FoodData fd = (FoodData)item.Data;
    if(fd==null) item.Data = fd = new FoodData(this, item);
    return fd;
  }

  protected void Rot(Item item, Entity holder)
  {
    FoodData fd = GetFoodData(item);
    fd.Rotting = true;
    if(holder==App.Player && Global.Rand(100)<holder.GetAttr(Attr.Smell))
    {
      if(holder.HasAilment(Ailment.Hallucinating))
        App.IO.Print(Global.Coinflip() ? "Ugh! Raunchy!" : "Like, gag me with a spoon!");
      else App.IO.Print(Global.Coinflip() ? "Eww! There's something really disgusting in your pack!"
                                        : "You smell the putrid stench of decay.");
    }
  }

  protected int eatTime, nutrition;
}
#endregion

#region FoodData
public sealed class FoodData
{
  public FoodData(Food food) { DecayTime=food.DecayTime; }
  public FoodData(Food food, Item item) { DecayTime=food.DecayTime; Nutrition=food.GetNutrition(item); }

  public bool Equals(FoodData o)
  {
    return o!=null && DecayTime==o.DecayTime && Nutrition==o.Nutrition && Rotting==o.Rotting && Tainted==o.Tainted;
  }

  public EntityClass Source;
  public int DecayTime, Nutrition;
  public bool Rotting, Tainted;
}
#endregion

#region XmlFood
public sealed class XmlFood : Food
{
  public XmlFood(XmlNode node)
  {
    ItemClass.Init(this, node);
    if(!Xml.IsEmpty(node, "decayTime")) DecayTime = Xml.Int(node, "decayTime");
    if(!Xml.IsEmpty(node, "eatTime")) eatTime = Xml.Int(node, "eatTime");
    nutrition = Xml.Int(node, "nutrition");
  }
}
#endregion

#region Corpse
public sealed class Corpse : Food
{
  public Corpse() { DecayTime=75; FoodType=FoodType.Meat; name="corpse"; }

  public EntityClass GetEntity(Item item) { return (EntityClass)((FoodData)item.Data).Source; }

  public override int GetEatTime(Item item)
  {
    switch(GetEntity(item).Size)
    {
      case EntitySize.Tiny: return 1;
      case EntitySize.Small: return 2;
      case EntitySize.Medium: return 4;
      case EntitySize.Large: return 8;
      case EntitySize.Huge: return 12;
      case EntitySize.Gigantic: return 16;
      default: throw new NotSupportedException();
    }
  }

  public override int GetNutrition(Item item) { return GetEntity(item).Nutrition; }

  public static Item Make(Entity deadguy) // TODO: taint the corpse if the entity died of poison
  {
    Item corpse  = new Item("builtin/Corpse");
    FoodData fd  = new FoodData((Food)corpse.Class);
    fd.Source    = deadguy.Class;
    fd.Nutrition = deadguy.Class.Nutrition;
    corpse.Data  = fd;
    return corpse;
  }
}
#endregion

#region FortuneCookie
public class FortuneCookie : Food
{
  public FortuneCookie()
  {
    name="fortune cookie"; Color=Color.Brown; weight=20; Price=2; nutrition=40; SpawnChance=55;
  }

  public override void OnEat(Item item, Entity eater)
  {
    if(eater==App.Player)
      App.IO.Print("The fortune cookie says: {0}",
                   "A starship ride has been promised to you by the galactic wizard.");
  }
}
#endregion

} // namespace Chrono