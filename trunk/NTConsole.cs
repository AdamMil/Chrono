using System;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;

namespace Chrono
{

public class NTConsole
{ 
  #region Type declarations
  [FlagsAttribute()]
  public enum Attribute : ushort
  { Black=0, Blue=1, Green=2, Red=4, Bright=8,
    Cyan=Green|Blue, Purple=Red|Blue, Brown=Red|Green, DarkGrey=Red|Green|Blue,
    Grey=Black|Bright, LightRed=Red|Bright, LightGreen=Green|Bright, LightBlue=Blue|Bright,
    LightCyan=Cyan|Bright, Magenta=Purple|Bright, Yellow=Brown|Bright, White=DarkGrey|Bright,
    
    GridTop=0x400, GridLeft=0x800, GridRight=0x1000, GridBottom=0x8000,
    Underlined=GridBottom, ReverseVideo=0x4000
  };
  [FlagsAttribute()]
  public enum InputModes : uint
  { None=0, Processed=1, LineBuffered=2, Echo=4, Window=8, Mouse=16
  }
  [FlagsAttribute()]
  public enum OutputModes : uint
  { None=0, Processed=1, WrapAtEOL=2
  }
  public enum DisplayModes : uint
  { FullScreen=1, HardwareFullscreen=2
  }

  [StructLayout(LayoutKind.Sequential, Size=4)]
  public struct CharInfo
  { public CharInfo(char c, Attribute attr) { Char=c; Attributes=attr; }
    public char      Char;
    public Attribute Attributes;
  }

  [StructLayout(LayoutKind.Sequential, Size=4)]
  public struct Coord
  { public Coord(short x, short y) { X=x; Y=y; }
    public Coord(int x, int y) { X=(short)x; Y=(short)y; }
    public short X, Y;
  }

  public enum InputType : ushort
  { Keyboard=1, Mouse=2, BufferResize=4
  }  
  [FlagsAttribute()]
  public enum Modifier : uint
  { RightAlt=1, LeftAlt=2, RightCtrl=4, LeftCtrl=8, Shift=16,
    NumLock=32, ScrollLock=64, CapsLock=128, Enhanced=256
  };
  [StructLayout(LayoutKind.Explicit)]
  public struct KeyRecord
  { [FieldOffset(0)]  public bool     KeyDown;
    [FieldOffset(4)]  public ushort   RepeatCount;
    [FieldOffset(6)]  public ushort   VirtualKey;
    [FieldOffset(8)]  public ushort   ScanCode;
    [FieldOffset(10)] public char     Char;
    [FieldOffset(12)] public Modifier ControlKeys;
  }
  [StructLayout(LayoutKind.Sequential)]
  public struct MouseRecord
  { [FlagsAttribute()]
    public enum Button : uint
    { LeftMost=1, RightMost=2, Second=4, Third=8, Fourth=16
    };
    [FlagsAttribute()]
    public enum Flag : uint
    { Clicked=0, Moved=1, DoubleClicked=2, MouseWheel=4
    };
    public Coord    Position;
    public Button   Buttons;
    public Modifier ControlKeys;
    public Flag     Flags;
  }
  [StructLayout(LayoutKind.Sequential)]
  public struct BufferResized
  { public Coord Size;
  }
  [StructLayout(LayoutKind.Explicit)]
  public struct InputRecord
  { [FieldOffset(0)] public InputType     Type;
    [FieldOffset(4)] public KeyRecord     Key;
    [FieldOffset(4)] public MouseRecord   Mouse;
    [FieldOffset(4)] public BufferResized Buffer;
  }

  public enum ControlType : uint
  { CtrlC=0, CtrlBreak=1, Close=2, Logoff=5, Shutdown=6
  }
  public delegate bool ControlHandler(ControlType ctrl);
  #endregion

  public NTConsole()
  { hRead  = GetStdHandle(-10); // STD_INPUT_HANDLE  = -10
    hWrite = GetStdHandle(-11); // STD_OUTPUT_HANDLE = -11
    if(hRead==invalid || hWrite==invalid) throw new ApplicationException("Unable to get standard handles.");
    SyncInputMode();
    SyncOutputMode();
  }

