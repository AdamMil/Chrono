using System;
using System.Drawing;

namespace Chrono
{

public enum AIState { Idle, Alerted };

public abstract class AI : Entity
{ public AIState State { get { return state; } }

  public override void Die(Entity killer, Item impl) { Die(); }
  public override void Die(Death cause)
  { if((cause==Death.Sickness || cause==Death.Starvation) && App.Player.CanSee(this))
      App.IO.Print(TheName+" falls to the ground, dead.");
    Die();
  }

  public override void Generate(int level, EntityClass myClass)
  { base.Generate(level, myClass);
    if(--level==0) return;
    Skill[] skills=null;

    switch(myClass) // first do special skills
    { case EntityClass.Fighter:
        skills = new Skill[] { Skill.Fighting, Skill.Armor, Skill.Dodge, Skill.Shields }; break;
    }
    AddSkills(100*level, skills);

    if(myClass==EntityClass.Fighter) // then do more general skills
    { skills = new Skill[(int)WeaponClass.NumClasses];
      for(int i=0; i<skills.Length; i++) skills[i] = (Skill)i; // first N weapon classes are mapped directly to skills
    }
    else skills=null;
    AddSkills(300*level, skills);
  }
  
  public override void OnDrink(Potion potion)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} drinks {1}.", TheName, potion.FullName);
  }
  public override void OnDrop(Item item) { if(App.Player.CanSee(this)) App.IO.Print("{0} drops {1}.", TheName, item); }
  public override void OnEquip(Wieldable item)
  { if(App.Player.CanSee(this)) App.IO.Print("{0}{1}.", TheName, item.FullName);
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
  public override void OnPickup(Item item)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} picks up {1}.", TheName, item.FullName);
  }
  public override void OnReadScroll(Scroll scroll)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} reads {1}.", TheName, scroll.FullName);
  }
  public override void OnRemove(Wearable item)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} removes {1}.", TheName, item.FullName);
  }
  public override void OnSick(string howSick)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} looks {1}.", TheName, howSick);
  }
  public override void OnSkillUp(Skill skill)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} looks more experienced.", TheName);
  }
  public override void OnUnequip(Wieldable item)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} unequips {1}.", TheName, item.FullName);
  }
  public override void OnWear(Wearable item)
  { if(App.Player.CanSee(this)) App.IO.Print("{0} puts on {1}.", TheName, item.FullName);
  }

  public bool HasEyes=true, HasEars=true, HasNose=true;

  protected virtual void Die()
  { if(Global.Rand(100)<30) Map.AddItem(Position, new Corpse(this)); // 30% chance of leaving a corpse
    for(int i=0; i<Inv.Count; i++) Map.AddItem(Position, Inv[i]);
    Map.Entities.Remove(this);
  }

  protected AIState state;

  void AddSkills(int points, Skill[] skills)
  { int[] skillTable = RaceSkills[(int)Race];
    int add=points/40, min=int.MaxValue, max=0, range;

    for(int i=0; i<skills.Length; i++)
    { int val = skillTable[(int)skills[i]];
      if(val<min) min=val; if(val>max) max=val;
    }
    range = max*2-min;

    do
      for(int i=0; i<skills.Length; i++)
      { int si=(int)skills[i], need = skillTable[si];
        if(Global.Rand(range)>need)
        { points -= add;
          if((SkillExp[si]+=add) >= need)
          { SkillExp[si] -= need;
            Skills[si]++;
          }
        }
        if(points<=0) break;
      }
    while(points>0);
  }
}

} // namespace Chrono