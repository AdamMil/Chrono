using System;
using System.Drawing;
using GameLib.Collections;
using Chrono;

namespace Chrono
{

public sealed class ConsoleIO : InputOutput
{ public ConsoleIO()
  { console.SetSize(80, 50);
    console.InputMode  = NTConsole.InputModes.None;
    console.OutputMode = NTConsole.OutputModes.Processed|NTConsole.OutputModes.WrapAtEOL;
    console.Fill(NTConsole.Blank);
    console.SetCursorVisibility(true, 20);
  }

  public override bool RedrawStats { get { return redrawStats; } set { redrawStats=value; } }

  public override int ScrollBack
  { get { return maxLines; }
    set
    { if(value<0) throw new ArgumentOutOfRangeException("ScrollBack", value, "cannot be negative");
      maxLines = Math.Max(value, 30);
    }
  }

  public override void Alert(Color color, string message) { AddLine(color, message); }

  public override string Ask(Color color, string prompt, bool allowEmpty, string rebuke)
  { string sprompt = prompt+' ';
    bool doRebuke=false;
    TextInput = true;
    while(true)
    { if(doRebuke) AddLine(color, rebuke, false);
      AddLine(color, sprompt);
      console.SetCursorPosition(sprompt.Length, Math.Min(uncleared, LineSpace)+MapHeight-1);
      string answer = console.ReadLine();
      if(answer!="" || allowEmpty) { TextInput = false; return answer; }
      doRebuke = true;
    }
  }

  public override char CharChoice(Color color, string prompt, string chars, char defaultChar, bool caseInsensitive,
                                  string rebuke)
  { string sprompt = prompt + (chars!=null ? " [" + chars + "] " : " ");
    bool doRebuke  = false;
    TextInput = true;
    if(rebuke==null) rebuke = "Invalid selection!";
    while(true)
    { if(doRebuke) AddLine(color, rebuke, false);
      AddLine(color, sprompt);
      console.SetCursorPosition(sprompt.Length, Math.Min(uncleared, LineSpace)+MapHeight-1);
      char c = console.ReadChar();
      if(c=='\r') c = defaultChar;
      if(chars==null || (caseInsensitive ? chars.ToLower() : chars).IndexOf(c) != -1) { TextInput = false; return c; }
      doRebuke = true;
    }
  }

  public override Direction ChooseDirection(bool allowSelf, bool allowVertical)
  { string prompt = "Choose a direction";
    if(allowSelf || allowVertical)
    { prompt += " [dir, ";
      if(allowSelf) prompt += '5';
      if(allowVertical) prompt += "<>";
      prompt += "]:";
    }
    else prompt += ':';
    char c = CharChoice(prompt, null);
    Direction d = !allowSelf&&c=='5' || !allowVertical && (c=='<' || c=='>') ? Direction.Invalid : CharToDirection(c);
    if(d==Direction.Invalid) Print("That's an odd direction!");
    return d;
  }

  public override void Print() { AddLine(Color.Normal, ""); }
  public override void Print(Color color, string line) { AddLine(color, line); }

  public override Input GetNextInput()
  { while(true)
    { Input inp = new Input();
      char c = console.ReadChar();
      if(c=='0') continue;
      if(c!='5' && char.IsDigit(c))
      { inp.Action = Action.Move;
        inp.Direction = CharToDirection(c);
        return inp;
      }
      switch(c)
      { case (char)('Q'-64): inp.Action = Action.Quit; break;
        case '.': case '5': inp.Action = Action.Rest; break;
        case 'c': inp.Action = Action.CloseDoor; break;
        case 'o': inp.Action = Action.OpenDoor; break;
        case '/':
          c = console.ReadChar();
          if(c=='0') continue;
          if(c=='5') { count=100; inp.Action = Action.Rest; }
          else if(char.IsDigit(c))
          { inp.Action = Action.MoveToInteresting;
            inp.Direction = CharToDirection(c);
          }
          break;
      }
      if(inp.Action != Action.None)
      { inp.Count = count;
        count = 0;
        return inp;
      }
    }
  }

  public override void Render(Creature viewer)
  { Map map = viewer.Map;
    int width = Math.Min(console.Width, MapWidth), height = Math.Min(console.Height, MapHeight);
    Rectangle rect = new Rectangle(viewer.Position.X-width/2, viewer.Position.Y-height/2, width, height);
    if(buf==null || buf.Length<rect.Width*rect.Height) buf = new NTConsole.CharInfo[rect.Width*rect.Height];
    for(int i=0,y=rect.Top; y<rect.Bottom; y++)
      for(int x=rect.Left; x<rect.Right; i++,x++)
        switch(map[x,y].Type)
        { case TileType.Wall:       buf[i] = new NTConsole.CharInfo('#', NTConsole.Attribute.Grey); break;
          case TileType.ClosedDoor: buf[i] = new NTConsole.CharInfo('+', NTConsole.Attribute.Brown); break;
          case TileType.OpenDoor:   buf[i] = new NTConsole.CharInfo((char)254, NTConsole.Attribute.Brown); break;
          case TileType.RoomFloor:  buf[i] = new NTConsole.CharInfo((char)250, NTConsole.Attribute.Grey); break;
          case TileType.Corridor:   buf[i] = new NTConsole.CharInfo((char)176, NTConsole.Attribute.Grey); break;
          case TileType.UpStairs:   buf[i] = new NTConsole.CharInfo('<', NTConsole.Attribute.Grey); break;
          case TileType.DownStairs: buf[i] = new NTConsole.CharInfo('>', NTConsole.Attribute.Grey); break;
          default: buf[i] = new NTConsole.CharInfo(' ', NTConsole.Attribute.Black); break;
        }
    foreach(Creature c in map.Creatures)
      if(rect.Contains(c.Position))
        buf[(c.Y-rect.Y)*rect.Width + c.X-rect.X] = new NTConsole.CharInfo('@', NTConsole.Attribute.White);
    console.PutBlock(new Rectangle(0, 0, rect.Width, rect.Height), buf);
    if(redrawStats)
    { RenderStats(viewer);
      console.SetCursorPosition(mapCX=width/2, mapCY=height/2);
      redrawStats=false;
    }
  }

