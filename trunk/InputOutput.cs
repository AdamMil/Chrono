using System;
using SD=System.Drawing;

namespace Chrono
{

public enum Action { None, Quit, Rest, Move, MoveToInteresting, MoveToDanger, MoveAFAP, OpenDoor, CloseDoor }

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

public abstract class InputOutput
{ public abstract bool RedrawStats { get; set; }
  public abstract int ScrollBack { get; set; }
  
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
  { return CharChoice(Color.Normal, prompt, chars, '\n', false, null);
  }
  public char CharChoice(Color color, string prompt, string chars)
  { return CharChoice(color, prompt, chars, '\n', false, null);
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

  public abstract Input GetNextInput();

  public void Print(string format, params object[] parms) { Print(Color.Normal, format, parms); }
  public void Print(Color color, string format, params object[] parms) { Print(color, String.Format(format, parms)); }
  public void Print(string line) { Print(Color.Normal, line); }
  public abstract void Print();
  public abstract void Print(Color color, string line);

  public abstract void Render(Creature viewer);

  public abstract void SetTitle(string title);

  public bool YesNo(string prompt, bool defaultYes) { return YesNo(Color.Normal, prompt, defaultYes); }
  public abstract bool YesNo(Color color, string prompt, bool defaultYes);
}

} // namespace Chrono