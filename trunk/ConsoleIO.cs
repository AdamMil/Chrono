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
    for(int i=0; i<stats.Length; i++) stats[i] = int.MinValue;
  }

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
      SetCursorAtEOBL();
      string answer = console.ReadLine();
      AppendToBL(answer);
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
      SetCursorAtEOBL();
      char c = ReadChar(true);
      if(c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) c = defaultChar;
      else AppendToBL(c);
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

  public override MenuItem[] ChooseItem(string prompt, IKeyedInventory items, MenuFlag flags,
                                        params ItemClass[] classes)
  { string chars = items.CharString(classes);
    if((flags&MenuFlag.AllowNothing)!=0) chars = "-"+chars;
    bool any = Array.IndexOf(classes, ItemClass.Any)!=-1;
    if(any && chars.Length==0) return new MenuItem[0];
    if(chars.Length>0) chars += '?';
    if(!any || chars.Length==0) chars += '*';

    string sprompt = prompt + " [" + chars + "] ";
    bool doRebuke  = false;
    TextInput = true;
    while(true)
    { int num=-1;
      if(doRebuke) AddLine(Color.Normal, "Invalid selection!", false);
      AddLine(Color.Normal, sprompt);
      SetCursorAtEOBL();
      char c = ReadChar(true);
      if(c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) return new MenuItem[0];
      else AppendToBL(c);
      if(char.IsDigit(c) && (flags&MenuFlag.AllowNum)!=0)
      { num = c-'0';
        while(char.IsDigit(c=ReadChar(true))) num = c-'0' + num*10;
      }
      if(num<-1 || num==0 || chars.IndexOf(c)==-1) { doRebuke=true; continue; }
      if(char.IsLetter(c) && num>items[c].Count) { AddLine(Color.Normal, "You don't have that many!"); continue; }
      TextInput = false;
      if(c=='-') return new MenuItem[1] { new MenuItem(null, 0) };
      if(c=='?') return Menu(items, flags, classes);
      if(c=='*') return Menu(items, flags, ItemClass.Any);
      return new MenuItem[1] { new MenuItem(items[c], num==-1 ? items[c].Count : num) };
    }
  }

  public override void DisplayInventory(IKeyedInventory items, params ItemClass[] classes)
  { SetupMenu(items, MenuFlag.None, classes);

    int cs=0, width=Math.Min(MapWidth, console.Width), height=console.Height, iheight=height-2;
    ClearScreen();

    while(true)
    { redraw:
      ItemClass head = ItemClass.Invalid;
      int mc=cs, yi=0;
      for(; yi<iheight && mc<menu.Length; yi++) // draw the menu items
      { if(menu[mc].Item.Class != head)
        { head = menu[mc].Item.Class;
          PutString(NTConsole.Attribute.White, 0, yi, head.ToString());
        }
        else
        { PutString(0, yi, "{0} - {1}", menu[mc].Char, menu[mc].Item.FullName);
          mc++;
        }
      }
      PutString(0, yi, cs+mc<menu.Length ? "--more--" : "--bottom--");

      while(true)
      { char c = ReadChar();
        if(c=='\r' || c=='\n') goto done;
        if(c==' ') rec.Key.VirtualKey = NTConsole.Key.Next;
        switch(rec.Key.VirtualKey)
        { case NTConsole.Key.Prior: case NTConsole.Key.Up: case NTConsole.Key.Numpad8:
            if(cs>0) // page up
            { cs -= Math.Min(iheight, cs);
              console.Fill(0, 0, width, height); // clear the area we'll be using
              goto redraw;
            }
            break;
          case NTConsole.Key.Next: case NTConsole.Key.Down: case NTConsole.Key.Numpad2:
            if(menu.Length>cs+mc) // page down
            { cs += mc;
              console.Fill(0, 0, width, height); // clear the area we'll be using
              goto redraw;
            }
            break;
          case NTConsole.Key.Escape: goto done;
        }
      }
    }
    done:
    RestoreScreen();
  }
  
  public override void DisplayMap(Creature viewer) { }

  public override MenuItem[] Menu(System.Collections.ICollection items, MenuFlag flags, params ItemClass[] classes)
  { SetupMenu(items, flags, classes);

    MenuItem[] ret;
    bool reletter=(flags&MenuFlag.Reletter)!=0, allownum=(flags&MenuFlag.AllowNum)!=0;
    int cs=0, width=Math.Min(MapWidth, console.Width);
    int height=reletter ? Math.Min(54, console.Height) : console.Height, iheight=height-2;

    while(true) // drawing
    { redraw:
      ClearScreen(); // clear the area we'll be using
      ItemClass head = ItemClass.Invalid;
      int mc=cs, yi=0;
      char c='a';
      for(; yi<iheight && mc<menu.Length; yi++) // draw the menu items
      { if(menu[mc].Item.Class != head)
        { head = menu[mc].Item.Class;
          PutString(NTConsole.Attribute.White, 0, yi, head.ToString());
        }
        else
        { if(reletter) menu[mc].Char=c;
          DrawMenuItem(yi, menu[mc++], flags);
          if(++c>'z') c='A';
        }
      }
      PutString(0, yi, "Enter selection:");

      while(true) // key handling
      { int num=-1;
        c = ReadChar();
        
        if(allownum && char.IsDigit(c)) // read the number of items if allowed
        { num = c-'0';
          while(char.IsDigit(c=ReadChar())) num = c-'0' + num*10;
          if(num<0) continue;
        }
        if(char.IsLetter(c))
        { head = ItemClass.Invalid;
          for(int i=reletter?cs:0,end=reletter?mc:menu.Length,y=-1; i<end; i++)
          { if(i>=cs && i<cs+mc) // if it's onscreen
            { if(head!=menu[i].Item.Class) { head=menu[i].Item.Class; y++; } // calculate the offset to the item
              y++;
            }
            if(menu[i].Char==c)
            { menu[i].Count = num>-1 ? Math.Min(num, menu[i].Item.Count) : menu[i].Count>0 ? 0 : menu[i].Item.Count;
              if((flags&MenuFlag.Multi)==0 && menu[i].Count>0)
              { for(int j=0; j<menu.Length; j++) if(j!=i) menu[j].Count=0;
                goto redraw;
              }
              else if(i>=cs && i<cs+mc)            // if it's onscreen
              { DrawMenuItem(y, menu[i], flags);   // draw it
                console.SetCursorPosition(16, yi); // and restore the cursor, 16 == length of "Enter selection:"
              }
              break;
            }
          }
        }
        else
        { switch(c)
          { case '+': for(int i=0; i<menu.Length; i++) menu[i].Count = menu[i].Item.Count; goto redraw;
            case '-': for(int i=0; i<menu.Length; i++) menu[i].Count = 0; goto redraw;
            case '\r': case '\n': goto done;
            case ' ': rec.Key.VirtualKey=NTConsole.Key.Next; break;
          }
          switch(rec.Key.VirtualKey)
          { case NTConsole.Key.Prior: case NTConsole.Key.Up: case NTConsole.Key.Numpad8:
              if(cs>0) // page up
              { cs -= Math.Min(iheight, cs);
                console.Fill(0, 0, width, height); // clear the area we'll be using
                goto redraw;
              }
              break;
            case NTConsole.Key.Next: case NTConsole.Key.Down: case NTConsole.Key.Numpad2:
              if(menu.Length>cs+mc) // page down
              { cs += mc;
                console.Fill(0, 0, width, height); // clear the area we'll be using
                goto redraw;
              }
              break;
            case NTConsole.Key.Escape: ret=new MenuItem[0]; goto doreturn;
          }
        }
      }
    }
    done:
    cs = 0;
    for(int i=0; i<menu.Length; i++) if(menu[i].Count>0) cs++;
    ret = new MenuItem[cs];
    for(int i=0,mi=0; i<menu.Length; i++) if(menu[i].Count>0) { menu[i].Char=menu[i].Item.Char; ret[mi++]=menu[i]; }

    doreturn:
    if(buf!=null) console.PutBlock(0, 0, 0, 0, mapW, mapH, buf); // replace what we've overwritten
    DrawLines();
    return ret;
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
      else if(rec.Key.HasMod(NTConsole.Modifier.Ctrl)) switch(c+64)
      { case 'Q': inp.Action = Action.Quit; break;
      }
      else switch(c)
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
        case 'c': inp.Action = Action.CloseDoor; break;
        case 'd': inp.Action = Action.Drop; break;
        case 'D': inp.Action = Action.DropType; break;
        case 'e': inp.Action = Action.Eat; break;
        case 'i': inp.Action = Action.Inventory; break;
        case 'o': inp.Action = Action.OpenDoor; break;
        case 'R': inp.Action = Action.Remove; break;
        case 'w': inp.Action = Action.Wield; break;
        case 'W': inp.Action = Action.Wear; break;
        case '<': inp.Action = Action.GoUp; break;
        case '>': inp.Action = Action.GoDown; break;
        case '/':
          inp.Direction = CharToDirection(ReadChar());
          if(inp.Direction==Direction.Self) { count=100; inp.Action=Action.Rest; }
          else if(inp.Direction!=Direction.Invalid) inp.Action = Action.MoveToInteresting;
          break;
        case '?': ShowHelp(); break;
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
    RenderStats(viewer);
    SetCursorToPlayer();
    uncleared = 0;
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

  int LineSpace { get { return console.Height-MapHeight-1; } }
  int MapWidth  { get { return Math.Max(console.Width-30, 50); } }
  int MapHeight { get { return Math.Max(console.Height-16, 40); } }

  bool TextInput
  { get { return inputMode; }
    set
    { if(value==inputMode) return;
      console.InputMode = value ? NTConsole.InputModes.LineBuffered|NTConsole.InputModes.Echo
                                : NTConsole.InputModes.None;
      DrawLines();
      inputMode = value;
    }
  }

  void AddLine(Color color, string line) { AddLine(color, line, true); }
  void AddLine(Color color, string line, bool redraw)
  { uncleared += LineHeight(line);
    if(!TextInput && uncleared >= LineSpace-1)
    { lines.Append(new Line(Color.Normal, "--more--"));
      DrawLines();
      SetCursorAtEOBL();
      while(true)
      { char c = ReadChar();
        if(c==' ' || c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) break;
      }
      SetCursorToPlayer();
      uncleared = 0;
    }
    lines.Append(new Line(color, line));
    while(lines.Count>maxLines) lines.Remove(lines.Head);
    if(redraw) DrawLines();
  }
  
  void AddWrapped(int i, string line)
  { if(wrapped.Length<i)
    { string[] narr = new string[wrapped.Length*2];
      Array.Copy(wrapped, narr, i);
      wrapped = narr;
    }
    wrapped[i] = line;
  }

  void AppendToBL(char c)
  { Line line = (Line)lines.Tail.Data;
    line.Text += c;
    lines.Tail.Data = line;
  }
  void AppendToBL(string s)
  { Line line = (Line)lines.Tail.Data;
    line.Text += s;
    lines.Tail.Data = line;
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

  void ClearScreen() // doesn't clear stat area
  { int width=Math.Min(MapWidth, console.Width), height=console.Height, mheight=MapHeight;
    console.Fill(0, 0, width, height); // clear map area
    console.Fill(0, mheight, console.Width, height-mheight); // clear message area
  }

  bool Diff(Creature player, Attr attr) { return player.GetAttr(attr)!=stats[(int)attr]; }
  bool Diff(Creature player, Attr attr1, Attr attr2) { return Diff(player, attr1) || Diff(player, attr2); }
  
  void DrawLines()
  { Line[] arr;
    { LinkedList.Node node = lines.Tail;
      int nlines = Math.Min(lines.Count, LineSpace), i=nlines-1, height;
      arr = new Line[nlines];
      while(i>=0)
      { Line line = (Line)node.Data;
        height = WordWrap(line.Text);
        while(height-->0 && i>=0) arr[i--] = new Line(line.Color, wrapped[height]);
        node=node.PrevNode;
      }
    }

    blY=MapHeight-1;
    console.Fill(0, MapHeight, console.Width, console.Height-MapHeight);
    console.SetCursorPosition(0, blY+1); // MapHeight

    for(int i=0; i<arr.Length; i++)
    { console.Attributes = ColorToAttr(arr[i].Color);
      console.WriteLine(arr[i].Text);
      blX = arr[i].Text.Length; blY++;
    }
    SetCursorToPlayer();
  }

  void DrawMenuItem(int y, MenuItem item, MenuFlag flags)
  { PutString(0, y, "[{0}] {1} - {2}",
              (flags&MenuFlag.AllowNum)==0 ?
                item.Count==0 ? "-" : "+" :
                item.Count==0 ? " - " : item.Count==item.Item.Count ? " + " : item.Count.ToString("d3"),
              item.Char, item.Item.FullName);
  }

  int LineHeight(string line) { return WordWrap(line); }

  char NormalizeDirChar()
  { char c = rec.Key.Char;
    if(rec.Key.VirtualKey>=NTConsole.Key.Numpad1 && rec.Key.VirtualKey<=NTConsole.Key.Numpad9)
    { c = dirLets[(int)rec.Key.VirtualKey-(int)NTConsole.Key.Numpad1];
      if(rec.Key.HasMod(NTConsole.Modifier.Shift)) c = char.ToUpper(c);
    }
    else switch(rec.Key.VirtualKey)
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

  void PutStringP(int width, int x, int y, string str) { PutStringP(Color.Normal, width, x, y, str); }
  void PutStringP(int width, int x, int y, string format, params object[] parms)
  { PutStringP(Color.Normal, width, x, y, string.Format(format, parms));
  }
  void PutStringP(Color color, int width, int x, int y, string str)
  { PutString(color, x, y, str);
    for(int i=str.Length; i<width; i++) console.WriteChar(' ');
  }
  void PutStringP(Color color, int width, int x, int y, string format, params object[] parms)
  { PutStringP(color, width, x, y, string.Format(format, parms));
  }

  char ReadChar() { return ReadChar(false); }
  char ReadChar(bool echo)
  { if(rec.Type==NTConsole.InputType.Keyboard && --rec.Key.RepeatCount<=0)
      rec.Type=NTConsole.InputType.BufferResize;
    while(rec.Type!=NTConsole.InputType.Keyboard || !rec.Key.KeyDown || rec.Key.Char==0 &&
          (rec.Key.VirtualKey>=NTConsole.Key.Shift && rec.Key.VirtualKey<=NTConsole.Key.Menu ||
           rec.Key.VirtualKey>=NTConsole.Key.Numlock))
      rec = console.ReadInput();
    if(echo && rec.Key.Char!=0) console.WriteChar(rec.Key.Char);
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
  { int x=MapWidth+2, y=0, xlines=0, width=console.Width-x;
    if(Diff(player, Attr.ExpLevel))
      PutStringP(width, x, y, "{0} the {1} (lv {2})", player.Name, player.Title, player.ExpLevel+1);
    PutStringP(width, x, y+1, "Human");
    if(Diff(player, Attr.HP, Attr.MaxHP))
    { int healthpct = player.HP*100/player.MaxHP;
      PutStringP(healthpct<25 ? Color.Dire : healthpct<50 ? Color.Warning : Color.Normal,
                width, x, y+2, "HP:   {0}/{1}", player.HP, player.MaxHP);
    }
    if(Diff(player, Attr.MaxHP, Attr.MaxMP))
    { int magicpct = player.MP*100/player.MaxMP;
      PutStringP(magicpct<25 ? Color.Dire : magicpct<50 ? Color.Warning : Color.Normal,
                 width, x, y+3, "MP:   {0}/{1}", player.MP, player.MaxMP);
    }
    if(Diff(player, Attr.AC)) PutStringP(width, x, y+4, "AC:   {0}", player.AC);
    if(Diff(player, Attr.EV)) PutStringP(width, x, y+5, "EV:   {0}", player.EV);
    if(Diff(player, Attr.Str)) PutStringP(width, x, y+6, "Str:  {0}", player.Str);
    if(Diff(player, Attr.Int)) PutStringP(width, x, y+7, "Int:  {0}", player.Int);
    if(Diff(player, Attr.Dex)) PutStringP(width, x, y+8, "Dex:  {0}", player.Dex);
    if(Diff(player, Attr.Gold)) PutStringP(width, x, y+9, "Gold: {0}", player.Gold);
    if(Diff(player, Attr.Exp)) PutStringP(width, x, y+10, "Exp:  {0}/{0}", player.Exp, player.NextExp);
    if(Diff(player, Attr.Age))
    { PutStringP(width, x, y+11, "Turn: {0}", player.Age);
      PutStringP(width, x, y+12, "Dungeon level {0}", App.CurrentLevel+1);
    }
    y += 13;
    if(hunger!=player.HungerLevel || playerFlags!=player.Flags)
    { if(player.HungerLevel==Hunger.Hungry) { PutStringP(Color.Warning, width, x, y++, "Hungry"); xlines++; }
      else if(player.HungerLevel==Hunger.Starving) { PutStringP(Color.Dire, width, x, y++, "Starving"); xlines++; }
      if(xlines<statLines) console.Fill(x, y, width, statLines-xlines);
    }
    UpdateStats(player);

    statLines=xlines;
  }

  void RestoreScreen()
  { if(buf!=null) console.PutBlock(0, 0, 0, 0, mapW, mapH, buf); // replace what we've overwritten
    DrawLines();
  }

  void SetCursorAtEOBL() { console.SetCursorPosition(blX, blY); }
  void SetCursorToPlayer() { console.SetCursorPosition(mapW/2, mapH/2); }
  
  void SetupMenu(System.Collections.ICollection items, MenuFlag flags, ItemClass[] classes)
  { if(items.Count==0) throw new ArgumentException("No items in the collection.", "items");
    if(items.Count>52 && (flags&MenuFlag.Reletter)==0)
      throw new NotSupportedException("Too many items in the collection.");

    Item[] itemarr = new Item[items.Count]; // first sort by character
    items.CopyTo(itemarr, 0);
    Array.Sort(itemarr, ItemComparer.Default);
    
    if(Array.IndexOf(classes, ItemClass.Any)!=-1)
    { menu = new MenuItem[items.Count];
      for(int i=0,mi=0; i<(int)ItemClass.NumClasses; i++) // then group by item class
        for(int j=0; j<itemarr.Length; j++) if(itemarr[j].Class==(ItemClass)i) menu[mi++] = new MenuItem(itemarr[j]);
    }
    else
    { System.Collections.ArrayList list = new System.Collections.ArrayList();
      for(int i=0; i<itemarr.Length; i++)
        if(Array.IndexOf(classes, itemarr[i].Class)!=-1) list.Add(new MenuItem(itemarr[i]));
      menu = (MenuItem[])list.ToArray(typeof(MenuItem));
    }
  }
  
  void ShowHelp()
  { ClearScreen();
    string helptext =
@"                   Chrono Help
a - use special ability     A - list abilities
c - close door              C - check experience
d - drop item(s)
e - eat food                E - evoke item power
f - fire weapon/attack
i - inventory listing
o - open door               O - dungeon overview
p - pray
q - quaff a potion
r - read scroll or book     R - remove a worn item
s - search adjacent tiles   S - manage skills
t - throw an item
u - use item
v - view item description   V - version info
w - wield an item           W - wear an item
x - examine surroundings    X - examine level map
z - zap a wand              Z - cast a spell
! - shout or command allies ' - wield item a/b
, - pick up item(s)         . - rest one turn
: - examine occupied tile   \ - check knowledge
< - ascend staircase        > - descend staircase
= - reassign item letters   ^ - describe religion
/ . - rest 100 turns        / DIR - long walk
Ctrl-A - toggle autopickup  Ctrl-Q - quit
Ctrl-P - see old messages   Ctrl-X - quit + save";

    console.SetCursorPosition(0, 0);
    NTConsole.OutputModes mode = console.OutputMode;
    console.OutputMode = NTConsole.OutputModes.Processed;
    console.Write(helptext);
    console.ReadChar();
    console.OutputMode = mode;
    RestoreScreen();
  }
  
  void UpdateStats(Creature player)
  { for(int i=0; i<(int)Attr.NumAttributes; i++) stats[i] = player.GetAttr((Attr)i);
    playerFlags = player.Flags;
    hunger = player.HungerLevel;
  }

  int WordWrap(string line)
  { int width=console.Width, s=0, e=line.Length, height=0;
    if(e==0) { AddWrapped(0, ""); return 1; }
    
    while(e-s>width)
    { for(int i=Math.Min(s+width, e)-1; i>=s; i--)
        if(char.IsWhiteSpace(line[i]))
        { AddWrapped(height++, line.Substring(s, i));
          s = i+1;
          goto next;
        }
      AddWrapped(height++, line.Substring(s, width));
      s += width;
      next:;
    }
    if(e-s>0) AddWrapped(height++, line.Substring(s, e-s));
    return height;
  }

  NTConsole.CharInfo[] buf;
  bool[] vis;
  MenuItem[] menu;
  string[] wrapped = new string[4];

  int[] stats = new int[(int)Attr.NumAttributes];
  Creature.Flag playerFlags;
  Hunger hunger;

  NTConsole console = new NTConsole();
  LinkedList lines = new LinkedList(); // a circular array would be more efficient...
  NTConsole.InputRecord rec;
  int  uncleared, maxLines=200, blX, blY, mapW, mapH, count, statLines;
  bool inputMode;

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
    else switch(tile.Type)
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