  public InputModes InputMode
  { get { return inMode; }
    set
    { Check(SetConsoleMode(hRead, (uint)value));
      SyncInputMode();
    }
  }

  public OutputModes OutputMode
  { get { return outMode; }
    set
    { Check(SetConsoleMode(hWrite, (uint)value));
      SyncOutputMode();
    }
  }
  
  public string Title
  { get
    { if(ascii)
      { byte[] buf = new byte[512];
        uint len = GetConsoleTitleA(buf, 512);
        return AsciiToString(buf, len);
      }
      else
      { char[] buf = new char[512];
        uint len = GetConsoleTitleW(buf, 512);
        return BufferToString(buf, len);
      }
    }
    set
    { Check(SetConsoleTitleA(Encoding.ASCII.GetBytes(value)));
    }
  }
  
  public Attribute Attributes
  { get
    { ScreenBufferInfo si = new ScreenBufferInfo();
      Check(GetConsoleScreenBufferInfo(hWrite, out si));
      return (Attribute)si.Attributes;
    }
    set
    { Check(SetConsoleTextAttribute(hWrite, (ushort)value));
    }
  }
  
  public Point CursorPosition
  { get { return GetCursorPosition(); }
    set { SetCursorPosition(value); }
  }
  
  public DisplayModes DisplayMode
  { get
    { uint mode;
      Check(GetConsoleDisplayMode(out mode));
      return (DisplayModes)mode;
    }
  }
  
  public bool InputWaiting
  { get
    { InputRecord rec;
      uint num, read;
      Check(GetNumberOfConsoleInputEvents(hRead, out num));
      while(num-->0)
      { Check(PeekConsoleInputA(hRead, out rec, 1, out read));
        if(rec.Type<=InputType.BufferResize) return true;
        Check(ReadConsoleInputA(hRead, out rec, 1, out read));
      }
      return false;
    }
  }
  
  public int Width
  { get
    { ScreenBufferInfo si = new ScreenBufferInfo();
      Check(GetConsoleScreenBufferInfo(hWrite, out si));
      return si.Window.Right+1;
    }
  }
  public int Height
  { get
    { ScreenBufferInfo si = new ScreenBufferInfo();
      Check(GetConsoleScreenBufferInfo(hWrite, out si));
      return si.Window.Bottom+1;
    }
  }

  public event ControlHandler ControlEvent
  { add    { SetConsoleCtrlHandler(value, true); }
    remove { SetConsoleCtrlHandler(value, false); }
  }

  public Attribute ForeToBack(Attribute fore) { return (Attribute)((byte)fore<<4); }
  
  public void SetCursorVisibility(bool visible, int fillPct)
  { if(fillPct<0 || fillPct>100)
      throw new ArgumentOutOfRangeException("fillPct", fillPct, "Must be percent from 0 to 100");
    if(fillPct==0) { fillPct=1; visible=false; }
    CursorInfo ci = new CursorInfo((uint)fillPct, visible);
    Check(SetConsoleCursorInfo(hWrite, ref ci));
  }
  
  public void SetCursorPosition(Point pt) { SetCursorPosition(pt.X, pt.Y); }
  public void SetCursorPosition(int x, int y)
  { Check(SetConsoleCursorPosition(hWrite, new Coord(x, y)));
  }

  public Point GetCursorPosition()
  { ScreenBufferInfo si;
    Check(GetConsoleScreenBufferInfo(hWrite, out si));
    return new Point(si.Cursor.X, si.Cursor.Y);
  }
  
  public void SetSize(int width, int height)
  { SmallRect rect = new SmallRect();
    rect.Right  = (short)(width-1);
    rect.Bottom = (short)(height-1);
    SetConsoleWindowInfo(hWrite, true, ref rect);
    Check(SetConsoleScreenBufferSize(hWrite, new Coord(width, height)));
    Check(SetConsoleWindowInfo(hWrite, true, ref rect));
  }
  
