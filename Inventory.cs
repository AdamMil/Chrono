using System;
using System.Collections.Generic;

namespace Chrono
{

public interface IInventory : ICollection<Item>
{
  Item this[int index] { get; }
  bool IsFull { get; }
  int Weight { get; }

  new Item Add(Item item);
  bool Contains(params ItemType[] ItemType);
  Item[] GetItems(params ItemType[] types);
  void RemoveAt(int index);
}

public interface IKeyedInventory : IInventory
{
  Item this[char c] { get; }
  string CharString(params ItemType[] types);
  bool Remove(char c);
}

#region ItemPile
public sealed class ItemPile : IInventory
{
  public ItemPile Clone() // will only be called if there are items
  {
    ItemPile ret = new ItemPile();
    ret.items = new List<Item>(items.Count);
    foreach(Item i in items) ret.items.Add(i.Clone());
    return ret;
  }

  #region IInventory Members
  public Item this[int i] 
  {
    get { return items[i]; } 
  }

  public bool IsFull 
  {
    get { return false; } // item piles can hold an unlimited number of items
  }

  public int Weight
  {
    get
    {
      int weight = 0;
      if(items != null)
      {
        foreach(Item i in items) weight += i.FullWeight;
      }
      return weight;
    }
  }

  public Item Add(Item item)
  {
    if(items == null) items = new List<Item>();
    else
    {
      for(int i=0; i<items.Count; i++)
      {
        if(item.CanStackWith(this[i]))
        {
          this[i].Count += item.Count;
          return this[i];
        }
      }
    }

    items.Add(item);
    return item;
  }

  public void Clear()
  {
    if(items != null) items.Clear();
  }

  public bool Contains(Item item) 
  {
    return items!=null && items.Contains(item); 
  }

  public Item[] GetItems(params Chrono.ItemType[] types)
  {
    if(items == null || items.Count == 0) return new Item[0];
    if(Array.IndexOf(types, ItemType.Any) != -1) return items.ToArray();

    List<Item> list = new List<Item>();
    for(int i=0; i<items.Count; i++)
    {
      if(Array.IndexOf(types, this[i].Type) != -1) list.Add(items[i]);
    }
    return list.ToArray();
  }

  public bool Contains(params ItemType[] types)
  {
    if(items == null) return false;
    for(int i=0; i<items.Count; i++)
    {
      if(Array.IndexOf(types, this[i].Type) != -1) return true;
    }
    return false;
  }

  public bool Remove(Item item) { return items.Remove(item); }
  public void RemoveAt(int index) { items.RemoveAt(index); }
  #endregion

  #region ICollection Members
  bool ICollection<Item>.IsReadOnly
  {
    get { return false; }
  }

  public int Count
  {
    get { return items==null ? 0 : items.Count; } 
  }

  public object SyncRoot
  {
    get { return this; }
  }

  void ICollection<Item>.Add(Item item)
  {
    Add(item);
  }

  public void CopyTo(Item[] array, int index) 
  { 
    if(items != null) items.CopyTo(array, index);
  }
  #endregion

  #region IEnumerable Members
  public IEnumerator<Item> GetEnumerator() 
  {
    return items==null ? (IEnumerator<Item>)EmptyEnumerator<Item>.Instance : items.GetEnumerator(); 
  }

  System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }
  #endregion

  List<Item> items;
}
#endregion

#region Inventory
public sealed class Inventory : IKeyedInventory
{
  public Item this[int i]
  { 
    get { return items.Values[i]; } 
  }

  public Item this[char c]
  {
    get
    {
      if(items == null) return null;
      Item item;
      items.TryGetValue(c, out item);
      return item;
    }
  }

  public int Count 
  {
    get { return items == null ? 0 : items.Count; } 
  }

  public bool IsFull 
  {
    // inventories can always hold some gold, which is always assigned the character '$'
    get { return Count == 53 || Count == 52 && items.ContainsKey('$'); } 
  }

  public int Weight
  {
    get
    {
      int weight = 0;
      if(items != null)
      {
        foreach(Item i in items.Values) weight += i.FullWeight;
      }
      return weight;
    }
  }

