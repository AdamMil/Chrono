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
    { if(doRebuke) AddLine(color, rebuke);
      AddLine(color, sprompt, true);
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
    { if(doRebuke) AddLine(color, rebuke);
      AddLine(color, sprompt, true);
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
      if(doRebuke) AddLine("Invalid selection!");
      AddLine(sprompt, true);
      SetCursorAtEOBL();
      char c = ReadChar(true);
      if(c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) return new MenuItem[0];
      else AppendToBL(c);
      if(char.IsDigit(c) && (flags&MenuFlag.AllowNum)!=0)
      { num = c-'0';
        while(char.IsDigit(c=ReadChar(true))) num = c-'0' + num*10;
      }
      if(num<-1 || num==0 || chars.IndexOf(c)==-1) { doRebuke=true; continue; }
      if(char.IsLetter(c) && num>items[c].Count) { AddLine("You don't have that many!", true); continue; }
      TextInput = false;
      if(c=='-') return new MenuItem[1] { new MenuItem(null, 0) };
      if(c=='?') return Menu(items, flags, classes);
      if(c=='*') return Menu(items, flags, ItemClass.Any);
      return new MenuItem[1] { new MenuItem(items[c], num==-1 ? items[c].Count : num) };
    }
  }

  public override Spell ChooseSpell(Entity viewer)
  { string chars="";
    char c='a';
    for(int i=0; i<viewer.Spells.Count; i++) chars += c++;
    chars += '?';

    c = CharChoice("Cast which spell?", chars);
    if(c==0) return null;
    if(c!='?') return (Spell)viewer.Spells[c-'a'];

    ClearScreen();
    console.SetCursorPosition(0, 0);
    WriteLine(Color.Normal, "{0} Chance", "Spell".PadRight(38));
    console.WriteLine();

    c='a';
    foreach(Spell s in viewer.Spells)
      WriteLine(Color.Normal, "{0} - {1} ({2}%)", c++, s.Name.PadRight(34), s.CastChance(viewer));
    WriteLine(Color.Normal, "Choose spell by character.");
      
    Spell selected;
    while(true)
    { c=ReadChar();
      if(rec.Key.VirtualKey==NTConsole.Key.Escape || c=='\r' || c=='\n') { selected=null; break; }
      int index = c-'a';
      if(index>=0 && index<viewer.Spells.Count) { selected = (Spell)viewer.Spells[index]; break; }
    }
    RestoreScreen();
    return selected;
  }

  public override Spell ChooseSpell(Entity reader, Spellbook book)
  { Spell selected=null;
    bool clear=true;
    while(true)
    { if(clear)
      { ClearScreen();
        console.SetCursorPosition(0, 0);
        WriteLine(Color.Normal, "The "+book.FullName);
        console.WriteLine();
        clear=false;
      }
      else console.SetCursorPosition(0, 2);
      char c='a';
      foreach(Spell s in book.Spells)
      { Color color;
        int knowledge = reader.SpellKnowledge(s);
        if(knowledge>0) color = knowledge<2500 ? Color.Dire : knowledge<5000 ? Color.Warning : Color.Normal;
        else
        { int chance = s.LearnChance(reader);
          if(chance>=80) color = Color.LightGreen;
          else if(chance>=50) color = Color.Green;
          else if(chance>=25) color = Color.Cyan;
          else color = Color.DarkGrey;
        }
        WriteLine(color, "{0} {1} {2} (lv{3}, {4})", c++, s==selected ? '+' : '-', s.Name, s.Level, s.Class);
      }
      WriteLine(Color.Normal, "Choose spell by character. ? to view description.");
      
      nextchar: c=ReadChar();
      if(rec.Key.VirtualKey==NTConsole.Key.Escape) { selected=null; break; }
      if(c=='\r' || c=='\n') break;
      if(c=='?' && selected!=null)
      { ClearScreen();
        console.SetCursorPosition(0, 0);
        WriteLine(Color.Normal, "{0} (lv{1}, {2})", selected.Name, selected.Level, selected.Class);
        console.WriteLine();
        int height = WordWrap(selected.Description, mapW);
        for(int i=0; i<height; i++) console.WriteLine(wrapped[i]);
        ReadChar();
        clear=true;
        continue;
      }

      int index = c-'a';
      if(index>=0 && index<book.Spells.Length) { selected = book.Spells[index]; continue; }
      goto nextchar;
    }
    RestoreScreen();
    return selected;
  }

  public override RangeTarget ChooseTarget(Entity viewer, Spell spell, bool allowDir)
  { ClearLines();
    Point[] vpts = viewer.VisibleTiles();
    Entity[] mons = viewer.VisibleCreatures(vpts);
    Point pos = new Point(mapW/2, mapH/2), mpos;
    int monsi = 0;
    bool arbitrary=false, first=true;
    
    try
    { while(true)
      { if(pos.X<0) pos.X=0;
        else if(pos.X>=mapW) pos.X=mapW-1;
        if(pos.Y<0) pos.Y=0;
        else if(pos.Y>=mapH) pos.X=mapH-1;
        mpos = DisplayToMap(pos, viewer.Position);
        if(first) { AddLine("Choose target [direction or *+-= to target creatures]:", true); first=false; }
        else DescribeTile(viewer, mpos, vpts);
        
        if(spell!=null)
        { System.Collections.ICollection affected = spell.TracePath(viewer, mpos);
          if(affected!=null && affected.Count>0)
          { RenderMap(viewer, viewer.Position, vpts);
            foreach(Point ap in affected)
              if(viewer.Memory[ap].Type!=TileType.Border)
              { Point pt = MapToDisplay(ap, viewer.Position);
                buf[pt.Y*mapW+pt.X].Attributes |= NTConsole.ForeToBack(NTConsole.Attribute.Red);
              }
            console.PutBlock(0, 0, 0, 0, mapW, mapH, buf);
          }
        }

        console.SetCursorPosition(pos);

        nextChar:
        ReadChar();
        if(rec.Key.VirtualKey==NTConsole.Key.Escape) return new RangeTarget(new Point(-1, -1), Direction.Invalid);
        switch(char.ToLower(NormalizeDirChar()))
        { case 'b':
            if(arbitrary) pos.Offset(-1, 1);
            else return new RangeTarget(Direction.DownLeft);
            break;
          case 'j':
            if(arbitrary) pos.Y++;
            else return new RangeTarget(Direction.Down);
            break;
          case 'n':
            if(arbitrary) pos.Offset(1, 1);
            else return new RangeTarget(Direction.DownRight);
            break;
          case 'h':
            if(arbitrary) pos.X--;
            else return new RangeTarget(Direction.Left);
            break;
          case '.':
            if(arbitrary) return new RangeTarget(mpos);
            else return new RangeTarget(Direction.Self);
          case 'l':
            if(arbitrary) pos.X++;
            else return new RangeTarget(Direction.Right);
            break;
          case 'y':
            if(arbitrary) pos.Offset(-1, -1);
            else return new RangeTarget(Direction.UpLeft);
            break;
          case 'k':
            if(arbitrary) pos.Y--;
            else return new RangeTarget(Direction.Up);
            break;
          case 'u':
            if(arbitrary) pos.Offset(1, -1);
            else return new RangeTarget(Direction.UpRight);
            break;
          case '@': pos = new Point(mapW/2, mapH/2); break;
          case '<': if(!arbitrary) return new RangeTarget(Direction.Above); break;
          case '>': if(!arbitrary) return new RangeTarget(Direction.Below); break;
          case '*': AddLine("Move cursor to target position.", true); arbitrary=true; goto nextChar;
          case '+':
            if(mons.Length>0)
            { monsi = (monsi+1)%mons.Length;
              lastTarget = mons[monsi];
              pos = MapToDisplay(lastTarget.Position, viewer.Position);
              arbitrary=true;
            }
            else goto nextChar;
            break;
          case '-':
            if(mons.Length>0)
            { monsi = (monsi+mons.Length-1)%mons.Length;
              lastTarget = mons[monsi];
              pos = MapToDisplay(lastTarget.Position, viewer.Position);
              arbitrary=true;
            }
            else goto nextChar;
            break;
          case '=':
            if(lastTarget!=null)
            { int i;
              for(i=0; i<mons.Length; i++) if(mons[i]==lastTarget) break;
              if(i==mons.Length) { lastTarget=null; goto nextChar; }
              else pos = MapToDisplay(mons[monsi=i].Position, viewer.Position);
              arbitrary=true;
            }
            break;
          case ' ': case '\r': case '\n':
            if(arbitrary || mpos!=viewer.Position) return new RangeTarget(mpos);
            else goto nextChar;
          default: goto nextChar;
        }
      }
    }
    finally { RestoreLines(); }
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
        { PutString(0, yi, "{0} - {1}", menu[mc].Char, menu[mc].Item.InvName);
          mc++;
        }
      }
      PutString(0, yi, cs+mc<menu.Length ? "--more--" : "--bottom--");

      while(true)
      { char c = ReadChar();
        if(c=='\r' || c=='\n') goto done;
        if(c==' '  || c=='k') rec.Key.VirtualKey = NTConsole.Key.Next;
        if(c=='j') rec.Key.VirtualKey = NTConsole.Key.Prior;
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

  public override void DisplayTileItems(IInventory items) { DisplayTileItems(items, true); }
  public void DisplayTileItems(IInventory items, bool visible)
  { if(items.Count==0) return;
    if(items.Count==1) AddLine((visible ? "You see here: " : "You saw here: ")+items[0]);
    else
    { int nitems=items.Count+1, space=LineSpace-uncleared-2;
      bool other=false;
      if(nitems>space)
      { nitems = space;
        if(nitems<=1) AddLine("There are several items here.");
        else if(nitems==2) { AddLine((visible ? "You see here: " : "You saw here: ")+items[0]); other=true; }
      }
      if(nitems<=space || nitems>2)
      { AddLine("You see here:"); nitems--;
        if(nitems<items.Count) { other=true; nitems--; }
        for(int i=0; i<nitems; i++) AddLine(items[i].ToString());
      }
      if(other) AddLine(visible ? "There are other items here as well." : "There were other items there as well.");
    }
  }

  public override Point DisplayMap(Entity viewer)
  { ClearLines();
    int oldW = mapW; mapW = console.Width-1; 
    console.Fill(0, 0, mapW, mapH);
    SetCursorToPlayer();
    Point[] vpts = viewer.VisibleTiles();
    Point pos = viewer.Position, ret = new Point(-1, -1);

    int oldi=0;
    char c;
    while(true)
    { RenderMap(viewer, pos, vpts);
      DescribeTile(viewer, pos, vpts);
      nextChar:
      ReadChar();
      if(rec.Key.VirtualKey==NTConsole.Key.Escape) break;
      switch(c=char.ToLower(NormalizeDirChar()))
      { case 'b': pos.Offset(-1, 1); break;
        case 'j': pos.Y++; break;
        case 'n': pos.Offset(1, 1); break;
        case 'h': pos.X--; break;
        case 'l': pos.X++; break;
        case 'y': pos.Offset(-1, -1); break;
        case 'k': pos.Y--; break;
        case 'u': pos.Offset(1, -1); break;
        case 'g': ret = pos; goto done;
        case '@': pos = viewer.Position; break;
        case '<': case '>':
        { Map map = viewer.Memory;
          int ni  = oldi, size=map.Width*map.Height;
          TileType sf = c=='<' ? TileType.UpStairs : TileType.DownStairs;
          int x = ni%map.Width, y;
          for(y=ni/map.Width; ; y++)
          { for(; x<map.Width; x++)
            { if(++ni==size) { x=y=ni=0; }
              if(ni==oldi) goto nextChar;
              if(map[x, y].Type==sf) goto found;
            }
            x = 0;
          }
          found: pos=new Point(x, y); oldi=ni; break;
        }
        case ' ': case '\r': case '\n': goto done;
        default: goto nextChar;
      }
    }
    done:
    console.Fill(0, 0, mapW, mapH);
    mapW=oldW;
    RestoreLines();
    RestoreScreen();
    renderStats=true;
    return ret;
  }

  public override void ExamineItem(Entity viewer, Item item)
  { ClearScreen();
    console.SetCursorPosition(0, 0);
    console.WriteLine("{0} - {1}", item.Char, item.InvName);
    console.WriteLine();
    if(item.ShortDesc!=null)
    { WriteWrapped(item.ShortDesc, MapWidth);
      console.WriteLine();
    }
    if(item is Modifying)
    { Modifying mod = (Modifying)item;
      if(mod.AC!=0) console.WriteLine("Armor {0}: {1}", mod.AC<0 ? "penalty" : "bonus", mod.AC);
      if(mod.Dex!=0) console.WriteLine("Dexterity modifier: {0}", mod.Dex);
      if(mod.EV!=0) console.WriteLine("Evasion modifier: {0}", mod.EV);
      if(mod.Int!=0) console.WriteLine("Intelligence modifier: {0}", mod.Int);
      if(mod.Speed!=0) console.WriteLine("Speed modifier: {0}", mod.Speed);
      if(mod.Str!=0) console.WriteLine("Strength modifier: {0}", mod.Str);
    }
    if(item is Chargeable)
    { Chargeable ch = (Chargeable)item;
      console.WriteLine("This item has {0} charges remaining.", ch.Charges);
      if(ch.Recharged>0) console.WriteLine("This item has been recharged {0} times.", ch.Recharged);
    }
    if(item is Weapon)
    { Weapon w = (Weapon)item;
      if(w.Delay!=0) console.WriteLine("Attack delay: {0}%", w.Delay);
      if(w.Noise==0) console.WriteLine("This weapon is silent (noise=0).");
      else if(w.Noise<4) console.WriteLine("This weapon is rather quiet (noise={0}).", w.Noise);
      else if(w.Noise<8) console.WriteLine("This weapon is quite noisy (noise={0}).", w.Noise);
      else console.WriteLine("This weapon is extremely noisy (noise={0}).", w.Noise);
      if(w.ToHitBonus!=0) console.WriteLine("To hit {0}: {1}", w.ToHitBonus<0 ? "penalty" : "bonus", w.ToHitBonus);
      console.WriteLine("It falls into the '{0}' category.", w.wClass.ToString().ToLower());
    }
    else if(item is Shield)
    { Shield s = (Shield)item;
      console.WriteLine("Chance to block: {0}%", s.BlockChance);
    }
    if(item is Wieldable)
    { Wieldable w = (Wieldable)item;
      console.WriteLine("It is a {0}-handed item.", w.AllHandWield ? "two" : "one");
      console.WriteLine("This item is better for the {0}.",
                        w.Exercises==Attr.Str ? "strong" : w.Exercises==Attr.Dex ? "dexterous" : "intelligent");
    }
    if(item.Count>1)
      console.WriteLine("They weigh about {0} mt. each ({1} total).", item.Weight, item.Weight*item.Count);
    else console.WriteLine("It weighs about {0} mt.", item.Weight);
    if(item.LongDesc!=null)
    { console.WriteLine();
      WriteWrapped(item.LongDesc, MapWidth);
    }
    ReadChar();
    RestoreScreen();
  }

  public override void ExamineTile(Entity viewer, Point pos)
  { DescribeTile(viewer, pos, viewer.VisibleTiles());
  }

  public override void ManageSkills(Entity player)
  { ClearScreen();
    console.SetCursorPosition(0, 0);
    console.WriteLine("You have {0} points of unallocated experience.", player.ExpPool);

    int[] skillTable = Entity.RaceSkills[(int)player.Race];
    Skill[] skills = new Skill[(int)Skill.NumSkills];
    int numSkills=0;

    for(int i=0; i<(int)Skill.NumSkills; i++) if(player.SkillExp[i]>0) skills[numSkills++]=(Skill)i;

    while(true)
    { char c='a';
      console.SetCursorPosition(0, 2);
      for(int i=0; i<numSkills; i++)
      { bool enabled = player.Training(skills[i]);
        Write(enabled ? Color.Normal : Color.DarkGrey, "{0} {1} {2} Skill {3} ",
              c++, enabled ? '+' : '-', skills[i].ToString().PadRight(18), player.GetSkill(skills[i])+1);
        WriteLine(Color.LightBlue, "({0})", player.SkillExp[(int)skills[i]]*9/skillTable[(int)skills[i]]+1);
      }

      nextChar:
      c = ReadChar();
      if(c==' ' || c=='\r' || c=='\n' || rec.Key.VirtualKey==NTConsole.Key.Escape) break;
      if(c>='a' && c-'a'<numSkills) player.Train(skills[c-'a'], !player.Training(skills[c-'a']));
      else goto nextChar;
    }
    RestoreScreen();
  }

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
          { case ',':
              bool select=false;
              for(int i=0; i<menu.Length; i++) if(menu[i].Count!=menu[i].Item.Count) { select=true; break; }
              for(int i=0; i<menu.Length; i++) menu[i].Count = select ? menu[i].Item.Count : 0;
              goto redraw;
            case '+': for(int i=0; i<menu.Length; i++) menu[i].Count = menu[i].Item.Count; goto redraw;
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
      int count = 1;
      ReadChar();
      char c = NormalizeDirChar();

      if(c==0)
        {
        }
      else if(rec.Key.HasMod(NTConsole.Modifier.Ctrl)) switch(c+64)
      { case 'P': DisplayMessages(); break;
        case 'Q': inp.Action = Action.Quit; break;
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
        case 'a': inp.Action = Action.UseItem; break;
        case 'c': inp.Action = Action.CloseDoor; break;
        case 'C': inp.Action = Action.Carve; break;
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
        case '/':
          inp.Direction = CharToDirection(ReadChar());
          if(inp.Direction==Direction.Self) { count=100; inp.Action=Action.Rest; }
          else if(inp.Direction!=Direction.Invalid) inp.Action = Action.MoveToInteresting;
          break;
        case '?': DisplayHelp(); break;
      }
      if(inp.Action != Action.None)
      { inp.Count = count;
        return inp;
      }
    }
  }

  public override void Render(Entity viewer)
  { if(viewer.Is(Entity.Flag.Asleep)) return;
    mapW = Math.Min(console.Width, MapWidth); mapH = Math.Min(console.Height, MapHeight);
    RenderMap(viewer, viewer.Position, viewer.VisibleTiles());
    RenderStats(viewer);
    uncleared = 0;
    DrawLines();
  }

  public override void SetTitle(string title) { console.Title = title; }

  public override bool YesNo(Color color, string prompt, bool defaultYes)
  { char c = CharChoice(color, prompt, defaultYes ? "Yn" : "yN", defaultYes ? 'y' : 'n', true, "Please enter Y or N.");
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
      uncleared = 0;
      inputMode = value;
    }
  }

  void AddLine(string line) { AddLine(Color.Normal, line, false); }
  void AddLine(string line, bool redraw) { AddLine(Color.Normal, line, redraw); }
  void AddLine(Color color, string line) { AddLine(color, line, false); }
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

  void ClearLines() { oldLines = lines; lines = new LinkedList(); }

  void ClearScreen() // doesn't clear stat area
  { int width=Math.Min(MapWidth, console.Width), height=console.Height, mheight=MapHeight;
    console.Fill(0, 0, width, height); // clear map area
    console.Fill(0, mheight, console.Width, height-mheight); // clear message area
  }

  void DescribeTile(Entity viewer, Point pos, Point[] vpts)
  { bool visible=false;
    for(int i=0; i<vpts.Length; i++) if(vpts[i]==pos) { visible=true; break; }
    lines.Clear(); uncleared=0;
    if(!visible)
    { if(!viewer.Memory.GetFlag(pos, Tile.Flag.Seen)) { AddLine("You can't see that position.", true); return; }  
      AddLine("You can't see that position, but here's what you remember:");
    }
    Map map = visible ? viewer.Map : viewer.Memory;

    AddLine(map[pos].Type.ToString()+'.');
    Entity e = visible ? map.GetEntity(pos) : map[pos].Entity;
    if(e!=null)
    { string prefix = e==viewer ? "You are" : "{0} is";
      AddLine(string.Format(prefix+" here.", e.AName));
      Weapon w = e.Weapon;
      if(w!=null) AddLine(string.Format(prefix+" wielding {1} {2}.", "It", Global.AorAn(w.Name), w.Name));
      if(e!=viewer && e is AI)
      { AI ai = (AI)e;
        if(ai.State != AIState.Alerted) AddLine("It doesn't appear to have noticed you.");
      }
    }
    if(viewer.Map.HasItems(pos)) DisplayTileItems(map[pos].Items, visible);
    DrawLines();
    uncleared = 0;
  }

  bool Diff(Entity player, Attr attr) { return renderStats || player.GetAttr(attr)!=stats[(int)attr]; }
  bool Diff(int a, int b) { return renderStats || a!=b; }

  void DisplayHelp()
  { ClearScreen();
    string helptext =
@"                   Chrono Help
a - use/apply item
c - close door              C - carve up corpse
d - drop item(s)            D - drop item types
e - eat food
f - fire weapon/attack
i - inventory listing       I - invoke item power
o - open door
m - use special ability     M - list abilities
p - pray
q - quaff a potion
r - read scroll or book     R - remove a worn item
s - search adjacent tiles   S - manage skills
t - throw an item
v - view item description   V - version info
w - wield a weapon          W - wear an item
x - examine surroundings    X - examine level map
z - zap a wand              Z - cast a spell
! - shout or command allies ' - wield item a/b
, - pick up item(s)         . - rest one turn
: - examine occupied tile   \ - check knowledge
< - ascend staircase        > - descend staircase
= - reassign item letters   ^ - describe religion
b, h, j, k, l, n, u, y - nethack-like movement
/ .    - rest 100 turns     / DIR  - long walk
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

  void DisplayMessages()
  { int width=MapWidth, height=console.Height, ls, lss;
    System.Collections.ArrayList list = new System.Collections.ArrayList();
    for(LinkedList.Node node=lines.Head; node!=null; node=node.NextNode)
    { Line line = (Line)node.Data;
      int lh = WordWrap(line.Text, width);
      for(int i=0; i<lh; i++) list.Add(new Line(line.Color, wrapped[i]));
    }
    ls = lss = Math.Max(0, list.Count-height);

    ClearScreen();
    while(true)
    { for(int y=0,i=ls; i<list.Count && y<height; y++,i++)
      { Line line = (Line)list[i];
        PutStringP(line.Color, width, 0, y, line.Text);
      }

      nextChar: ReadChar();
      if(rec.Key.VirtualKey==NTConsole.Key.Escape) goto done;
      if(rec.Key.VirtualKey==NTConsole.Key.Prior) ls = Math.Max(0, ls-height*2/3);
      else if(rec.Key.VirtualKey==NTConsole.Key.Next) ls = Math.Min(lss, ls+height*2/3);
      else switch(char.ToLower(NormalizeDirChar()))
      { case 'j': if(ls<lss) ls++; break;
        case 'k': if(ls>0)   ls--; break;
        case ' ': case '\r': case '\n': goto done;
        default: goto nextChar;
      }
    }

    done: RestoreScreen();
  }

  Point DisplayToMap(Point pt, Point mapCenter) { return new Point(pt.X-mapW/2+mapCenter.X, pt.Y-mapH/2+mapCenter.Y); }

  void DrawLines()
  { int maxlines=LineSpace, li=maxlines-1;
    Line[] arr = new Line[maxlines];;
    { LinkedList.Node node = lines.Tail;
      while(li>=0 && node!=null)
      { Line line = (Line)node.Data;
        int height = WordWrap(line.Text);
        while(height-->0 && li>=0) arr[li--] = new Line(line.Color, wrapped[height]);
        node=node.PrevNode;
      }
    }

    blY=MapHeight-1;
    console.Fill(0, MapHeight, console.Width, console.Height-MapHeight);
    console.SetCursorPosition(0, blY+1); // MapHeight

    for(li++; li<arr.Length; li++)
    { console.Attributes = ColorToAttr(arr[li].Color);
      console.WriteLine(arr[li].Text);
      blX = arr[li].Text.Length; blY++;
    }
    SetCursorToPlayer();
  }

  void DrawMenuItem(int y, MenuItem item, MenuFlag flags)
  { PutString(0, y, "[{0}] {1} - {2}",
              (flags&MenuFlag.AllowNum)==0 ?
                item.Count==0 ? "-" : "+" :
                item.Count==0 ? " - " : item.Count==item.Item.Count ? " + " : item.Count.ToString("d3"),
              item.Char, item.Item.InvName);
  }

  int LineHeight(string line) { return WordWrap(line); }

  Point MapToDisplay(Point pt, Point mapCenter) { return new Point(pt.X-mapCenter.X+mapW/2, pt.Y-mapCenter.Y+mapH/2); }

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
  { if(str.Length>width) str = str.Substring(0, width);
    PutString(color, x, y, str);
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

  void RenderMap(Entity viewer, Point pos, Point[] vpts)
  { Rectangle rect = new Rectangle(pos.X-mapW/2, pos.Y-mapH/2, mapW, mapH);
    int size = mapW*mapH;
    if(buf==null || buf.Length<size) buf = new NTConsole.CharInfo[size];
    if(vis==null || vis.Length<size) vis = new bool[size];

    seeInvisible = viewer.Is(Entity.Flag.SeeInvisible);

    Map map = viewer.Memory==null ? viewer.Map : viewer.Memory;
    if(map==viewer.Map)
    { for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++) buf[i] = TileToChar(map[x,y], vis[i]);
      RenderMonsters(map.Entities, vpts, rect);
    }
    else
    { Array.Clear(vis, 0, size);
      for(int i=0; i<vpts.Length; i++)
        if(rect.Contains(vpts[i])) vis[(vpts[i].Y-rect.Y)*rect.Width+vpts[i].X-rect.X] = true;
      for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++)
        { Tile tile = map[x,y];
          buf[i] = tile.Type==TileType.UpStairs || tile.Type==TileType.DownStairs || tile.Entity==null ?
                    TileToChar(tile, vis[i]) : CreatureToChar(tile.Entity, vis[i]);
        }
      map = viewer.Map;
      for(int i=0,y=rect.Top; y<rect.Bottom; y++)
        for(int x=rect.Left; x<rect.Right; i++,x++) if(vis[i]) buf[i] = TileToChar(map[x,y], vis[i]);
      RenderMonsters(map.Entities, vpts, rect);
    }
    console.PutBlock(new Rectangle(0, 0, rect.Width, rect.Height), buf);
  }

  void RenderMonsters(System.Collections.ICollection coll, Point[] vpts, Rectangle rect)
  { foreach(Entity c in coll)
    { if(!seeInvisible && c.Is(Entity.Flag.Invisible)) continue;
      Point cp = c.Position;
      int bi = (c.Y-rect.Y)*rect.Width + c.X-rect.X;
      if(!rect.Contains(cp) || !vis[bi]) continue;
      for(int i=0; i<vpts.Length; i++) if(vpts[i]==cp) { buf[bi] = CreatureToChar(c, vis[bi]); break; }
    }
  }

  void RenderStats(Entity player)
  { int x=MapWidth+2, y=0, xlines, width=console.Width-x;
    if(Diff(player.ExpLevel, expLevel)) PutStringP(width, x, y, "{0} the {1}", player.Name, player.Title);
    PutStringP(width, x, y+1, "Human");
    if(Diff(player, Attr.MaxHP) || Diff(player.HP, hp))
    { int healthpct = player.HP*100/player.MaxHP;
      PutStringP(healthpct<25 ? Color.Dire : healthpct<50 ? Color.Warning : Color.Normal,
                width, x, y+2, "HP:   {0}/{1}", player.HP, player.MaxHP);
    }
    if(Diff(player, Attr.MaxMP) || Diff(player.MP, mp))
    { int magicpct = player.MP*100/player.MaxMP;
      PutStringP(magicpct<25 ? Color.Dire : magicpct<50 ? Color.Warning : Color.Normal,
                 width, x, y+3, "MP:   {0}/{1}", player.MP, player.MaxMP);
    }
    if(Diff(player, Attr.AC)) PutStringP(width, x, y+4, "AC:   {0}", player.AC);
    if(Diff(player, Attr.EV)) PutStringP(width, x, y+5, "EV:   {0}", player.EV);
    if(Diff(player, Attr.Str)) PutStringP(width, x, y+6, "Str:  {0}", player.Str);
    if(Diff(player, Attr.Int)) PutStringP(width, x, y+7, "Int:  {0}", player.Int);
    if(Diff(player, Attr.Dex)) PutStringP(width, x, y+8, "Dex:  {0}", player.Dex);
    if(Diff(player.Gold, gold)) PutStringP(width, x, y+9, "Gold: {0}", player.Gold);
    if(Diff(player.Exp, exp))
      PutStringP(width, x, y+10, "Exp:  {0}/{1} [{2}] (lv {3})", player.Exp, player.NextExp, player.ExpPool, player.ExpLevel);
    if(Diff(player.Age, age))
    { PutStringP(width, x, y+11, "Turn: {0}", player.Age);
      PutStringP(width, x, y+12, "Dungeon level {0}", App.CurrentLevel+1);
    }

    y = 13;
    bool drewHands=false;
    { string ws="";
      for(int i=0; i<player.Hands.Length; i++) if(player.Hands[i]!=null) ws += player.Hands[i].FullName;
      if(renderStats || equipStr!=ws)
      { handLines=0; drewHands=true;
        for(int i=0; i<player.Hands.Length; i++)
          if(player.Hands[i]!=null)
          { Item item = player.Hands[i];
            PutStringP(item.Class==ItemClass.Weapon ? Color.LightCyan : Color.LightGreen, width, x, y++, "{0}) {1}",
                       item.Char, item.FullName);
            handLines++;
          }
      }
    }
    y = 13 + handLines;
    if(drewHands || sick!=player.Sickness || hunger!=player.HungerLevel || playerFlags!=player.Flags ||
       carry!=player.CarryStress)
    { xlines=handLines;

      if(player.HungerLevel==HungerLevel.Hungry) { PutStringP(Color.Warning, width, x, y++, "Hungry"); xlines++; }
      else if(player.HungerLevel==HungerLevel.Starving) { PutStringP(Color.Dire, width, x, y++, "Starving"); xlines++; }

      if(player.Sickness>0)
      { PutStringP(player.Sickness>1 ? Color.Dire : Color.Warning, width, x, y++, "Sick"); xlines++;
      }
      
      switch(player.CarryStress)
      { case CarryStress.Burdened: PutStringP(Color.Warning, width, x, y++, "Burdened"); xlines++; break;
        case CarryStress.Stressed: PutStringP(Color.Dire, width, x, y++, "Stressed"); xlines++; break;
        case CarryStress.Overtaxed: PutStringP(Color.Dire, width, x, y++, "Overtaxed"); xlines++; break;
      }
      
      if(player.Is(Entity.Flag.Confused)) { PutStringP(Color.Warning, width, x, y++, "Confused"); xlines++; }
      if(player.Is(Entity.Flag.Hallucinating)) { PutStringP(Color.Warning, width, x, y++, "Hallucinating"); xlines++; }

      if(xlines<statLines) console.Fill(x, y, width, statLines-xlines);
      statLines=xlines;
    }
    UpdateStats(player);
    renderStats=false;
  }

  void RestoreLines() { lines=oldLines; oldLines=null; }

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
      for(int ci=0; ci<classes.Length; ci++)
        for(int i=0; i<itemarr.Length; i++)
          if(itemarr[i].Class==classes[ci]) list.Add(new MenuItem(itemarr[i]));
      menu = (MenuItem[])list.ToArray(typeof(MenuItem));
    }
  }

  void UpdateStats(Entity player)
  { for(int i=0; i<(int)Attr.NumAttributes; i++) stats[i] = player.GetAttr((Attr)i);
    age=player.Age; exp=player.Exp; expLevel=player.ExpLevel; gold=player.Gold; hp=player.HP; mp=player.MP;
    sick=player.Sickness; hunger=player.HungerLevel; carry=player.CarryStress; playerFlags=player.Flags;
    equipStr = "";
    for(int i=0; i<player.Hands.Length; i++) if(player.Hands[i]!=null) equipStr += player.Hands[i].FullName;
  }

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

  void Write(Color color, string format, params object[] parms)
  { console.Attributes = ColorToAttr(color);
    console.Write(string.Format(format, parms));
  }

  void WriteLine(Color color, string str)
  { console.Attributes = ColorToAttr(color);
    console.WriteLine(str);
  }
  void WriteLine(Color color, string format, params object[] parms)
  { console.Attributes = ColorToAttr(color);
    console.WriteLine(string.Format(format, parms));
  }

  void WriteWrapped(string text, int width)
  { int height = WordWrap(text, width);
    for(int i=0; i<height; i++) console.WriteLine(wrapped[i]);
  }

  NTConsole.CharInfo[] buf;
  bool[] vis;
  MenuItem[] menu;
  string[] wrapped = new string[4];
  Entity lastTarget;

  // these are used for stat rendering
  int[] stats = new int[(int)Attr.NumAttributes];
  Entity.Flag playerFlags;
  HungerLevel hunger;
  CarryStress carry;
  string equipStr="";
  int age=-1, exp=-1, expLevel=-1, gold=-1, hp=-1, mp=-1, sick=-1, handLines;
  bool renderStats;

  NTConsole console = new NTConsole();
  LinkedList lines = new LinkedList(), oldLines; // a circular array would be more efficient...
  NTConsole.InputRecord rec;
  int  uncleared, maxLines=200, blX, blY, mapW, mapH, statLines;
  bool inputMode, seeInvisible;

  static NTConsole.Attribute ColorToAttr(Color color)
  { NTConsole.Attribute attr = NTConsole.Attribute.Black;
    if((color & Color.Red)    != Color.Black) attr |= NTConsole.Attribute.Red;
    if((color & Color.Green)  != Color.Black) attr |= NTConsole.Attribute.Green;
    if((color & Color.Blue)   != Color.Black) attr |= NTConsole.Attribute.Blue;
    if((color & Color.Bright) != Color.Black) attr |= NTConsole.Attribute.Bright;
    return attr;
  }

  static NTConsole.CharInfo CreatureToChar(Entity c, bool visible)
  { return new NTConsole.CharInfo(raceMap[(int)c.Race], visible ? ColorToAttr(c.Color) : NTConsole.Attribute.DarkGrey);
  }

  static NTConsole.CharInfo ItemToChar(Item item)
  { return new NTConsole.CharInfo(itemMap[(int)item.Class], ColorToAttr(item.Color));
  }

  static NTConsole.CharInfo TileToChar(Tile tile, bool visible)
  { NTConsole.CharInfo ci;
    if((visible || tile.Type!=TileType.UpStairs && tile.Type!=TileType.DownStairs) &&
       tile.Items!=null && tile.Items.Count>0)
      ci = ItemToChar(tile.Items[0]);
    else switch(tile.Type)
    { case TileType.Wall:       ci = new NTConsole.CharInfo('#', NTConsole.Attribute.Brown); break;
      case TileType.ClosedDoor: ci = new NTConsole.CharInfo('+', NTConsole.Attribute.Yellow); break;
      case TileType.OpenDoor:   ci = new NTConsole.CharInfo((char)254, NTConsole.Attribute.Yellow); break;
      case TileType.RoomFloor:  ci = new NTConsole.CharInfo((char)250, NTConsole.Attribute.Grey); break;
      case TileType.Corridor:   ci = new NTConsole.CharInfo((char)176, NTConsole.Attribute.Grey); break;
      case TileType.UpStairs:
        return new NTConsole.CharInfo('<', visible ? NTConsole.Attribute.Grey : NTConsole.Attribute.LightBlue);
      case TileType.DownStairs:
        return new NTConsole.CharInfo('>', visible ? NTConsole.Attribute.Grey : NTConsole.Attribute.Red);
      default: ci = new NTConsole.CharInfo(' ', NTConsole.Attribute.Black); break;
    }
    if(!visible && tile.Type!=TileType.ClosedDoor) ci.Attributes = NTConsole.Attribute.DarkGrey;
    //ci.Attributes |= NTConsole.ForeToBack((NTConsole.Attribute)((tile.Sound*15+Map.MaxSound-1)/Map.MaxSound));
    //ci.Attributes |= NTConsole.ForeToBack((NTConsole.Attribute)((tile.Scent*15+Map.MaxScent-1)/Map.MaxScent));
    return ci;
  }

  // Amulet, Weapon, Shield, Armor, Ammo, Food, Corpse, Scroll, Ring, Potion, Wand, Tool, Spellbook, Container, Treasure,
  static readonly char[] itemMap = new char[(int)ItemClass.NumClasses]
  { '$', '"', ')', '[', '[', '(', '%', '&', '?', '=', '!', '/', ']', '+', ']', '*',
  };
  static readonly char[] raceMap = new char[(int)Race.NumRaces]
  { '@', 'o'
  };

  static readonly char[] dirLets = new char[9] { 'b', 'j', 'n', 'h', '.', 'l', 'y', 'k', 'u' };
}

} // namespace Chrono.Application