  public char ReadChar()
  { InputModes old = inMode;
    if((inMode&InputModes.LineBuffered)!=0) InputMode = inMode & (~InputModes.LineBuffered&~InputModes.Echo);
    uint len;
    char c;
    unsafe
    { if(ascii)
      { byte b;
        Check(ReadConsoleA(hRead, &b, 1, out len, NULL));
        c = (char)b;
      }
      else Check(ReadConsoleW(hRead, &c, 1, out len, NULL));
    }
    if(InputMode!=old) InputMode=old;
    if((old&InputModes.Echo)!=0) WriteChar(c);
    return c;
  }
  
  public InputRecord ReadInput()
  { InputRecord rec;
    ReadInput(out rec);
    return rec;
  }

  public void ReadInput(out InputRecord rec)
  { uint read;
    do
    { if(ascii)
      { Check(ReadConsoleInputA(hRead, out rec, 1, out read));
        if(rec.Type==InputType.Keyboard) rec.Key.Char = (char)((ushort)rec.Key.Char&0xFF);
      }
      else Check(ReadConsoleInputW(hRead, out rec, 1, out read));
    } while(rec.Type>InputType.BufferResize);
  }
  
  public string ReadLine()
  { InputModes old = inMode;
    if((inMode&InputModes.LineBuffered)==0) InputMode = inMode|InputModes.LineBuffered;
    uint   len;
    string ret;
    if(ascii)
    { byte[] buf = new byte[512];
      unsafe
      { fixed(byte *pbuf=buf)
          Check(ReadConsoleA(hRead, pbuf, 512, out len, NULL));
      }
      if(buf[len-1]==(byte)'\n') len--;
      if(buf[len-1]==(byte)'\r') len--;
      ret = AsciiToString(buf, len);
    }
    else
    { char[] buf = new char[512];
      unsafe
      { fixed(char *pbuf=buf)
          Check(ReadConsoleW(hRead, pbuf, 512, out len, NULL));
      }
      if(buf[len-1]=='\n') len--;
      if(buf[len-1]=='\r') len--;
      ret = BufferToString(buf, len);
    }
    if(InputMode!=old) InputMode=old;
    WriteChar('\n');
    return ret;
  }
  
  public void WriteChar(char c)
  { uint count;
    unsafe
    { if(ascii)
      { byte b = (byte)c;
        Check(WriteConsoleA(hWrite, &b, 1, out count, NULL));
      }
      else Check(WriteConsoleW(hWrite, &c, 1, out count, NULL));
    }
  }

  public int Write(string format, object p0) { return Write(String.Format(format, p0)); }
  public int Write(string format, object p0, object p1) { return Write(String.Format(format, p0, p1)); }
  public int Write(string format, object p0, object p1, object p2) { return Write(String.Format(format, p0, p1, p2)); }
  public int Write(string format, params object[] parms) { return Write(String.Format(format, parms)); }
  public int Write(string str)
  { uint count;
    unsafe
    { if(ascii)
        fixed(byte *pbuf=Encoding.ASCII.GetBytes(str))
          Check(WriteConsoleA(hWrite, pbuf, (uint)str.Length, out count, NULL));
      else fixed(char *pstr=str)
        Check(WriteConsoleW(hWrite, pstr, (uint)str.Length, out count, NULL));
    }
    return (int)count;
  }

  public void WriteLine() { Write("\r\n"); }
  public void WriteLine(string format, object p0) { WriteLine(String.Format(format, p0)); }
  public void WriteLine(string format, object p0, object p1) { WriteLine(String.Format(format, p0, p1)); }
  public void WriteLine(string format, object p0, object p1, object p2) { WriteLine(String.Format(format, p0, p1, p2)); }
  public void WriteLine(string format, params object[] parms) { WriteLine(String.Format(format, parms)); }
  public void WriteLine(string line)
  { Write(line);
    Write("\r\n");
  }

  public char GetChar(Point pt) { return GetChar(pt.X, pt.Y); }
  public char GetChar(int x, int y)
  { uint  read;
    char  c;
    unsafe
    { if(ascii)
      { byte b;
        Check(ReadConsoleOutputCharacterA(hWrite, &b, 1, new Coord(x, y), out read));
        c = (char)b;
      }
      else Check(ReadConsoleOutputCharacterW(hWrite, &c, 1, new Coord(x, y), out read));
    }
    return c;
  }
  
