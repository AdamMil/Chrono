using System;
using SD=System.Drawing;

namespace Chrono
{

public enum Action
{ None, Quit, Rest, Move, MoveToInteresting, MoveToDanger, MoveAFAP, OpenDoor, CloseDoor, Pickup, Drop, DropType,
  GoUp, GoDown, Eat, Wear, Remove, Wield, Inventory, ShowMap, Fire, Quaff, Read, ViewItem, Invoke, SwapAB, Reassign,
  ManageSkills, UseItem, Carve, ExamineTile, Throw, ZapWand
}

public struct Input
{ public Input(Action action) { Action=action; Direction=Direction.Invalid; Count=1; }
  public Action Action;
  public Direction Direction;
  public int    Count;
}

[Flags]
public enum Color
{ Black=0, Red=1, Green=2, Blue=4, Bright=8,
  Cyan=Green|Blue, Purple=Red|Blue, Brown=Red|Green, Grey=Red|Green|Blue,
  DarkGrey=Black|Bright, LightRed=Red|Bright, LightGreen=Green|Bright, LightBlue=Blue|Bright,
  LightCyan=Cyan|Bright, Magenta=Purple|Bright, Yellow=Brown|Bright, White=Grey|Bright,

  Normal=Grey, Warning=Brown, Dire=LightRed
}

[Flags] public enum MenuFlag { None=0, Reletter=1, Multi=2, AllowNum=4, AllowNothing=8 };

public struct MenuItem
{ public MenuItem(Item item) { Item=item; Count=0; Char = item==null ? '\0' : item.Char; }
  public MenuItem(Item item, int count) { Item=item; Count=count; Char = item==null ? '\0' : item.Char; }
  public Item Item;
  public int  Count;
  public char Char;
}

public struct RangeTarget
{ public RangeTarget(Direction dir) { Point=new SD.Point(-1, -1); Dir=dir; }
  public RangeTarget(SD.Point pt) { Point=pt; Dir=Direction.Invalid; }
  public RangeTarget(SD.Point pt, Direction dir) { Point=pt; Dir=dir; }
  public SD.Point Point; // X,Y == -1,-1 for an invalid point (no selection)
  public Direction Dir;
}

public abstract class InputOutput
{ public abstract int ScrollBack { get; set; }

  public void Alert(string message) { Alert(Color.Warning, message); }
  public abstract void Alert(Color color, string message);

  public string Ask(string prompt) { return Ask(Color.Normal, prompt, true, null); }
  public string Ask(Color color, string prompt) { return Ask(color, prompt, true, null); }
  public string Ask(string prompt, bool allowEmpty) { return Ask(Color.Normal, prompt, allowEmpty); }
  public string Ask(Color color, string prompt, bool allowEmpty)
  { return Ask(color, prompt, allowEmpty, "Hey, enter something!");
  }
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

  public Direction ChooseDirection() { return ChooseDirection(true, true); }
  public abstract Direction ChooseDirection(bool allowSelf, bool allowVertical);

  public abstract MenuItem[] ChooseItem(string prompt, IKeyedInventory items, MenuFlag flags,
                                        params ItemClass[] classes);

  public abstract RangeTarget ChooseTarget(Entity viewer, bool allowDir);

  public void DisplayInventory(IKeyedInventory items) { DisplayInventory(items, ItemClass.Any); }
  public abstract void DisplayInventory(IKeyedInventory items, params ItemClass[] classes);
  public abstract void DisplayTileItems(IInventory items);
  public abstract void DisplayMap(Entity viewer);

  public abstract void ExamineItem(Entity viewer, Item item);
  public abstract void ExamineTile(Entity viewer, SD.Point pos);

  public abstract Input GetNextInput();

  public abstract void ManageSkills(Entity player);

  public MenuItem[] Menu(IInventory items, MenuFlag flags)
  { return Menu(items, flags, ItemClass.Any);
  }
  public abstract MenuItem[] Menu(System.Collections.ICollection items, MenuFlag flags, params ItemClass[] classes);

  public void Print(string format, params object[] parms) { Print(Color.Normal, format, parms); }
  public void Print(Color color, string format, params object[] parms) { Print(color, String.Format(format, parms)); }
  public void Print(string line) { Print(Color.Normal, line); }
  public abstract void Print();
  public abstract void Print(Color color, string line);

  public abstract void Render(Entity viewer);

  public abstract void SetTitle(string title);

  public bool YesNo(string prompt, bool defaultYes) { return YesNo(Color.Normal, prompt, defaultYes); }
  public abstract bool YesNo(Color color, string prompt, bool defaultYes);
}

} // namespace Chrono