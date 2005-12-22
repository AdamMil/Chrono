using System;
using System.Collections;
using GameLib.Collections;
using Point=System.Drawing.Point;
using Rectangle=System.Drawing.Rectangle;

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

  struct Line
  { public Line(Color color, string text) { Color=color; Text=text; }
    public Color Color;
    public string Text;
  }

  public override string Ask(Color color, string prompt, bool allowEmpty, string rebuke)
  { prompt += " ";
    TextInput = true;
    if(rebuke==null) rebuke = "Please enter something!";
    while(true)
    { AddLine(color, prompt, true);
      MoveCursorToEOBL();
      string answer = console.ReadLine();
      if(answer!="" || allowEmpty)
      { AppendToBL(answer);
        TextInput = false;
        return answer;
      }
      AddLine(color, rebuke);
    }
  }

  public override char CharChoice(Color color, string prompt, string chars, char defaultChar, bool caseInsensitive,
                                  string rebuke)
  { prompt += (chars!=null ? " [" + chars + "] " : " ");
    TextInput = true;
    if(caseInsensitive) chars = chars.ToLower();
    if(rebuke==null) rebuke = "Invalid selection!";
    while(true)
    { AddLine(color, prompt, true);
      MoveCursorToEOBL();

      char c = ReadChar();
      if(c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) c = defaultChar;
      else if(IsPrintable(c)) Echo(c);

      int index = chars==null ? -1 : chars.IndexOf(caseInsensitive ? char.ToLower(c) : c);
      if(chars==null || c==defaultChar || index!=-1)
      { TextInput = false;
        return index==-1 ? c : chars[index];
      }

      AddLine(color, rebuke);
    }
  }

  public override Direction ChooseDirection(string prompt, bool allowSelf, bool allowVertical)
  { if(prompt==null) prompt = "Choose a direction";
    if(allowSelf || allowVertical)
    { prompt += " [dir, ";
      if(allowSelf) prompt += '5';
      if(allowVertical) prompt += "<>";
      prompt += "]:";
    }
    else prompt += ":";
    Direction d = CharToDirection(CharChoice(prompt, null), allowSelf, allowVertical);
    if(d==Direction.Invalid) Print("That's an odd direction!");
    return d;
  }

  public override MenuItem[] ChooseItem(string prompt, IKeyedInventory items, MenuFlag flags, params ItemType[] types)
  { bool any  = types.Length==(int)ItemType.NumTypes || Array.IndexOf(types, ItemType.Any) != -1;
    bool none = (flags&MenuFlag.AllowNothing) != 0;
    string chars = items.CharString(types);
    if(any && !none && chars.Length==0) return new MenuItem[0];

    if(chars.Length!=0 || !any) // if we have '?' or '*'
    { prompt += " [";
      if(none) prompt += "-";
      prompt = prompt + chars + " or ";
      if(chars.Length!=0) { chars += "?"; prompt += "?"; } // if there are items, allow them to be selected with a menu
      if(!any) { chars += "*"; prompt += "*"; } // if we allow the selection of "no item", add the character for that
      if(none) chars += "-";
    }
    else prompt = prompt + " [" + chars;

    prompt += "] ";

    TextInput = true;
    while(true)
    { int count = -1;

      AddLine(prompt, true);
      MoveCursorToEOBL();
      char c = ReadChar();
      if(c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) return new MenuItem[0]; // abort
      else if(IsPrintable(c)) Echo(c);

      if(char.IsDigit(c) && (flags&MenuFlag.AllowNum)!=0) // a count of items is specified
      { count = c-'0';
        int printed = 1;
        while(true)
        { c = ReadChar();
          if(c=='\b')
          { if(printed!=0) { Echo(c); printed--; count /= 10; }
            else { count=-1; break; }
          }
          else if(char.IsDigit(c))
          { int nc = c-'0' + count*10;
            if(nc>count) // guard against overflow
            { count=nc;
              Echo(c);
              printed++;
            }
          }
          else
          { if(IsPrintable(c)) Echo(c);
            break;
          }
        }
      }

      if(count==0 || chars.IndexOf(c)==-1) goto rebuke;

      Item item = items[c];
      if(count!=-1 && (char.IsLetter(c) || c=='$') && count>item.Count)
      { AddLine("You don't have that many!");
        continue;
      }

      TextInput = false;
      if(c=='-') return new MenuItem[1] { new MenuItem(null, 0) }; // the "no item" item
      if(c=='?') return Menu(items, flags, types);
      if(c=='*') return Menu(items, flags, ItemType.Any);
      return new MenuItem[1] { new MenuItem(item, count==-1 ? item.Count : count) };

      rebuke:
      AddLine("Invalid selection!");
    }
  }

  public override void DisplayInventory(IInventory inv, params ItemType[] types)
  { MenuItem[] menu = CreateMenu(inv, MenuFlag.None, types);

    int mtop=0, width=Math.Min(MapWidth, console.Width), height=console.Height, iheight=height-2;
    ClearScreen();

    while(true)
    { redraw:
      ItemType head = ItemType.Invalid;
      int top=mtop, y=0;
      for(; y<iheight && top<menu.Length; y++) // draw the menu items
      { if(menu[top].Item.Type != head)
        { head = menu[top].Item.Type;
          PutString(NTConsole.Attribute.White|NTConsole.Attribute.Underlined, 0, y, head.ToString());
        }
        else
        { PutString(0, y, "{0} - {1}", menu[top].Char, menu[top].Item.GetInvName());
          top++;
        }
      }

      PutString(0, y, top<menu.Length ? "--more--" : "--bottom--");

      while(true)
      { char c = ReadChar();
        if(c=='\r' || c=='\n' || c==' ') rec.Key.VirtualKey = NTConsole.Key.Next;
        switch(rec.Key.VirtualKey)
        { case NTConsole.Key.Prior: case NTConsole.Key.Up: case NTConsole.Key.Numpad8:
            if(mtop>0) // page up
            { mtop -= Math.Min(iheight, mtop);
              console.Fill(0, 0, width, height); // clear the area we'll be using
              goto redraw;
            }
            break;
          case NTConsole.Key.Next: case NTConsole.Key.Down: case NTConsole.Key.Numpad2:
            if(top<menu.Length) // page down
            { mtop += top-mtop;
              console.Fill(0, 0, width, height); // clear the area we'll be using
              goto redraw;
            }
            else goto done; // if already on the last page, then close the inventory
          case NTConsole.Key.Escape: goto done;
        }
      }
    }
    done:
    RestoreScreen();
  }

  public override void DisplayTileItems(IInventory items) { DisplayTileItems(items, true); }
  public override void ExamineTile(Player viewer, Point pt) { DescribeTile(viewer, pt, viewer.CanSee(pt)); }

  public override Input GetNextInput()
  { while(true)
    { Input inp = new Input();
      int count = 1;
      ReadChar();
      char c = NormalizeDirChar();
      
      if(c==0) continue;
      if(rec.Key.HasMod(NTConsole.Modifier.Ctrl))
        switch(c+64)
        { case 'N': inp.Action = Action.NameItem; break;
          case 'P': DisplayMessages(false); break;
          case 'Q': inp.Action = Action.Quit; break;
          case 'X': inp.Action = Action.Save; break;
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
          case 'a': inp.Action = Action.UseItem; break;
          case 'c': inp.Action = Action.CloseDoor; break;
          case 'd': inp.Action = Action.Drop; break;
          case 'D': inp.Action = Action.DropType; break;
          case 'e': inp.Action = Action.Eat; break;
          case 'f': inp.Action = Action.Fire; break;
          case 'i': inp.Action = Action.Inventory; break;
          case 'I': inp.Action = Action.Invoke; break;
          case 'o': inp.Action = Action.OpenDoor; break;
          case 'q': inp.Action = Action.Quaff; break;
          case 'r': inp.Action = Action.Read; break;
          case 'R': inp.Action = Action.Remove; break;
          case 'S': inp.Action = Action.ManageSkills; break;
          case 't': inp.Action = Action.Throw; break;
          case 'T': inp.Action = Action.TalkTo; break;
          case 'v': inp.Action = Action.ViewItem; break;
          case 'w': inp.Action = Action.Wield; break;
          case 'W': inp.Action = Action.Wear; break;
          case 'X': inp.Action = Action.ShowMap; break;
          case 'z': inp.Action = Action.ZapWand; break;
          case 'Z': inp.Action = Action.CastSpell; break;
          case '<': inp.Action = Action.GoUp; break;
          case '>': inp.Action = Action.GoDown; break;
          case '=': inp.Action = Action.Reassign; break;
          case ':': inp.Action = Action.ExamineTile; break;
          case '\'':inp.Action = Action.SwapAB; break;
          case '\\':inp.Action = Action.ShowKnowledge; break;
          case '/':
            inp.Direction = CharToDirection(ReadChar());
            if(inp.Direction==Direction.Self) { count=100; inp.Action=Action.Rest; }
            else if(inp.Direction!=Direction.Invalid) inp.Action = Action.MoveToInteresting;
            break;
          case '?': DisplayHelp(); break;
        }

      if(inp.Action != Action.Invalid)
      { inp.Count = count;
        return inp;
      }
    }
  }

  public override void ManageSkills(Player player)
  { ClearScreen();
    console.SetCursorPosition(0, 0);
    console.WriteLine("You have {0} points of unallocated experience.", player.ExpPool);

    Skill[] skills = new Skill[(int)Skill.NumSkills]; // fill an array with the skills in which we have some experience
    int numSkills=0;
    for(int i=0; i<(int)Skill.NumSkills; i++) if(player.GetSkill((Skill)i)!=0) skills[numSkills++] = (Skill)i;

    console.WriteLine(numSkills==0 ? "You are completely unskilled!" : "Select a skill to toggle training it.");

    while(true)
    { char c = 'a';
      console.SetCursorPosition(0, 3);
      for(int i=0; i<numSkills; i++)
      { bool enabled = player.Training(skills[i]);
        Write(enabled ? Color.Normal : Color.DarkGrey, "{0} {1} {2} Skill {3} ",
              c++, enabled ? '+' : '-', skills[i].ToString().PadRight(18), player.GetSkill(skills[i]));
        // TODO: not sure if i want this
        // WriteLine(Color.LightBlue, "({0})", player.SkillExp[(int)skills[i]]*9/player.NextSkillLevel(skills[i])+1);
      }

      nextChar:
      c = ReadChar();
      if(c==' ' || c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) break;
      else if(c>='a' && c-'a'<numSkills) player.SetTraining(skills[c-'a'], !player.Training(skills[c-'a']));
      else goto nextChar;
    }

    RestoreScreen();
  }

  public override MenuItem[] Menu(System.Collections.ICollection items, MenuFlag flags, params ItemType[] types)
  { return Menu(CreateMenu(items, flags, types), flags);
  }

  public override MenuItem[] Menu(MenuItem[] items, MenuFlag flags)
  { MenuItem[] ret;

    bool reletter = (flags&MenuFlag.Reletter)!=0, allownum=(flags&MenuFlag.AllowNum)!=0;

    // width and height are the total height of the menu area.
    // iheight is the height of the area displaying items. mtop is the index of the first visible item
    int width = Math.Min(MapWidth, console.Width), height=reletter ? Math.Min(54, console.Height) : console.Height,
      iheight = height-2, mtop = 0;

    while(true)
    { redraw:
      ClearScreen(); // clear the area we'll be using
      ItemType head = ItemType.Invalid;
      
      int top=mtop, y=0;
      char c = 'a';
      
      for(; y<iheight && top<items.Length; y++)
      { if(items[top].Item!=null && items[top].Item.Type!=head) // if it's the start of a new category
        { head = items[top].Item.Type;
          PutString(NTConsole.Attribute.White|NTConsole.Attribute.Underlined, 0, y, head.ToString());
        }
        else // otherwise, draw an item within a category
        { if(reletter) items[top].Char = c;
          DrawMenuItem(y, items[top++], flags);
          if(c++=='z') c = 'A';
        }
      }

      PutString(0, y, "Enter selection: ");
      
      while(true) // key handling
      { int count = -1;

        c = ReadChar();
        if(allownum && char.IsDigit(c))
        { count = c-'0';
          while(true)
          { c = ReadChar();
            if(char.IsDigit(c))
            { int nc = c-'0' + count*10;
              if(nc>count) count = nc; // guard against overflow
            }
            else if(c=='\b')
            { if(count==0) { count=-1; break; }
              count /= 10;
            }
            else break;
          }
        }

        if(char.IsLetter(c)) // (possibly) (de-)selecting an item
        { head = ItemType.Invalid;
          for(int i=mtop,yo=0; i<top; yo++,i++)
          { if(items[i].Item!=null && head!=items[i].Item.Type)
            { head = items[i].Item.Type;
              yo++;
            }
            if(items[i].Char==c)
            { items[i].Count = count!=-1 ? items[i].Item==null ? count : Math.Min(count, items[i].Item.Count)
                                         : items[i].Item==null ? items[i].Count==0 ? 1 : 0
                                                               : items[i].Count!=0 ? 0 : items[i].Item.Count;
              if((flags&MenuFlag.Multi)==0 && items[i].Count!=0) goto done;
              DrawMenuItem(yo, items[i], flags); // update the menu item
              console.SetCursorPosition(17, y);  // and then put the cursor back after "Enter selection: "
              break;
            }
          }
        }
        else // not selecting an item
        { switch(c)
          { case ',': // toggle all onscreen items
              bool select = false; // by default, deselect everything
              for(int i=mtop; i<top; i++) // but if not all items are selected, then we'll select them all
                if(items[i].Count!=(items[i].Item==null ? 1 : items[i].Item.Count)) { select = true; break; }
              for(int i=mtop; i<top; i++)
                items[i].Count = select ? (items[i].Item==null ? 1 : items[i].Item.Count) : 0;
              goto redraw;
            case '+': case '=': // select all onscreen items
              for(int i=mtop; i<top; i++) items[i].Count = items[i].Item==null ? 1 : items[i].Item.Count;
              goto redraw;
            case '-': // deselect all onscreen items
              for(int i=mtop; i<top; i++) items[i].Count = 0;
              goto redraw;
            case '<': rec.Key.VirtualKey = NTConsole.Key.Prior; break; // aliases for page up and page down
            case '>': case ' ': case '\r': case '\n': rec.Key.VirtualKey = NTConsole.Key.Next; break;
          }

          switch(rec.Key.VirtualKey)
          { case NTConsole.Key.Prior: case NTConsole.Key.Up: case NTConsole.Key.Numpad8: // page up
              if(mtop!=0) // if we're not at the top already
              { mtop -= Math.Min(iheight, mtop);
                console.Fill(0, 0, width, height);
                goto redraw;
              }
              break;
            case NTConsole.Key.Next: case NTConsole.Key.Down: case NTConsole.Key.Numpad2: // page down
              if(top<items.Length) // if we're not at the bottom already, scroll down
              { mtop += top-mtop;
                console.Fill(0, 0, width, height);
                goto redraw;
              }
              else goto done; // otherwise return the selected items
            case NTConsole.Key.Escape: ret = new MenuItem[0]; goto doReturn; // abort
          }
        }
      }
    }
    
    done:
    iheight = 0; // reuse this to count the number of selected items
    for(int i=0; i<items.Length; i++) if(items[i].Count!=0) iheight++;
    ret = new MenuItem[iheight];
    for(int i=0,mi=0; i<items.Length; i++) // now add them to the return array
      if(items[i].Count!=0)
      { if(items[i].Item!=null) items[i].Char = items[i].Item.Char;
        ret[mi++] = items[i];
      }
    
    doReturn:
    RestoreScreen();
    return ret;
  }

  public override void Print(Color color, string line) { AddLine(color, line, true); }

  public override void Render(Player viewer, bool updateMap)
  { mapW = Math.Min(console.Width, MapWidth);
    mapH = Math.Min(console.Height, MapHeight);
    if(updateMap) RenderMap(viewer, viewer.Pos, viewer.VisibleTiles());
    DrawStats(viewer);
    unviewed = 0;
    DrawLines();
  }

  public override void SetTitle(string title) { console.Title = title; }

  public override bool YesNo(Color color, string prompt, bool defaultYes)
  { prompt += " [yes/no]";
    while(true)
    { string str = Ask(color, prompt, true, "Please enter 'yes' or 'no'.");
      if(str=="") { AppendToBL(defaultYes ? "yes" : "no"); return defaultYes; }
      str = str.ToLower();
      if(str=="yes") return true;
      else if(str=="no") return false;
      else AddLine(color, "Please enter 'yes' or 'no'.");
    }
  }

  public override bool YN(Color color, string prompt, bool defaultYes)
  { char c = CharChoice(color, prompt, defaultYes ? "Yn" : "yN", defaultYes ? 'y' : 'n', true,
                        "Please enter 'y' or 'n'.");
    return c==0 ? defaultYes : Char.ToLower(c)=='y';
  }

  int LineSpace { get { return console.Height-MapHeight-4; } } // the amount of space available for text history
  int MapWidth  { get { return console.Width; } } // the width of the map
  int MapHeight { get { return Math.Max(console.Height-19, 36); } } // the height of the map

  bool TextInput // returns true if we're set up to read text
  { get { return inputMode; }
    set
    { if(value==inputMode) return;
      console.InputMode = value ? NTConsole.InputModes.LineBuffered|NTConsole.InputModes.Echo
                                : NTConsole.InputModes.None;
      DrawLines();
      unviewed  = 0;
      inputMode = value;
    }
  }

  void AddLine(string line) { AddLine(Color.Normal, line, false); }
  void AddLine(string line, bool redrawNow) { AddLine(Color.Normal, line, redrawNow); }
  void AddLine(Color color, string line) { AddLine(color, line, false); }
  void AddLine(Color color, string line, bool redrawNow)
  { if(unviewed!=-1) unviewed += LineHeight(line); // if we're not skipping, add to the number of unviewed lines

    if(!TextInput && unviewed>=LineSpace-1) // if we're not reading text and we have more lines that can fit at once...
    { lines.Append(new Line(Color.Normal, "--more--"));
      DrawLines();
      MoveCursorToEOBL();
      while(true)
      { char c = ReadChar();
        if(c==' ' || c=='\r' || c=='\n') { unviewed=0; break; }
        if(rec.Key.VirtualKey==NTConsole.Key.Escape) { unviewed=-1; break; } // -1 means skip all pending lines
      }
      MoveCursorToPlayer();
    }

    lines.Append(new Line(color, line));
    while(lines.Count>maxLines) lines.Remove(lines.Head); // if the scrollback buffer is overfilled, cut it down to size
    if(redrawNow) DrawLines();
  }

  // add a line to the 'wrapped' array, at the given index, expanding the array if necessary
  void AddWrapped(int index, string line)
  { if(wrapped.Length<index)
    { string[] narr = new string[wrapped.Length*2];
      Array.Copy(wrapped, narr, index);
      wrapped = narr;
    }
    wrapped[index] = line;
  }

  void AppendToBL(string s)
  { Line line = (Line)lines.Tail.Data;
    line.Text += s;
    lines.Tail.Data = line;
  }

  void BLBackspace()
  { Line line = (Line)lines.Tail.Data;
    if(line.Text.Length!=0) line.Text = line.Text.Substring(0, line.Text.Length-1);
    lines.Tail.Data = line;
  }

  // convert movement characters into the Direction enum
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

  void ClearScreen()
  { int width=Math.Min(MapWidth, console.Width), height=console.Height, mheight=MapHeight;
    console.Fill(0, 0, width, height); // clear map area
    console.Fill(0, mheight, console.Width, height-mheight); // clear message area
    console.Attributes = ColorToAttr(Color.Normal);
  }

  MenuItem[] CreateMenu(ICollection items, MenuFlag flags, ItemType[] types)
  { if(items.Count==0) throw new ArgumentException("No items in the collection.", "items");
    if(types.Length==0) throw new ArgumentException("No types passed.", "types");
    if(items.Count>52 && (flags&MenuFlag.Reletter)==0)
      throw new NotSupportedException("Too many items in the collection.");

    Item[] itemarr = new Item[items.Count]; // first sort items by type and character
    items.CopyTo(itemarr, 0);
    Array.Sort(itemarr, ItemComparer.ByTypeAndChar);

    MenuItem[] ret;
    if(Array.IndexOf(types, ItemType.Any)!=-1) // if ItemType.Any was passed, add every item to the menu
    { ret = new MenuItem[items.Count];
      for(int i=0; i<itemarr.Length; i++) ret[i] = new MenuItem(itemarr[i]);
    }
    else // otherwise, use only the item types that were passed
    { Array.Sort(types); // sort the array of types

      ArrayList list = new ArrayList();
      for(int i=0,j=0; i<types.Length; i++)
      { ItemType type = types[i];
        while(j<itemarr.Length && itemarr[j].Type!=type) j++;
        for(; j<itemarr.Length && itemarr[j].Type==type; j++) list.Add(new MenuItem(itemarr[j]));
      }

      ret = (MenuItem[])list.ToArray(typeof(MenuItem));
    }
    
    return ret;
  }

  void DescribeTile(Player viewer, Point pt, bool visible) { DescribeTile(viewer, pt, visible, true); }
  void DescribeTile(Player viewer, Point pt, bool visible, bool clearLines)
  { if(clearLines) { lines.Clear(); unviewed=0; }

    if(!visible)
    { if(!viewer.Memory.GetFlag(pt, TileFlag.Seen)) { AddLine("You can't see that position.", true); return; }
      AddLine("You can't see that position, but here's what you remember:");
    }

    Map map = visible ? viewer.Map : viewer.Memory;

    AddLine(map[pt].Type.ToString()+'.');
    Entity e = map.GetEntity(pt);
    if(e!=null)
    { string prefix = e==viewer ? "You are" : "{0} is";
      AddLine(string.Format(prefix+" here.", e.AName));

      /* TODO: finish this Weapon w = e.Weapon;
      if(w!=null) AddLine(string.Format(prefix+" wielding {1} {2}.", "It", Global.AorAn(w.Name), w.Name));
      */

      if(e!=viewer)
      { string name = e.Name==null ? "It" : e.Name;
        int healthpct = e.HP*100/e.MaxHP;
        if(healthpct>=90) AddLine(name+" looks healthy.");
        else if(healthpct>=75) AddLine(name+" looks slightly wounded.");
        else if(healthpct>=50) AddLine(name+" looks wounded.");
        else if(healthpct>=25) AddLine(name+" looks heavily wounded.");
        else AddLine(name+" looks almost dead.");

        /* TODO: this too if(e is AI)
          switch(((AI)e).State)
          { case AIState.Asleep: AddLine(name+" appears to be asleep."); break;
            case AIState.Attacking: AddLine(name+" looks angry!"); break;
            case AIState.Escaping: AddLine(name+" looks frightened."); break;
            case AIState.Guarding: AddLine(name+" looks alert."); break;
            case AIState.Following: AddLine(name+" appears to be following you."); break;
            case AIState.Idle: case AIState.Patrolling: case AIState.Wandering: AddLine(name+" looks bored."); break;
            case AIState.Working: AddLine(name+"'s busy working."); break;
            default: AddLine("UNKNOWN AI STATE"); break;
          }
        */
      }
    }

    if(map.HasItems(pt)) DisplayTileItems(map[pt].Items, visible);
    DrawLines();
    unviewed = 0;
  }

  void DisplayHelp()
  { ClearScreen();
    string helptext =
@"                                  Chrono Help
a - use/apply item
c - close door              C - carve up a corpse
d - drop item(s)            D - drop items by type
e - eat food
f - fire weapon/attack      (item 'q' is preferred ammunition)
i - inventory listing       I - invoke a wielded item's power
o - open door
m - use special ability     M - list special abilities/mutations
p - pray
q - quaff a potion          Q - display quests
r - read a scroll or book   R - remove a worn item
s - search adjacent tiles   S - manage skills
t - throw an item           T - talk to somebody
v - view item description   V - version info
w - wield a weapon          W - wear an item
x - examine surroundings    X - examine level map
z - zap a wand              Z - cast a spell
! - shout or command allies ' - toggle between wielding items a and b
, - pick up item(s)         . - rest one turn
: - examine occupied tile   \ - display known items
< - ascend staircase        > - descend staircase
= - reassign item letters   ^ - describe religion
b, h, j, k, l, n, u, y - movement (in addition to the keypad)
/ DIR  - long walk          / .    - rest up to 100 turns
Ctrl-A - toggle autopickup  Ctrl-Q - quit
Ctrl-N - name an item       Ctrl-X - quit + save
Ctrl-P - see old messages";

    console.SetCursorPosition(0, 0);
    NTConsole.OutputModes mode = console.OutputMode;
    console.OutputMode = NTConsole.OutputModes.Processed;
    console.Attributes = ColorToAttr(Color.Normal);
    console.Write(helptext);
    ReadChar();
    console.OutputMode = mode;
    RestoreScreen();
  }

  void DisplayMessages(bool fromTop)
  { int width=MapWidth, height=console.Height, ls, lss;
    ArrayList list = new ArrayList();
    for(LinkedList.Node node=lines.Head; node!=null; node=node.Next)
    { Line line = (Line)node.Data;
      if(line.Text=="--more--") continue;
      int lh = WordWrap(line.Text, width);
      for(int i=0; i<lh; i++) list.Add(new Line(line.Color, wrapped[i]));
    }
    lss = Math.Max(0, list.Count-height);
    ls  = fromTop ? 0 : lss;

    ClearScreen();
    while(true)
    { for(int y=0,i=ls; i<list.Count && y<height; y++,i++)
      { Line line = (Line)list[i];
        PutString(line.Color, width, 0, y, line.Text);
      }

      nextChar: ReadChar();
      if(rec.Key.VirtualKey==NTConsole.Key.Escape) goto done;
      if(rec.Key.VirtualKey==NTConsole.Key.Prior) ls = Math.Max(0, ls-height*2/3);
      else if(rec.Key.VirtualKey==NTConsole.Key.Next) ls = Math.Min(lss, ls+height*2/3);
      else
        switch(char.ToLower(NormalizeDirChar()))
        { case 'j': if(ls<lss) ls++; break;
          case 'k': if(ls>0)   ls--; break;
          case ' ': case '\r': case '\n': goto done;
          default: goto nextChar;
        }
    }

    done: RestoreScreen();
  }

  void DisplayTileItems(IInventory items, bool visible)
  { if(items==null || items.Count==0) return;
    if(items.Count==1) AddLine((visible ? "You see here: " : "You saw here: ")+items[0].GetAName());
    else
    { int needed=items.Count+1, avail=LineSpace-unviewed-2;
      if(needed>avail)
      { // TODO: perhaps allow this
        /*if(allowInventory) DisplayInventory(items);
        else */if(avail<=2) AddLine("There are several items here.");
        else
        { int toshow = avail-2;
          AddLine(visible ? "You see here:" : "You saw here:");
          for(int i=0; i<toshow; i++) AddLine(items[i].GetAName());
          AddLine("There are more items as well.");
        }
      }
      else
      { AddLine(visible ? "You see here:" : "You saw here");
        needed--;
        for(int i=0; i<needed; i++) AddLine(items[i].GetAName());
      }
    }
  }

  void DrawLines()
  { int maxlines=LineSpace, li=maxlines-1;

    Line[] arr = new Line[maxlines]; // allocate enough lines to cover the maximum available text space
    { LinkedList.Node node = lines.Tail; // traverse through the list, wordwrapping each entry and filling the array
      while(li>=0 && node!=null)
      { Line line = (Line)node.Data;
        int height = WordWrap(line.Text);
        while(height-->0 && li>=0) arr[li--] = new Line(line.Color, wrapped[height]);
        node = node.Previous;
      }
    }

    blY = console.Height-LineSpace-1;
    console.Fill(0, blY, console.Width, maxlines); // clear the area into which we'll be writing
    console.SetCursorPosition(0, blY);

    for(blY--,li++; li<arr.Length; li++) // write out the lines
    { console.Attributes = ColorToAttr(arr[li].Color);
      console.WriteLine(arr[li].Text);
      blX = arr[li].Text.Length; blY++; // update the coordinate of the end of the bottom line
    }

    MoveCursorToPlayer();
  }

  void DrawMenuItem(int y, MenuItem item, MenuFlag flags)
  { PutString(0, y, "[{0}] {1} - {2}",
              (flags&MenuFlag.AllowNum)==0 ?
                item.Count==0 ? "-" : "+" :
                item.Count==0 ? " - " :
                item.Count==item.Item.Count ? " + " :
                item.Count>999 ? "###" : item.Count.ToString("d3"),
              item.Char, item.Text!=null ? item.Text : item.Item.GetInvName());
  }

  void DrawStats(Player player)
  { int x=0, y=MapHeight, width=console.Width-x;
    console.SetCursorPosition(x, y);
    x += Write(Color.Normal, "{0} the {1}  St:{2} Dx:{3} In:{4} AC:{5} EV:{6} ",
               player.Name, player.Title, player.Str, player.Dex, player.Int, player.AC, player.EV);

    while(x++<width) console.WriteChar(' ');
    x = 0; y++;
    console.SetCursorPosition(x, y);

    x += Write(Color.Normal, "Dlvl:{0} $:{1} ", player.Map.Index+1, player.Gold);
    int percent = player.MaxHP==0 ? 0 : player.HP*100/player.MaxHP;
    x += Write(percent<25 ? Color.Dire : percent<50 ? Color.Warning : percent<75 ? Color.Yellow :
               percent<100 ? Color.Green : Color.Normal, "HP:{0}/{1} ", player.HP, player.MaxHP);
    percent = player.MaxMP==0 ? 0 : player.MP*100/player.MaxMP;
    x += Write(percent<25 ? Color.Dire : percent<50 ? Color.Warning : Color.Normal, "MP:{0}/{1} ",
               player.MP, player.MaxMP);
    x += Write(Color.Normal, "Exp:{0} ({1}/{2},{3}) T:{4}",
               player.XL, player.XP, player.NextXL, player.ExpPool, player.Turns);

    while(x++<width) console.WriteChar(' ');
    x = 0; y++;
    console.SetCursorPosition(x, y);

    switch(player.HungerLevel)
    { case HungerLevel.Fainting: x += Write(Color.Dire, "Fainting "); break;
      case HungerLevel.Hungry: x += Write(Color.Warning, "Hungry "); break;
      case HungerLevel.Satiated: case HungerLevel.Stuffed: x += Write(Color.Normal, "Satiated "); break;
      case HungerLevel.Weak: x += Write(Color.Dire, "Weak "); break;
    }

    int amount = player.GetEffectValue(EffectType.Sickness);
    if(amount!=0) x += Write(amount>1 ? Color.Dire : Color.Warning, "Sick ");

    switch(player.CarryStress)
    { case CarryStress.Burdened: x += Write(Color.Normal, "Burdened "); break;
      case CarryStress.Overloaded: x += Write(Color.Dire, "Overloaded "); break;
      case CarryStress.Overtaxed: x += Write(Color.Dire, "Overtaxed "); break;
      case CarryStress.Strained: x += Write(Color.Dire, "Strained "); break;
      case CarryStress.Stressed: x += Write(Color.Warning, "Stressed "); break;
    }

    if(player.HasAilment(Ailment.Blind)) x += Write(Color.Warning, "Blind ");
    if(player.HasAilment(Ailment.Confused)) x += Write(Color.Warning, "Conf ");
    if(player.HasAilment(Ailment.Hallucinating)) x += Write(Color.Warning, "Halluc ");
    if(player.HasAilment(Ailment.Stunned)) x += Write(Color.Warning, "Stun ");

    while(x++<width) console.WriteChar(' ');
    /*x = 0; y++;
    console.SetCursorPosition(x, y);

    if(player.Hands!=null)
      for(int i=0; i<player.Hands.Length; i++)
        if(player.Hands[i]!=null && (i==0 || player.Hands[i]!=player.Hands[i-1]))
        { Item item = player.Hands[i];
          x += Write(item.Type==ItemType.Weapon ? Color.LightCyan : Color.LightGreen, "{0}:{1} ",
                    item.Char, item.GetFullName());
        }
    while(x++<width) console.WriteChar(' ');*/
  }

  void Echo(char c)
  { console.WriteChar(rec.Key.Char);
    if(c=='\b') BLBackspace();
    else AppendToBL(c.ToString());
  }

  int LineHeight(string line) { return WordWrap(line); } // this is not the best implementation...

  void MoveCursorToEOBL() { console.SetCursorPosition(blX, blY); }
  void MoveCursorToPlayer() { console.SetCursorPosition(mapW/2, mapH/2); }

  char NormalizeDirChar() // convert keypad movement to character movement
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

  void PutString(int width, int x, int y, string str) { PutString(Color.Normal, width, x, y, str); }
  void PutString(int width, int x, int y, string format, params object[] parms)
  { PutString(Color.Normal, width, x, y, string.Format(format, parms));
  }
  void PutString(Color color, int width, int x, int y, string str)
  { if(str.Length>width) str = str.Substring(0, width);
    PutString(color, x, y, str);
    for(int i=str.Length; i<width; i++) console.WriteChar(' ');
  }
  void PutString(Color color, int width, int x, int y, string format, params object[] parms)
  { PutString(color, width, x, y, string.Format(format, parms));
  }

  char ReadChar() { return ReadChar(false); }
  char ReadChar(bool echo)
  { if(rec.Type==NTConsole.InputType.Keyboard && --rec.Key.RepeatCount<=0)
      rec.Type=NTConsole.InputType.BufferResize;
    while(rec.Type!=NTConsole.InputType.Keyboard || !rec.Key.KeyDown || rec.Key.Char==0 &&
          (rec.Key.VirtualKey>=NTConsole.Key.Shift && rec.Key.VirtualKey<=NTConsole.Key.Menu ||
           rec.Key.VirtualKey>=NTConsole.Key.Numlock))
      rec = console.ReadInput(); // skip non-character inputs
    if(echo && rec.Key.Char!=0) console.WriteChar(rec.Key.Char);
    return rec.Key.Char;
  }

  void RenderEntities(ICollection coll, Point[] vpts, Rectangle rect)
  { foreach(Entity c in coll)
    { if(!seeInvisible && c.HasAbility(Ability.SeeInvisible)) continue; // TODO: clairvoyance, warning, etc
      Point cp = c.Pos;
      int bi = (c.Y-rect.Y)*rect.Width + c.X-rect.X;
      if(!rect.Contains(cp) || !vis[bi]) continue;
      for(int i=0; i<vpts.Length; i++) if(vpts[i]==cp) { buf[bi] = EntityToChar(c, vis[bi]); break; }
    }
  }

  void RenderMap(Player viewer, Point center, Point[] vpts)
  { Rectangle rect = new Rectangle(center.X-mapW/2, center.Y-mapH/2, mapW, mapH);
    int size = mapW*mapH;
    bool showAll = viewer.Map.Type==MapType.Overworld;
    if(buf==null || buf.Length<size) buf = new NTConsole.CharInfo[size];
    if(vis==null || vis.Length<size) vis = new bool[size];

    seeInvisible = viewer.HasAbility(Ability.SeeInvisible); // TODO: take clairvoyance, etc into account

    Map map = viewer.Memory==null ? viewer.Map : viewer.Memory;
    if(map==viewer.Map)
    { for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++)
          buf[i] = TileToChar(map[x,y], true);
      unsafe { fixed(bool* visp=vis) GameLib.Interop.Unsafe.Fill(visp, 1, size); }
      RenderEntities(map.Entities, vpts, rect);
    }
    else
    { Array.Clear(vis, 0, size);
      for(int i=0; i<vpts.Length; i++)
        if(rect.Contains(vpts[i])) vis[(vpts[i].Y-rect.Y)*rect.Width+vpts[i].X-rect.X] = true;
      for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++)
        { Tile tile = map[x,y];
          buf[i] = tile.Type==TileType.UpStairs || tile.Type==TileType.DownStairs || tile.Entity==null ?
                    TileToChar(tile, showAll || vis[i]) : EntityToChar(tile.Entity, vis[i]);
        }
      map = viewer.Map;
      for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++) if(vis[i]) buf[i] = TileToChar(map[x,y], showAll || vis[i]);
      RenderEntities(map.Entities, vpts, rect);
    }

    console.PutBlock(new Rectangle(0, 0, rect.Width, rect.Height), buf);
  }

  void RestoreScreen()
  { if(buf!=null) console.PutBlock(0, 0, 0, 0, mapW, mapH, buf); // replace what we've overwritten
    DrawStats(App.Player);
    DrawLines();
  }

  // wordwrap a string, placing the output into 'wrapped' and returning the height in lines
  int WordWrap(string line) { return WordWrap(line, console.Width-1); }
  int WordWrap(string line, int width)
  { int s=0, e=line.Length, height=0;
    if(e==0) { AddWrapped(0, ""); return 1; }

    while(e-s>width)
    { for(int i=Math.Min(s+width, e)-1; i>=s; i--)
        if(char.IsWhiteSpace(line[i]))
        { AddWrapped(height++, line.Substring(s, i-s));
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

  int Write(Color color, string format, params object[] parms) { return Write(color, string.Format(format, parms)); }
  int Write(Color color, string str)
  { console.Attributes = ColorToAttr(color);
    console.Write(str);
    return str.Length;
  }

  NTConsole console = new NTConsole();
  LinkedList lines = new LinkedList();
  string[] wrapped = new string[4];
  NTConsole.CharInfo[] buf;
  bool[] vis;
  NTConsole.InputRecord rec;
  int unviewed, maxLines=200, blX, blY, mapW, mapH;
  bool inputMode, seeInvisible;

  static NTConsole.Attribute ColorToAttr(Color color)
  { NTConsole.Attribute attr = NTConsole.Attribute.Black;
    if((color & Color.Red)    != Color.Black) attr |= NTConsole.Attribute.Red;
    if((color & Color.Green)  != Color.Black) attr |= NTConsole.Attribute.Green;
    if((color & Color.Blue)   != Color.Black) attr |= NTConsole.Attribute.Blue;
    if((color & Color.Bright) != Color.Black) attr |= NTConsole.Attribute.Bright;
    return attr;
  }

  static NTConsole.CharInfo EntityToChar(Entity e, bool visible)
  { EntityClass ec = e.Class;
    return new NTConsole.CharInfo(raceMap[(int)ec.GetRace(e)],
                                  visible ? ColorToAttr(ec.GetColor(e)) : NTConsole.Attribute.DarkGrey);
  }

  static bool IsPrintable(char c) { return char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c); }

  static NTConsole.CharInfo ItemToChar(Item item)
  { ItemClass ic = item.Class;
    return new NTConsole.CharInfo(itemMap[(int)ic.Type], ColorToAttr(ic.Color));
  }

  static NTConsole.CharInfo TileToChar(Tile tile, bool visible)
  { NTConsole.CharInfo ci;
    if((visible || tile.Type!=TileType.UpStairs && tile.Type!=TileType.DownStairs) &&
       tile.Items!=null && tile.Items.Count>0)
      ci = ItemToChar(tile.Items[0]);
    else switch(tile.Type)
    { case TileType.Wall:       ci = new NTConsole.CharInfo('#', NTConsole.Attribute.Brown); break;
      case TileType.ClosedDoor: ci = new NTConsole.CharInfo('+', NTConsole.Attribute.Yellow); break;
      case TileType.OpenDoor:   ci = new NTConsole.CharInfo((char)0xFE, NTConsole.Attribute.Yellow); break;
      case TileType.RoomFloor:  ci = new NTConsole.CharInfo((char)0xFA, NTConsole.Attribute.Grey); break;
      case TileType.Corridor:   ci = new NTConsole.CharInfo((char)0xB0, NTConsole.Attribute.Grey); break;
      case TileType.UpStairs:
        return new NTConsole.CharInfo('<', visible ? NTConsole.Attribute.Grey : NTConsole.Attribute.LightBlue);
      case TileType.DownStairs:
        return new NTConsole.CharInfo('>', visible ? NTConsole.Attribute.Grey : NTConsole.Attribute.Red);
      case TileType.ShallowWater: ci = new NTConsole.CharInfo((char)0xF7, NTConsole.Attribute.Cyan); break;
      case TileType.DeepWater: ci = new NTConsole.CharInfo((char)0xF7, NTConsole.Attribute.Blue); break;
      case TileType.Ice: ci = new NTConsole.CharInfo((char)'/', NTConsole.Attribute.White); break;
      case TileType.Lava: ci = new NTConsole.CharInfo((char)0xF7, NTConsole.Attribute.LightRed); break;
      case TileType.Pit: ci = new NTConsole.CharInfo('o', NTConsole.Attribute.Grey); break;
      case TileType.Hole: ci = new NTConsole.CharInfo('O', NTConsole.Attribute.Grey); break;
      case TileType.Altar: ci = new NTConsole.CharInfo('_', NTConsole.Attribute.Grey); break;
      case TileType.Tree: case TileType.Forest: ci = new NTConsole.CharInfo('T', NTConsole.Attribute.Green); break;
      case TileType.DirtSand: ci = new NTConsole.CharInfo((char)0xFA, NTConsole.Attribute.Brown); break;
      case TileType.Grass: ci = new NTConsole.CharInfo('\"', NTConsole.Attribute.LightGreen); break;
      case TileType.Hill: ci = new NTConsole.CharInfo('^', NTConsole.Attribute.Brown); break;
      case TileType.Mountain: ci = new NTConsole.CharInfo('^', NTConsole.Attribute.White); break;
      case TileType.Road: ci = new NTConsole.CharInfo((char)0xB0, NTConsole.Attribute.Brown); break;
      case TileType.Town:
        return new NTConsole.CharInfo('o', visible ? NTConsole.Attribute.Grey : NTConsole.Attribute.LightRed);
      case TileType.Portal: return new NTConsole.CharInfo('\\', NTConsole.Attribute.Yellow);
      default: ci = new NTConsole.CharInfo(' ', NTConsole.Attribute.Black); break;
    }
    if(!visible && tile.Type!=TileType.ClosedDoor) ci.Attributes = NTConsole.Attribute.DarkGrey;
    //ci.Attributes |= NTConsole.ForeToBack((NTConsole.Attribute)((tile.Sound*15+Map.MaxSound-1)/Map.MaxSound));
    //ci.Attributes |= NTConsole.ForeToBack((NTConsole.Attribute)((tile.Scent*15+Map.MaxScent-1)/Map.MaxScent));
    return ci;
  }

  // Gold, Amulet, Weapon, Shield, Armor, Ammo, Food, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container, Treasure,
  static readonly char[] itemMap = new char[(int)ItemType.NumTypes]
  { '$', '"', ')', '[', '[', '(', '%', '?', '=', '!', '/', ']', '+', ']', '*',
  };

  // Human
  static readonly char[] raceMap = new char[(int)Race.NumRaces]
  { '@'
  };

  // convert keypad keys to movement letters
  static readonly char[] dirLets = new char[9] { 'b', 'j', 'n', 'h', '.', 'l', 'y', 'k', 'u' };
}

} // namespace Chrono