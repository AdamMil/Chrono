using System;
using System.Collections;

namespace Chrono
{

public struct InventoryItem
{ public Item Item;
  public char Char;
}

public sealed class Inventory : IEnumerable
{ public Inventory() : this(52) { }
  public Inventory(int maxItems)
  { if(maxItems<1 || maxItems>52) throw new ArgumentOutOfRangeException("maxItems");
    this.maxItems = maxItems;
  }

  public InventoryItem this[int i]  { get { return (InventoryItem)items[i]; } }
  public Item this[char c]
  { get
    { foreach(InventoryItem i in items) if(i.Char==c) return i.Item;
      return null;
    }
  }

  public int  Count   { get { return items==null ? 0 : items.Count; } }
  public bool IsFull  { get { return Count>=maxItems; } }
  public bool HasRoom { get { return Count<maxItems; } }

  #region IEnumerable members
  public IEnumerator GetEnumerator()
  { return items==null ? (IEnumerator)new EmptyEnumerator() : items.GetEnumerator();
  }
  #endregion

  private SortedList items;
  private int maxItems;
}

} // namespace Chrono