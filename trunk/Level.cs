using System;
using System.Collections;
using System.Drawing;

namespace Chrono
{

public sealed class Level
{ 
  #region CreatureCollection
  public class CreatureCollection : ArrayList
  { public CreatureCollection(Level level) { this.level = level; }
  
    public new Creature this[int index] { get { return (Creature)base[index]; } }

    public new void Add(object o) { Add((Creature)o); }
    public void Add(Creature c)
    { base.Add(c);
      c.Level = level;
    }
    public new void AddRange(ICollection creatures)
    { base.AddRange(creatures);
      foreach(Creature c in creatures) level.Added(c);
    }
    public new void Insert(int index, object o) { Insert(index, (Creature)o); }
    public void Insert(int index, Creature c)
    { base.Insert(index, c);
      level.Added(c);
    }
    public void InsertRange(ICollection creatures, int index)
    { base.InsertRange(index, creatures);
      foreach(Creature c in creatures) level.Added(c);
    }
    public new void Remove(object o) { Remove((Creature)o); }
    public void Remove(Creature c)
    { base.Remove(c);
      level.Removed(c);
    }
    public new void RemoveAt(int index)
    { Creature c = this[index];
      base.RemoveAt(index);
      level.Removed(c);
    }
    public new void RemoveRange(int index, int count)
    { if(index<0 || index>=Count || count<0 || index+count>Count)
        throw new ArgumentOutOfRangeException();
      for(int i=0; i<count; i++) level.Removed(this[index+i]);
      base.RemoveRange(index, count);
    }

    protected Level level;
  }
  #endregion

  public Level(int width, int height)
  { map       = new Map(width, height);
    creatures = new CreatureCollection(this);
  }

  public int Width  { get { return map.Width; } }
  public int Height { get { return map.Height; } }
  public Map Map    { get { return map; } }
  
  public CreatureCollection Creatures { get { return creatures; } }

  public void Simulate()
  { thinking=true;
    foreach(Creature c in creatures) thinkQueue.Enqueue(c);
    while(thinkQueue.Count!=0)
    { Creature c = (Creature)thinkQueue.Dequeue();
      if(removedCreatures.Contains(c)) continue;
      c.Think();
    }
    thinking=false;
    removedCreatures.Clear();
  }
  
  void Added(Creature c)
  { c.Level=this;
    if(thinking) thinkQueue.Enqueue(c);
  }
  void Removed(Creature c)
  { c.Level=null;
    if(thinking) removedCreatures[c]=true;
  }

  Map  map;
  CreatureCollection creatures;
  Queue thinkQueue = new Queue();
  Hashtable removedCreatures = new Hashtable();
  bool thinking;
}

} // namespace Chrono