  public void PutChar(Point pt, char c) { PutChar(pt.X, pt.Y, c); }
  public void PutChar(int x, int y, char c)
  { uint written;
    unsafe
    { if(ascii)
      { byte b = (byte)c;
        Check(WriteConsoleOutputCharacterA(hWrite, &b, 1, new Coord(x, y), out written));
      }
      else Check(WriteConsoleOutputCharacterW(hWrite, &c, 1, new Coord(x, y), out written));
    }
  }
  
  public Attribute GetAttribute(Point pt) { return GetAttribute(pt.X, pt.Y); }
  public Attribute GetAttribute(int x, int y)
  { uint   read;
    ushort attr;
    unsafe
    { Check(ReadConsoleOutputAttribute(hWrite, &attr, 1, new Coord(x, y), out read));
    }
    return (Attribute)attr;
  }

  public void PutAttribute(Point pt, Attribute attr) { PutAttribute(pt.X, pt.Y, attr); }
  public void PutAttribute(int x, int y, Attribute attr)
  { uint written;
    unsafe
    { Check(WriteConsoleOutputAttribute(hWrite, (ushort*)&attr, 1, new Coord(x, y), out written));
    }
  }
  
  public CharInfo GetCharInfo(Point pt) { return GetCharInfo(pt.X, pt.Y); }
  public CharInfo GetCharInfo(int x, int y)
  { Coord pos = new Coord(x, y);
    uint   read;
    ushort attr;
    char   c;
    unsafe
    { if(ascii)
      { byte b;
        Check(ReadConsoleOutputCharacterA(hWrite, &b, 1, pos, out read));
        c = (char)b;
      }
      else Check(ReadConsoleOutputCharacterW(hWrite, &c, 1, pos, out read));
      Check(ReadConsoleOutputAttribute(hWrite, &attr, 1, pos, out read));
    }
    return new CharInfo(c, (Attribute)attr);
  }
  
  public void PutCharInfo(Point pt, CharInfo c) { PutCharInfo(pt.X, pt.Y, c); }
  public void PutCharInfo(int x, int y, CharInfo c)
  { Coord pos = new Coord(x, y);
    uint written;
    unsafe
    { if(ascii)
      { byte b = (byte)c.Char;
        Check(WriteConsoleOutputCharacterA(hWrite, &b, 1, pos, out written));
      }
      else Check(WriteConsoleOutputCharacterW(hWrite, &c.Char, 1, pos, out written));
      Check(WriteConsoleOutputAttribute(hWrite, (ushort*)&c.Attributes, 1, pos, out written));
    }
  }

  public void Fill(CharInfo c)
  { ScreenBufferInfo si = new ScreenBufferInfo();
    Check(GetConsoleScreenBufferInfo(hWrite, out si));
    Fill(0, 0, si.Window.Right+1, si.Window.Bottom+1, c);
  }
  public void Fill(Rectangle rect, CharInfo c) { Fill(rect.X, rect.Y, rect.Width, rect.Height, c); }
  public void Fill(int x, int y, int width, int height, CharInfo c)
  { Coord start = new Coord(x, y);
    short   end = (short)(y+height);
    uint written;
    if(ascii)
    { byte b = (byte)c.Char;
      for(; start.Y<end; start.Y++)
      { FillConsoleOutputCharacterA(hWrite, b, (uint)width, start, out written);
        FillConsoleOutputAttribute(hWrite, (ushort)c.Attributes, (uint)width, start, out written);
      }
    }
    else for(; start.Y<end; start.Y++)
    { FillConsoleOutputCharacterW(hWrite, c.Char, (uint)width, start, out written);
      FillConsoleOutputAttribute(hWrite, (ushort)c.Attributes, (uint)width, start, out written);
    }
  }
  
  public void GetBlock(Point dest, Point src, int width, int height, CharInfo[] buf) { GetBlock(dest.X, dest.Y, src.X, src.Y, width, height, buf); }
  public void GetBlock(Rectangle rect, CharInfo[] buf) { GetBlock(rect.X, rect.Y, 0, 0, rect.Width, rect.Height, buf); }
  public void GetBlock(int dx, int dy, int sx, int sy, int width, int height, CharInfo[] buf)
  { SmallRect srect = new SmallRect(dx, dy, dx+width, dy+height);
    ReadConsoleOutput(hWrite, buf, new Coord(width, height), new Coord(sx, sy), ref srect);
  }