  public Item Add(Item item)
  {
    if(items == null) items = new SortedList<char,Item>();
    
    foreach(Item i in items.Values)
    {
      if(item.CanStackWith(i))
      {
        i.Count += item.Count;
        return i;
      }
    }

    if(IsFull) return null;

    if(item.Type == ItemType.Gold) // if the item is gold, it always gets the character '$'
    {
      item.Char = '$';
    }
    else if(!char.IsLetter(item.Char) || items.ContainsKey(item.Char)) // otherwise, assign it a new character if
    {                                                                  // necessary
      for(char c='a'; c<='z'; c++) if(!items.ContainsKey(c)) { item.Char=c; goto done; }
      for(char c='A'; c<='Z'; c++) if(!items.ContainsKey(c)) { item.Char=c; goto done; }
      throw new Exception("This shouldn't happen.");
    }

    done:
    items.Add(item.Char, item);
    return item;
  }

  public string CharString(params ItemType[] types)
  {
    Item[] items = GetItems(types);
    if(items.Length == 0) return "";

    Array.Sort(items, ItemComparer.ByCharGoldFirst); // sort items by letter, but put gold first

    // the longest string output by this algorithm should be 41 characters -- 1 for gold and 20 for each of the upper
    // and lowercase letters. each alphabet is 26 characters, and the longest output is given by having no runs
    // collapsed, which means 3 contiguous characters plus a character skipped, or ceil(N*3/4) where N is the alphabet
    // size. for an N of 26, this is 20 characters -- "abcefgijkmnoqrsuvwyz"
    System.Text.StringBuilder sb = new System.Text.StringBuilder(41, 41);
    for(int i=0; i<items.Length; i++)
    {
      // add the current item
      int run = 1;
      char c  = items[i].Char;
      sb.Append(c);

      // then see how many items have contiguous characters (eg, abcdefg is a run of 7 items)
      for(int j=i+1; j<items.Length && items[j].Char == ++c; j++) run++;

      if(run > 1)
      {
        // runs are always output as two or three characters. we already have the first character. now add the middle.
        if(run > 3) // for more than 3 items, represent them as a range (eg, "a-f")
        {
          sb.Append('-');
        }
        else if(run == 3) // for 3 items, we there's no point in using "a-c" rather than "abc", so just use "abc"
        {
          sb.Append((char)(c+1)); // output the second character
        }
        i += run-1;
        sb.Append((char)(c+i)); // output the last character in the run
      }
    }

    return sb.ToString();
  }

  public void Clear()
  {
    if(items != null) items.Clear(); 
  }

  public bool Contains(Item item)
  {
    return items != null && items.ContainsValue(item); 
  }

  public Item[] GetItems(params ItemType[] types)
  {
    if(items == null || items.Count == 0) return new Item[0];

    if(Array.IndexOf(types, ItemType.Any)!=-1)
    {
      Item[] ret = new Item[Count];
      CopyTo(ret, 0);
      return ret;
    }
    else
    {
      List<Item> list = new List<Item>();
      foreach(Item i in items.Values)
      {
        if(Array.IndexOf(types, i.Type) != -1) list.Add(i);
      }
      return list.ToArray();
    }
  }

  public bool Contains(params ItemType[] types)
  {
    if(items == null) return false;
    for(int i=0; i<items.Count; i++)
    {
      if(Array.IndexOf(types, this[i].Type) != -1) return true;
    }
    return false;
  }

  public bool Remove(char c) { return items.Remove(c); }

  public bool Remove(Item item)
  {
    int index = items.IndexOfValue(item);
    if(index == -1) return false;
    items.RemoveAt(index);
    return true;
  }
  
  public void RemoveAt(int index) { items.RemoveAt(index); }

  #region IEnumerable members
  public IEnumerator<Item> GetEnumerator()
  {
    return items == null ? EmptyEnumerator<Item>.Instance : items.Values.GetEnumerator();
  }

  System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }
  #endregion

  #region ICollection Members
  bool ICollection<Item>.IsReadOnly
  {
    get { throw new Exception("The method or operation is not implemented."); }
  }

  void ICollection<Item>.Add(Item item)
  {
    Add(item);
  }

  public void CopyTo(Item[] array, int index) 
  {
    if(items != null) items.Values.CopyTo(array, index); 
  }

  public object SyncRoot 
  {
    get { return this; } 
  }
  #endregion

  private SortedList<char,Item> items;
}
#endregion

} // namespace Chrono