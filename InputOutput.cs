using System;
using System.Collections.Generic;
using Point = System.Drawing.Point;

namespace Chrono
{

#region Enums
public enum Action
{ Invalid, Quit, Rest, Move, MoveToInteresting, MoveToDanger, MoveAFAP, OpenDoor, CloseDoor, Pickup, Drop, DropType,
  GoUp, GoDown, Eat, Wear, Remove, Wield, Inventory, ShowMap, Fire, Quaff, Read, ViewItem, Invoke, SwapAB, Reassign,
  ManageSkills, UseItem, ExamineTile, Throw, ZapWand, CastSpell, ShowKnowledge, Save, NameItem, TalkTo
}

[Flags]
public enum Color : byte
{ Black=0, Red=1, Green=2, Blue=4, Bright=8,
  Cyan=Green|Blue, Purple=Red|Blue, Brown=Red|Green, Grey=Red|Green|Blue,
  DarkGrey=Black|Bright, LightRed=Red|Bright, LightGreen=Green|Bright, LightBlue=Blue|Bright,
  LightCyan=Cyan|Bright, Magenta=Purple|Bright, Yellow=Brown|Bright, White=Grey|Bright,

  Normal=Grey, Warning=Brown, Dire=LightRed
}

[Flags] public enum MenuFlag { None=0, Reletter=1, Multi=2, AllowNum=4, AllowNothing=8 };
#endregion

#region Input
public struct Input
{ public Input(Action action) { Action=action; Direction=Direction.Invalid; Count=1; }
  public Action Action;
  public Direction Direction;
  public int Count;
}
#endregion

#region InputOutput
public abstract class InputOutput
{ public string Ask(string prompt) { return Ask(Color.Normal, prompt, true, null); }
  public string Ask(Color color, string prompt) { return Ask(Color.Normal, prompt, true, null); }
  public string Ask(string prompt, bool allowEmpty) { return Ask(Color.Normal, prompt, allowEmpty, null); }
  public string Ask(Color color, string prompt, bool allowEmpty) { return Ask(color, prompt, allowEmpty, null); }
  public string Ask(string prompt, bool allowEmpty, string rebuke)
  { return Ask(Color.Normal, prompt, allowEmpty, rebuke);
  }
  public abstract string Ask(Color color, string prompt, bool allowEmpty, string rebuke);

  public char CharChoice(string prompt, string chars)
  { return CharChoice(Color.Normal, prompt, chars, '\0', false, null);
  }
  public char CharChoice(Color color, string prompt, string chars)
  { return CharChoice(color, prompt, chars, '\0', false, null);
  }
  public char CharChoice(string prompt, string chars, char defaultChar)
  { return CharChoice(Color.Normal, prompt, chars, defaultChar, false, null);
  }
  public char CharChoice(Color color, string prompt, string chars, char defaultChar)
  { return CharChoice(color, prompt, chars, defaultChar, false, null);
  }
  public char CharChoice(string prompt, string chars, char defaultChar, bool caseInsensitive)
  { return CharChoice(Color.Normal, prompt, chars, defaultChar, caseInsensitive, null);
  }
  public abstract char CharChoice(Color color, string prompt, string chars, char defaultChar, bool caseInsensitive,
                                  string rebuke);

  public Direction ChooseDirection() { return ChooseDirection(null, true, true); }
  public Direction ChooseDirection(string prompt) { return ChooseDirection(prompt, true, true); }
  public Direction ChooseDirection(bool allowSelf, bool allowVertical)
  { return ChooseDirection(null, allowSelf, allowVertical);
  }
  public abstract Direction ChooseDirection(string prompt, bool allowSelf, bool allowVertical);

  public abstract MenuItem[] ChooseItem(string prompt, IKeyedInventory items, MenuFlag flags, params ItemType[] types);

  public void DisplayInventory(IInventory inv) { DisplayInventory(inv, ItemType.Any); }
  public abstract void DisplayInventory(IInventory inv, params ItemType[] types);

  public abstract void DisplayTileItems(IInventory items);
  public abstract void ExamineTile(Player viewer, Point pt);
  public abstract Input GetNextInput();
  public abstract void ManageSkills(Player player);

  public MenuItem[] Menu(IInventory items, MenuFlag flags)
  { 
    return Menu(items, flags, ItemType.Any);
  }
  public abstract MenuItem[] Menu(ICollection<Item> items, MenuFlag flags, params ItemType[] types);
  public abstract MenuItem[] Menu(MenuItem[] items, MenuFlag flags);

  public void Print() { Print(Color.Normal, ""); }
  public void Print(string format, params object[] parms) { Print(Color.Normal, format, parms); }
  public void Print(Color color, string format, params object[] parms) { Print(color, string.Format(format, parms)); }
  public void Print(string line) { Print(Color.Normal, line); }
  public abstract void Print(Color color, string line);

  public void Render(Player viewer) { Render(viewer, true); }
  public abstract void Render(Player viewer, bool updateMap);

  public abstract void SetTitle(string title);

  public bool YesNo(string prompt, bool defaultYes) { return YesNo(Color.Normal, prompt, defaultYes); }
  public abstract bool YesNo(Color color, string prompt, bool defaultYes);

  public bool YN(string prompt, bool defaultYes) { return YN(Color.Normal, prompt, defaultYes); }
  public abstract bool YN(Color color, string prompt, bool defaultYes);
}
#endregion

#region MenuItem
public sealed class MenuItem
{ public MenuItem(Item item) { Item=item; Text=null; Count=0; Char = item==null ? '\0' : item.Char; }
  public MenuItem(Item item, int count) { Item=item; Text=null; Count=count; Char = item==null ? '\0' : item.Char; }
  public MenuItem(string text, char c) { Item=null; Text=text; Count=0; Char=c; }

  public Item Item;
  public string Text;
  public int  Count;
  public char Char;
}
#endregion

#region RangeTarget
public struct RangeTarget
{ public RangeTarget(Direction dir) { Point=new Point(-1, -1); Dir=dir; }
  public RangeTarget(Point pt) { Point=pt; Dir=Direction.Invalid; }
  public RangeTarget(Point pt, Direction dir) { Point=pt; Dir=dir; }
  public Point Point; // X,Y == -1,-1 for an invalid point (no selection)
  public Direction Dir;
}
#endregion

} // namespace Chrono