  public void PutBlock(Point dest, Point src, int width, int height, CharInfo[] buf) { PutBlock(dest.X, dest.Y, src.X, src.Y, width, height, buf); }
  public void PutBlock(Rectangle rect, CharInfo[] buf) { PutBlock(rect.X, rect.Y, 0, 0, rect.Width, rect.Height, buf); }
  public void PutBlock(int dx, int dy, int sx, int sy, int width, int height, CharInfo[] buf)
  { OutputModes old = outMode;
    SmallRect drect = new SmallRect(dx, dy, dx+width, dy+height);
    WriteConsoleOutput(hWrite, buf, new Coord(width, height), new Coord(sx, sy), ref drect);
  }

  static readonly public CharInfo Blank = new CharInfo(' ', Attribute.Black);

  protected void Check(bool test)
  { if(!test) throw new ApplicationException("System call failed: error code "+GetLastError());
  }
  
  protected string BufferToString(char[] buf, uint length)
  { if(length==0) return "";
    StringBuilder sb = new StringBuilder();
    sb.Append(buf, 0, (int)length);
    return sb.ToString();
  }

  protected string AsciiToString(byte[] buf, uint length)
  { if(length==0) return "";
    return Encoding.ASCII.GetString(buf, 0, (int)length);
  }
  
  protected void SyncInputMode()
  { uint mode;
    GetConsoleMode(hRead, out mode);
    inMode = (InputModes)mode;
  }
  protected void SyncOutputMode()
  { uint mode;
    GetConsoleMode(hWrite, out mode);
    outMode = (OutputModes)mode;
  }

  #region DLL Imports
  [StructLayout(LayoutKind.Sequential, Size=16)]
  protected struct CursorInfo
  { public CursorInfo(uint size, bool visible) { Size=size; Visible=visible; }
    public uint Size;
    public bool Visible;
  }
  [StructLayout(LayoutKind.Sequential, Size=8)]
  protected struct SmallRect
  { public SmallRect(short left, short top, short right, short bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
    public SmallRect(int left, int top, int right, int bottom) { Left=(short)left; Top=(short)top; Right=(short)right; Bottom=(short)bottom; }
    public short Left, Top, Right, Bottom;
  }
  [StructLayout(LayoutKind.Explicit, Size=22)]
  protected struct ScreenBufferInfo
  { [FieldOffset(0)]  public Coord Size;
    [FieldOffset(4)]  public Coord Cursor;
    [FieldOffset(8)]  public Attribute Attributes;
    [FieldOffset(10)] public SmallRect Window;
    [FieldOffset(18)] public Coord MaxWindowSize;
  }

