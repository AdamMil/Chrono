using System;
using System.Collections;

namespace Chrono
{

public interface IInventory : ICollection
{ Item this[int index] { get; }
  bool IsFull { get; }
  int Weight { get; }

  Item Add(Item item);
  void Clear();
  bool Contains(Item item);
  Item[] GetItems(params ItemType[] types);
  bool Has(params ItemType[] ItemType);
  void Remove(Item item);
  void RemoveAt(int index);
}

public interface IKeyedInventory : IInventory
{ Item this[char c] { get; }
  string CharString(params ItemType[] types);
  void Remove(char c);
}

#region ItemPile
public sealed class ItemPile : IInventory
{ public ItemPile Clone() // will only be called if there are items
  { ItemPile ret = new ItemPile();
    ret.items = new ArrayList(items.Count);
    foreach(Item i in items) ret.items.Add(i.Clone());
    return ret;
  }

  #region IInventory Members
  public Item this[int i] { get { return (Item)items[i]; } }
  public bool IsFull { get { return false; } }

  public int Weight
  { get
    { int weight=0;
      if(items!=null) foreach(Item i in items) weight += i.FullWeight;
      return weight;
    }
  }

  public Item Add(Item item)
  { if(items==null) items = new ArrayList();
    else 
      for(int i=0; i<items.Count; i++)
        if(item.CanStackWith(this[i])) { this[i].Count += item.Count; return this[i]; }
    items.Add(item);
    return item;
  }

  public void Clear() { if(items!=null) items.Clear(); }

  public bool Contains(Item item) { return items!=null && items.Contains(item); }

  public Item[] GetItems(params Chrono.ItemType[] types)
  { if(items==null || items.Count==0) return new Item[0];
    if(Array.IndexOf(types, ItemType.Any)!=-1) return (Item[])items.ToArray(typeof(Item));
    ArrayList list = new ArrayList();
    for(int i=0; i<items.Count; i++) if(Array.IndexOf(types, this[i].Type)!=-1) list.Add(items[i]);
    return (Item[])list.ToArray(typeof(Item));
  }

  public bool Has(params ItemType[] types)
  { if(items==null) return false;
    for(int i=0; i<items.Count; i++) if(Array.IndexOf(types, this[i].Type)!=-1) return true;
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
  public IEnumerator GetEnumerator() { return items==null ? EmptyEnumerator.Instance : items.GetEnumerator(); }
  #endregion

  ArrayList items;
}
#endregion

#region Inventory
public sealed class Inventory : IKeyedInventory
{ public Item this[int i]  { get { return (Item)items.GetByIndex(i); } }

  public Item this[char c]
  { get
    { if(items==null) return null;
      Item it = (Item)items[c];
      if(c=='$' && it==null)
        foreach(Item i in items.Values) if(i.Type==ItemType.Gold) return i;
      return it;
    }
  }

  public int  Count  { get { return items==null ? 0 : items.Count; } }
  public bool IsFull { get { return Count>=52; } }

  public int Weight
  { get
    { int weight=0;
      if(items!=null) foreach(Item i in items.Values) weight += i.FullWeight;
      return weight;
    }
  }

  public Item Add(Item item)
  { if(items==null) items = new SortedList();
    foreach(Item i in items.Values)
      if(item.CanStackWith(i)) { i.Count += item.Count; return i; }
    if(IsFull) return null;
    if(!char.IsLetter(item.Char) || this[item.Char]!=null)
    { for(char c='a'; c<='z'; c++) if(items[c]==null) { item.Char=c; goto done; }
      for(char c='A'; c<='Z'; c++) if(items[c]==null) { item.Char=c; goto done; }
      return null;
    }
    done: items[item.Char] = item;
    return item;
  }

  public string CharString(params ItemType[] types)
  { Item[] items = GetItems(types);
    if(items.Length==0) return "";

    System.Text.StringBuilder sb = new System.Text.StringBuilder(42, 42);
    Array.Sort(items, ItemComparer.ByCharGoldFirst); // sort items by letter

    int i;
    if(items[0].Type==ItemType.Gold) { sb.Append('$'); i=1; }
    else i = 0;

    for(; i<items.Length; i++)
    { int run = 1;
      char c = items[i].Char;
      sb.Append(c);
      for(int j=i+1; j<items.Length && items[j].Char==++c; j++) run++;
      if(run!=1)
      { c = items[i].Char;
        if(run>3) { sb.Append('-'); sb.Append((char)(c+run)); }
        else if(run==3) { sb.Append((char)(c+1)); sb.Append((char)(c+2)); }
        else if(run==2) sb.Append((char)(c+1));
        i += run-1;
      }
    }

    return sb.ToString();
  }

  public void Clear() { if(items!=null) items.Clear(); }

  public bool Contains(Item item) { return items!=null && items.ContainsValue(item); }

  public Item[] GetItems(params ItemType[] types)
  { if(items==null || items.Count==0) return new Item[0];

    if(Array.IndexOf(types, ItemType.Any)!=-1)
    { Item[] ret = new Item[Count];
      items.Values.CopyTo(ret, 0);
      return ret;
    }
    else
    { ArrayList list = new ArrayList();
      foreach(Item i in items.Values) if(Array.IndexOf(types, i.Type)!=-1) list.Add(i);
      return (Item[])list.ToArray(typeof(Item));
    }
  }

  public bool Has(params ItemType[] types)
  { if(items==null) return false;
    for(int i=0; i<items.Count; i++) if(Array.IndexOf(types, this[i].Type)!=-1) return true;
    return false;
  }

  public void Remove(char c) { items.Remove(c); }
  public void Remove(Item item) { items.RemoveAt(items.IndexOfValue(item)); }
  public void RemoveAt(int index) { items.RemoveAt(index); }

  #region IEnumerable members
  public IEnumerator GetEnumerator()
  { return items==null ? (IEnumerator)EmptyEnumerator.Instance : items.Values.GetEnumerator();
  }
  #endregion

  #region ICollection Members
  public bool IsSynchronized { get { return false; } }
  public void CopyTo(Array array, int index) { if(items!=null) items.Values.CopyTo(array, index); }
  public object SyncRoot { get { return this; } }
  #endregion

  private SortedList items;
}
#endregion

} // namespace Chrono