using System;
using System.Collections;

namespace Chrono
{

public interface IInventory : ICollection
{ Item this[int index] { get; }
  bool IsFull { get; }
  
  Item Add(Item item);
  
  Item[] GetItems(ItemClass itemClass);
  
  bool Has(ItemClass itemClass);
  
  void Remove(Item item);
  void RemoveAt(int index);
}

public interface IKeyedInventory : IInventory
{ Item this[char c] { get; }
  string CharString();
  string CharString(ItemClass itemClass);
  string CharString(ItemClass[] classes);
  
  void Remove(char c);
}

#region ItemPile
public sealed class ItemPile : IInventory
{
  public ItemPile Clone() // will only be called if there are items
  { ItemPile ret = new ItemPile();
    ret.items = (ArrayList)items.Clone();
    return ret;
  }

  #region IInventory Members
  public Item this[int i] { get { return (Item)items[i]; } }
  public bool IsFull { get { return false; } }
  public Item Add(Item item)
  { if(items==null) items = new ArrayList();
    else 
      for(int i=0; i<items.Count; i++)
        if(item.CanStackWith(this[i])) { this[i].Count += item.Count; return this[i]; }
    items.Add(item);
    return item;
  }

  public Item[] GetItems(Chrono.ItemClass itemClass)
  { if(items==null || items.Count==0) return new Item[0];
    int count=0, mi=0;
    for(int i=0; i<items.Count; i++) if(this[i].Class==itemClass) count++;
    Item[] ret = new Item[count];
    for(int i=0; i<items.Count; i++)
      if(this[i].Class==itemClass)
      { ret[mi++] = this[i];
        if(mi==count) break;
      }
    return ret;
  }
  
  public bool Has(ItemClass itemClass)
  { for(int i=0; i<items.Count; i++) if(this[i].Class==itemClass) return true;
    return false;
  }

  public void Remove(Item item) { items.Remove(item); }
  public void RemoveAt(int index) { items.RemoveAt(index); }
  #endregion

  #region ICollection Members
  public bool IsSynchronized { get { return items==null ? false : items.IsSynchronized; } }
  public int Count { get { return items==null ? 0 : items.Count; } }
  public void CopyTo(Array array, int index) { if(items!=null) items.CopyTo(array, index); }
  public object SyncRoot { get { return this; } }
  #endregion

  #region IEnumerable Members
  public IEnumerator GetEnumerator() { return items==null ? new EmptyEnumerator() : items.GetEnumerator(); }
  #endregion

  ArrayList items;
}
#endregion

#region Inventory
public sealed class Inventory : IKeyedInventory
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

  public string CharString() { return CharString(ItemClass.Any); }
  public string CharString(ItemClass itemClass)
  { string ret=string.Empty;
    if(items!=null) foreach(Item i in items.Values) if(itemClass==ItemClass.Any || i.Class==itemClass) ret += i.Char;
    return ret;
  }
  public string CharString(ItemClass[] classes)
  { string ret=string.Empty;
    for(int i=0; i<classes.Length; i++) ret += CharString(classes[i]);
    return ret;
  }

  public Item[] GetItems(ItemClass itemClass)
  { if(items==null || items.Count==0) return new Item[0];
    Item[] ret;
    if(itemClass==ItemClass.Any)
    { ret = new Item[Count];
      items.Values.CopyTo(ret, 0);
      return ret;
    }
    else
    { int count=0, mi=0;
      foreach(Item i in items.Values) if(i.Class==itemClass) count++;
      ret = new Item[count];
      foreach(Item i in items.Values)
        if(i.Class==itemClass)
        { ret[mi++]=i;
          if(mi==count) break;
        }
    }
    return ret;
  }

  public bool Has(ItemClass itemClass)
  { for(int i=0; i<items.Count; i++) if(this[i].Class==itemClass) return true;
    return false;
  }

  public void Remove(char c) { items.Remove(c); }
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
#endregion

} // namespace Chrono