  [DllImport("kernel32")]
  protected static extern int GetLastError();
  [DllImport("kernel32")]
  protected static extern IntPtr GetStdHandle(int nStdHandle);
  [DllImport("kernel32")]
  protected static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint pmode);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint mode);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleCursorInfo(IntPtr hConsoleHandle, ref CursorInfo info);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleCursorPosition(IntPtr hConsoleHandle, Coord pos);
  [DllImport("kernel32")]
  protected static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleHandle, out ScreenBufferInfo info);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleScreenBufferSize(IntPtr hConsoleHandle, Coord size);
  [DllImport("kernel32")]
  protected static extern uint GetConsoleTitleA(byte[] buffer, uint size);
  [DllImport("kernel32")]
  protected static extern uint GetConsoleTitleW(char[] buffer, uint size);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleTitleA(byte[] title);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleTitleW(string title);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleWindowInfo(IntPtr hConsoleHandle, bool absoluteCoords, ref SmallRect rect);
  [DllImport("kernel32")]
  protected static extern bool FlushConsoleInputBuffer(IntPtr hConsoleHandle);
  [DllImport("kernel32")]
  protected static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleHandle, out uint numRecords);
  [DllImport("kernel32")]
  protected static extern bool ReadConsoleInputA(IntPtr hConsoleHandle, out InputRecord input, uint numToRead, out uint numRead);
  [DllImport("kernel32")]
  protected static extern bool ReadConsoleInputW(IntPtr hConsoleHandle, out InputRecord input, uint numToRead, out uint numRead);
  [DllImport("kernel32")]
  protected static extern bool PeekConsoleInputA(IntPtr hConsoleHandle, out InputRecord input, uint numToRead, out uint numRead);
  [DllImport("kernel32")]
  protected static extern bool PeekConsoleInputW(IntPtr hConsoleHandle, out InputRecord input, uint numToRead, out uint numRead);
  [DllImport("kernel32")]
  unsafe protected static extern bool ReadConsoleA(IntPtr hConsoleHandle, byte *buffer, uint numToRead, out uint numRead, IntPtr reserved);
  [DllImport("kernel32")]
  unsafe protected static extern bool WriteConsoleA(IntPtr hConsoleHandle, byte *buffer, uint numToWrite, out uint written, IntPtr reserved);
  [DllImport("kernel32")]
  unsafe protected static extern bool ReadConsoleW(IntPtr hConsoleHandle, char *buffer, uint numToRead, out uint numRead, IntPtr reserved);
  [DllImport("kernel32")]
  unsafe protected static extern bool WriteConsoleW(IntPtr hConsoleHandle, char *buffer, uint numToWrite, out uint written, IntPtr reserved);
  [DllImport("kernel32")]
  unsafe protected static extern bool ReadConsoleOutputCharacterA(IntPtr hConsoleHandle, byte *buffer, uint numToRead, Coord first, out uint numRead);
  [DllImport("kernel32")]
  unsafe protected static extern bool ReadConsoleOutputCharacterW(IntPtr hConsoleHandle, char *buffer, uint numToRead, Coord first, out uint numRead);
  [DllImport("kernel32")]
  unsafe protected static extern bool ReadConsoleOutputAttribute(IntPtr hConsoleHandle, ushort *buffer, uint numToRead, Coord first, out uint numRead);
  [DllImport("kernel32")]
  unsafe protected static extern bool WriteConsoleOutputCharacterA(IntPtr hConsoleHandle, byte *buffer, uint numToWrite, Coord first, out uint written);
  [DllImport("kernel32")]
  unsafe protected static extern bool WriteConsoleOutputCharacterW(IntPtr hConsoleHandle, char *buffer, uint numToWrite, Coord first, out uint written);
  [DllImport("kernel32")]
  unsafe protected static extern bool WriteConsoleOutputAttribute(IntPtr hConsoleHandle, ushort *buffer, uint numToWrite, Coord first, out uint written);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleTextAttribute(IntPtr hConsoleHandle, ushort attributes);
  [DllImport("kernel32")]
  protected static extern bool FillConsoleOutputCharacterA(IntPtr hConsoleHandle, byte character, uint count, Coord start, out uint written);
  [DllImport("kernel32")]
  protected static extern bool FillConsoleOutputCharacterW(IntPtr hConsoleHandle, char character, uint count, Coord start, out uint written);
  [DllImport("kernel32")]
  protected static extern bool FillConsoleOutputAttribute(IntPtr hConsoleHandle, ushort attribute, uint count, Coord start, out uint written);
  [DllImport("kernel32")]
  protected static extern bool WriteConsoleOutput(IntPtr hConsoleHandle, CharInfo[] buffer, Coord size, Coord start, ref SmallRect dest);
  [DllImport("kernel32")]
  protected static extern bool ReadConsoleOutput(IntPtr hConsoleHandle, CharInfo[] buffer, Coord size, Coord start, ref SmallRect src);
  [DllImport("kernel32")]
  protected static extern bool SetConsoleCtrlHandler(ControlHandler callback, bool add);
  [DllImport("kernel32")]
  protected static extern bool GenerateConsoleCtrlEvent(uint controlType, uint processGID);
  [DllImport("kernel32")]
  protected static extern bool GetConsoleDisplayMode(out uint mode);
  #endregion

  protected IntPtr invalid = new IntPtr((long)-1), NULL = new IntPtr(0);
  protected IntPtr hRead, hWrite;
  protected InputModes  inMode;
  protected OutputModes outMode;
  protected bool        ascii = (Environment.OSVersion.Platform != PlatformID.Win32NT);
}

} // namespace Chrono