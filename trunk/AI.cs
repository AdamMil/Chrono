using System;
using System.Drawing;

namespace Chrono
{

public abstract class AI : Entity
{ public override void Die(Entity killer, Item impl) { Die(); }
  public override void Die(Death cause) { Die(); }

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

  protected virtual void Die()
  { if(Global.Rand(100)<30) Map.AddItem(Position, new Corpse(this)); // 30% chance of leaving a corpse
    for(int i=0; i<Inv.Count; i++) Map.AddItem(Position, Inv[i]);
    Map.Entities.Remove(this);
  }

  public bool HasEyes=true, HasEars=true, HasNose=true;
  
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