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
    console.Fill();
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
      char c = ReadChar();
      if(c=='\r' || rec.Key.VirtualKey==NTConsole.Key.Escape) c = defaultChar;
      if(chars==null || c==defaultChar ||
         (caseInsensitive ? chars.ToLower() : chars).IndexOf(c) != -1)
      { TextInput = false;
        return c;
      }
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
    Direction d = CharToDirection(CharChoice(prompt, null), allowSelf, allowVertical);
    if(d==Direction.Invalid) Print("That's an odd direction!");
    return d;
  }

  public override void DisplayInventory(System.Collections.ICollection items, ItemClass itemClass)
  {
  }

  public override MenuItem[] Menu(System.Collections.ICollection items, MenuFlag flags, ItemClass itemClass)
  { if(items.Count==0) throw new ArgumentException("No items in the collection.", "items");
    if(items.Count>52) throw new NotSupportedException("Too many items in the collection.");

    Item[] itemarr = new Item[items.Count];
    items.CopyTo(itemarr, 0);
    Array.Sort(itemarr, ItemComparer.Default);

    if(itemClass==ItemClass.Invalid)
    { menu = new MenuItem[items.Count];
      for(int i=0,mi=0; i<(int)ItemClass.NumClasses; i++) // then group by item class
        for(int j=0; j<itemarr.Length; j++) if(itemarr[j].Class==(ItemClass)i) menu[mi++] = new MenuItem(itemarr[j]);
    }
    else
    { int count=0;
      for(int i=0; i<itemarr.Length; i++) if(itemarr[i].Class==itemClass) count++;
      menu = new MenuItem[count];
      for(int i=0,mi=0; i<itemarr.Length; i++) if(itemarr[i].Class==itemClass) menu[mi++] = new MenuItem(itemarr[i]);
    }

    if((flags|MenuFlag.Reletter)!=0)
    { char c='a';
      for(int i=0; i<menu.Length; c++,i++)
      { if(c>'z') c='A';
        menu[i].Char = c;
      }
    }

    int ci=0, y=0, width=Math.Min(MapWidth, console.Width), height=console.Height, iheight=height-2;
    console.Fill(0, 0, width, height); // clear the area we'll be using

    ItemClass head = ItemClass.Invalid;
    for(int mi=0,yi=0; yi<iheight && mi<menu.Length; yi++)
    { if(menu[mi].Item.Class != head)
      { head = menu[mi].Item.Class;
        PutString(NTConsole.Attribute.White, 0, y+yi, head.ToString());
      }
      else
      { PutString(0, y+yi, "[{0}] {1}",
                  (flags&MenuFlag.AllowNum)==0 ?
                    menu[mi].Count==0 ? "-" : "+" :
                    menu[mi].Count==0 ? " - " :
                                      menu[mi].Count==menu[mi].Item.Count ? " + " : menu[mi].Count.ToString("d3"),
                  menu[mi].Item.FullName);
      }
    }
    console.SetCursorPosition((flags&MenuFlag.AllowNum)==0 ? 1 : 2, ci-y);

    if(buf!=null) console.PutBlock(0, 0, 0, 0, mapW, mapH, buf); // replace what we've overwritten
    DrawLines();

    return null;
  }

  public override void Print() { AddLine(Color.Normal, ""); }
  public override void Print(Color color, string line) { AddLine(color, line); }

  public override Input GetNextInput()
  { while(true)
    { Input inp = new Input();
      ReadChar();
      char c = NormalizeDirChar();

      if(c==0)
        {
        }
      else if(rec.Key.HasMod(NTConsole.Modifier.Ctrl))
        switch(c+64)
        { case 'Q': inp.Action = Action.Quit; break;
        }
      else
        switch(c)
        { case 'b': case 'h': case 'j': case 'k': case 'l': case 'n': case 'u': case 'y':
            inp.Action = Action.Move;
            inp.Direction = CharToDirection(c);
            break;
          case 'B': case 'H': case 'J': case 'K': case 'L': case 'N': case 'U': case 'Y':
            inp.Action = Action.MoveToInteresting;
            inp.Direction = CharToDirection(c);
            break;
          case '.': inp.Action = Action.Rest; break;
          case ',': inp.Action = Action.Pickup; break;
          case 'd': inp.Action = Action.Drop; break;
          case 'D': inp.Action = Action.DropType; break;
          case '<': inp.Action = Action.GoUp; break;
          case '>': inp.Action = Action.GoDown; break;
          case 'c': inp.Action = Action.CloseDoor; break;
          case 'o': inp.Action = Action.OpenDoor; break;
          case '/':
            inp.Direction = CharToDirection(ReadChar());
            if(inp.Direction==Direction.Self) { count=100; inp.Action=Action.Rest; }
            else if(inp.Direction!=Direction.Invalid) inp.Action = Action.MoveToInteresting;
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
  { Map map = viewer.Memory==null ? viewer.Map : viewer.Memory;

    mapW = Math.Min(console.Width, MapWidth); mapH = Math.Min(console.Height, MapHeight);
    Rectangle rect = new Rectangle(viewer.Position.X-mapW/2, viewer.Position.Y-mapH/2, mapW, mapH);
    int size = rect.Width*rect.Height;
    if(buf==null || buf.Length<size) buf = new NTConsole.CharInfo[size];
    if(vis==null || vis.Length<size) vis = new bool[size];

    Array.Clear(vis, 0, size);
    Point[] vpts = viewer.VisibleTiles();
    for(int i=0; i<vpts.Length; i++)
      if(rect.Contains(vpts[i])) vis[(vpts[i].Y-rect.Y)*rect.Width+vpts[i].X-rect.X] = true;

    if(map==viewer.Map)
    { for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++) buf[i] = TileToChar(map[x,y], vis[i]);
      RenderMonsters(map.Creatures, vpts, rect, true);
    }
    else
    { for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++)
        { Tile tile = map[x,y];
          buf[i] = tile.Creature==null ? TileToChar(tile, vis[i]) : CreatureToChar(tile.Creature, vis[i]);
        }
      map = viewer.Map;
      for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++) if(vis[i]) buf[i] = TileToChar(map[x,y], vis[i]);
      RenderMonsters(map.Creatures, vpts, rect, true);
    }

    console.PutBlock(new Rectangle(0, 0, rect.Width, rect.Height), buf);
    if(redrawStats)
    { RenderStats(viewer);
      console.SetCursorPosition(mapW/2, mapH/2);
      redrawStats=false;
    }
  }

  public override void SetTitle(string title) { console.Title = title; }

  public override bool YesNo(Color color, string prompt, bool defaultYes)
  { char c = CharChoice(color, prompt, defaultYes ? "Yn" : "yN", defaultYes ? 'y' : 'n', true, null);
    return c==0 ? defaultYes : Char.ToLower(c)=='y';
  }

  struct Line
  { public Line(Color color, string text) { Color=color; Text=text; }
    public Color Color;
    public string Text;
  }

  class ItemComparer : System.Collections.IComparer
  { public int Compare(object x, object y) { return ((Item)x).Char-((Item)y).Char; }
    public static readonly ItemComparer Default = new ItemComparer();
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
  
  Direction CharToDirection(char c) { return CharToDirection(c, true, false); }
  Direction CharToDirection(char c, bool allowSelf, bool allowVertical)
  { c = char.ToLower(NormalizeDirChar());
    switch(c)
    { case 'b': return Direction.DownLeft;
      case 'j': return Direction.Down;
      case 'n': return Direction.DownRight;
      case 'h': return Direction.Left;
      case '.': return allowSelf ? Direction.Self : Direction.Invalid;
      case 'l': return Direction.Right;
      case 'y': return Direction.UpLeft;
      case 'k': return Direction.Up;
      case 'u': return Direction.UpRight;
      case '<': return allowVertical ? Direction.Above : Direction.Invalid;
      case '>': return allowVertical ? Direction.Below : Direction.Invalid;
      default: return Direction.Invalid;
    }
  }

  void DrawLines()
  { console.Fill(0, 40, console.Width, console.Height-MapHeight);
    console.SetCursorPosition(0, 40);

    LinkedList.Node node = lines.Tail;
    int nlines = Math.Min(uncleared, LineSpace);
    for(int i=1; i<nlines; i++) node=node.PrevNode;
    for(int i=0; i<nlines; node=node.NextNode,i++)
    { Line line = (Line)node.Data;
      console.Attributes = ColorToAttr(line.Color);
      console.WriteLine(line.Text);
    }
    console.SetCursorPosition(mapW/2, mapH/2);
  }

  char NormalizeDirChar()
  { char c = rec.Key.Char;
    if(rec.Key.VirtualKey>=NTConsole.Key.Numpad1 && rec.Key.VirtualKey<=NTConsole.Key.Numpad9)
    { c = dirLets[(int)rec.Key.VirtualKey-(int)NTConsole.Key.Numpad1];
      if(rec.Key.HasMod(NTConsole.Modifier.Shift)) c = char.ToUpper(c);
    }
    else
      switch(rec.Key.VirtualKey)
      { case NTConsole.Key.End:   c='B'; break;
        case NTConsole.Key.Down:  c='J'; break;
        case NTConsole.Key.Next:  c='N'; break;
        case NTConsole.Key.Left:  c='H'; break;
        case NTConsole.Key.Right: c='L'; break;
        case NTConsole.Key.Home:  c='Y'; break;
        case NTConsole.Key.Up:    c='K'; break;
        case NTConsole.Key.Prior: c='U'; break;
        default: return c;
      }
    return rec.Key.Char = c;
  }

  void PutString(int x, int y, string str) { PutString(ColorToAttr(Color.Normal), x, y, str); }
  void PutString(Color color, int x, int y, string str) { PutString(ColorToAttr(color), x, y, str); }
  void PutString(int x, int y, string format, params object[] parms) { PutString(Color.Normal, x, y, format, parms); }
  void PutString(Color color, int x, int y, string format, params object[] parms)
  { PutString(ColorToAttr(Color.Normal), x, y, string.Format(format, parms));
  }
  void PutString(NTConsole.Attribute attr, int x, int y, string format, params object[] parms)
  { PutString(attr, x, y, string.Format(format, parms));
  }
  void PutString(NTConsole.Attribute attr, int x, int y, string str)
  { console.SetCursorPosition(x, y);
    console.Attributes = attr;
    console.Write(str);
  }

  char ReadChar()
  { if(rec.Type==NTConsole.InputType.Keyboard && --rec.Key.RepeatCount<=0)
      rec.Type=NTConsole.InputType.BufferResize;
    while(rec.Type!=NTConsole.InputType.Keyboard || !rec.Key.KeyDown || rec.Key.Char==0 &&
          (rec.Key.VirtualKey>=NTConsole.Key.Shift && rec.Key.VirtualKey<=NTConsole.Key.Menu ||
           rec.Key.VirtualKey>=NTConsole.Key.Numlock))
      rec = console.ReadInput();
    return rec.Key.Char;
  }

  void RenderMonsters(System.Collections.ICollection coll, Point[] vpts, Rectangle rect, bool wantvis)
  { foreach(Creature c in coll)
    { Point cp = c.Position;
      int bi = (c.Y-rect.Y)*rect.Width + c.X-rect.X;
      if(!rect.Contains(cp) || vis[bi]!=wantvis) continue;
      for(int i=0; i<vpts.Length; i++)
        if(vpts[i]==cp) { buf[bi] = CreatureToChar(c, vis[bi]); break; }
    }
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
    PutString(x, 9, "Gold: {0}", 0);
    PutString(x,10, "Exp:  {0}/{0}", player.Exp, player.NextExp);
    PutString(x,11, "Turn: {0}", player.Age);
    PutString(x,12, "Dungeon level {0}", App.CurrentLevel+1);
  }

  NTConsole.CharInfo[] buf;
  bool[] vis;
  MenuItem[] menu;
  NTConsole console = new NTConsole();
  LinkedList lines = new LinkedList(); // a circular array would be better
  NTConsole.InputRecord rec;
  int  uncleared=0, maxLines=200, mapW, mapH, count;
  bool inputMode, redrawStats=true;

  static NTConsole.Attribute ColorToAttr(Color color)
  { NTConsole.Attribute attr = NTConsole.Attribute.Black;
    if((color & Color.Red)    != Color.Black) attr |= NTConsole.Attribute.Red;
    if((color & Color.Green)  != Color.Black) attr |= NTConsole.Attribute.Green;
    if((color & Color.Blue)   != Color.Black) attr |= NTConsole.Attribute.Blue;
    if((color & Color.Bright) != Color.Black) attr |= NTConsole.Attribute.Bright;
    return attr;
  }

  static NTConsole.CharInfo CreatureToChar(Creature c, bool visible)
  { return new NTConsole.CharInfo(raceMap[(int)c.Race], visible ? ColorToAttr(c.Color) : NTConsole.Attribute.DarkGrey);
  }
  
  static NTConsole.CharInfo ItemToChar(Item item)
  { return new NTConsole.CharInfo('%', ColorToAttr(item.Color));
  }

  static NTConsole.CharInfo TileToChar(Tile tile, bool visible)
  { NTConsole.CharInfo ci;
    if(tile.Items!=null && tile.Items.Count>0) ci = ItemToChar(tile.Items[0]);
    else
      switch(tile.Type)
      { case TileType.Wall:       ci = new NTConsole.CharInfo('#', NTConsole.Attribute.Brown); break;
        case TileType.ClosedDoor: ci = new NTConsole.CharInfo('+', NTConsole.Attribute.Yellow); break;
        case TileType.OpenDoor:   ci = new NTConsole.CharInfo((char)254, NTConsole.Attribute.Yellow); break;
        case TileType.RoomFloor:  ci = new NTConsole.CharInfo((char)250, NTConsole.Attribute.Grey); break;
        case TileType.Corridor:   ci = new NTConsole.CharInfo((char)176, NTConsole.Attribute.Grey); break;
        case TileType.UpStairs:   ci = new NTConsole.CharInfo('<', NTConsole.Attribute.Grey); break;
        case TileType.DownStairs: ci = new NTConsole.CharInfo('>', NTConsole.Attribute.Grey); break;
        default: ci = new NTConsole.CharInfo(' ', NTConsole.Attribute.Black); break;
      }
    if(!visible) ci.Attributes = NTConsole.Attribute.DarkGrey;
    return ci;
  }

  static readonly char[] raceMap = new char[(int)Race.NumRaces]
  { '@', 'o'
  };
  
  static readonly char[] dirLets = new char[9] { 'b', 'j', 'n', 'h', '.', 'l', 'y', 'k', 'u' };
}

} // namespace Chrono.Application