  public override void SetTitle(string title) { console.Title = title; }

  public override bool YesNo(Color color, string prompt, bool defaultYes)
  { return Char.ToLower(CharChoice(color, prompt, defaultYes ? "Yn" : "yN", defaultYes ? 'y' : 'n', true, null))=='y';
  }

  struct Line
  { public Line(Color color, string text) { Color=color; Text=text; }
    public Color Color;
    public string Text;
  }

  const int MapWidth=50, MapHeight=40;

  int LineSpace { get { return console.Height-MapHeight-1; } }

  bool TextInput
  { get { return inputMode; }
    set
    { if(value==inputMode) return;
      console.InputMode = value ? NTConsole.InputModes.LineBuffered|NTConsole.InputModes.Echo
                                : NTConsole.InputModes.None;
      uncleared = 0;
      DrawLines();
      inputMode = value;
    }
  }

  void AddLine(Color color, string line) { AddLine(color, line, true); }
  void AddLine(Color color, string line, bool redraw)
  { lines.Append(new Line(color, line));
    uncleared++;
    while(lines.Count>maxLines) lines.Remove(lines.Head);
    if(redraw) DrawLines();
  }
  
  void DrawLines()
  { console.Fill(0, 40, console.Width, console.Height-MapHeight, NTConsole.Blank);
    console.SetCursorPosition(0, 40);

    LinkedList.Node node = lines.Tail;
    int nlines = Math.Min(uncleared, LineSpace);
    for(int i=1; i<nlines; i++) node=node.PrevNode;
    for(int i=0; i<nlines; node=node.NextNode,i++)
    { Line line = (Line)node.Data;
      console.Attributes = ColorToAttr(line.Color);
      console.WriteLine(line.Text);
    }
    console.SetCursorPosition(mapCX, mapCY);
  }
  void PutString(int x, int y, string str) { PutString(Color.Normal, x, y, str); }
  void PutString(Color color, int x, int y, string str)
  { console.SetCursorPosition(x, y);
    console.Attributes = ColorToAttr(color);
    console.Write(str);
  }
  void PutString(int x, int y, string format, params object[] parms) { PutString(Color.Normal, x, y, format, parms); }
  void PutString(Color color, int x, int y, string format, params object[] parms)
  { console.SetCursorPosition(x, y);
    console.Attributes = ColorToAttr(color);
    console.Write(format, parms);
  }

  void RenderStats(Creature player)
  { const int x = MapWidth+2;
    int healthpct = player.HP*100/player.MaxHP;
    PutString(x, 0, "{0} the {1} (lv {2})", player.Name, player.Title, player.ExpLevel+1);
    PutString(x, 1, "Human");
    PutString(healthpct<25 ? Color.Dire : healthpct<50 ? Color.Warning : Color.Normal,
              x, 2, "HP:   {0}/{1}", player.HP, player.MaxHP);
    PutString(x, 3, "MP:   {0}/{1}", player.MP, player.MaxMP);
    PutString(x, 4, "AC:   {0}", player.AC);
    PutString(x, 5, "EV:   {0}", player.EV);
    PutString(x, 6, "Str:  {0}", player.Str);
    PutString(x, 7, "Int:  {0}", player.Int);
    PutString(x, 8, "Dex:  {0}", player.Dex);
    PutString(x, 9, "Per:  {0}", player.Per);
    PutString(x,10, "Gold: {0}", 0);
    PutString(x,11, "Exp:  {0}/{0}", player.Exp, player.NextExp);
    PutString(x,12, "Turn: {0}", player.Age);
    PutString(x,13, "Dungeon map {0}", App.CurrentLevel+1);
  }

  NTConsole.CharInfo[] buf;
  NTConsole console = new NTConsole();
  LinkedList lines = new LinkedList(); // a circular array would be better
  int  uncleared=0, maxLines=200, mapCX, mapCY, count;
  bool inputMode, redrawStats=true;

  static Direction CharToDirection(char c)
  { switch(c)
    { case '1': return Direction.DownLeft;
      case '2': return Direction.Down;
      case '3': return Direction.DownRight;
      case '4': return Direction.Left;
      case '5': return Direction.Self;
      case '6': return Direction.Right;
      case '7': return Direction.UpLeft;
      case '8': return Direction.Up;
      case '9': return Direction.UpRight;
      case '<': return Direction.Above;
      case '>': return Direction.Below;
      default: return Direction.Invalid;
    }
  }

  static NTConsole.Attribute ColorToAttr(Color color)
  { NTConsole.Attribute attr = NTConsole.Attribute.Black;
    if((color & Color.Red)    != Color.Black) attr |= NTConsole.Attribute.Red;
    if((color & Color.Green)  != Color.Black) attr |= NTConsole.Attribute.Green;
    if((color & Color.Blue)   != Color.Black) attr |= NTConsole.Attribute.Blue;
    if((color & Color.Bright) != Color.Black) attr |= NTConsole.Attribute.Bright;
    return attr;
  }
}

} // namespace Chrono.Application