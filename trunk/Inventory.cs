using System;
using System.Collections;

namespace Chrono
{

public sealed class Inventory : ICollection
{ public Inventory() : this(52) { }
  public Inventory(int maxItems)
  { if(maxItems<1 || maxItems>52) throw new ArgumentOutOfRangeException("maxItems");
    this.maxItems = maxItems;
  }

  public Item this[int i]  { get { return (Item)items.GetByIndex(i); } }
  public Item this[char c] { get { return items==null ? null : (Item)items[c]; } }

  public int  Count  { get { return items==null ? 0 : items.Count; } }
  public bool IsFull { get { return Count>=maxItems; } }

  public Item Add(Item item)
  { if(IsFull) throw new InvalidOperationException("This inventory is full!");
    if(items==null) items = new SortedList();
    foreach(Item i in items.Values)
      if(item.CanStackWith(i)) { i.Count += item.Count; return i; }
    if(item.Char==0 || this[item.Char]!=null)
    { for(char c='a'; c<='z'; c++) if(items[c]==null) { item.Char=c; goto done; }
      for(char c='A'; c<='A'; c++) if(items[c]==null) { item.Char=c; break; }
    }
    done: items[item.Char] = item;
    return item;
  }

  public Inventory Clone()
  { Inventory ret = new Inventory(maxItems);
    ret.items = (SortedList)items.Clone();
    return ret;
  }

  public string CharString()
  { string ret=string.Empty;
    foreach(Item i in items.Values) ret += i.Char;
    return ret;
  }

  public string CharString(ItemClass itemClass)
  { string ret=string.Empty;
    foreach(Item i in items.Values) if(i.Class==itemClass) ret += i.Char;
    return ret;
  }

  public void Remove(Item item) { items.RemoveAt(items.IndexOfValue(item)); }
  public void RemoveAt(int index) { items.RemoveAt(index); }

  #region IEnumerable members
  public IEnumerator GetEnumerator()
  { return items==null ? (IEnumerator)new EmptyEnumerator() : items.Values.GetEnumerator();
  }
  #endregion

  #region ICollection Members
  public bool IsSynchronized { get { return false; } }
  public void CopyTo(Array array, int index) { if(items!=null) items.Values.CopyTo(array, index); }
  public object SyncRoot { get { return this; } }
  #endregion

  private SortedList items;
  private int maxItems;
}

} // namespace Chrono