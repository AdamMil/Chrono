using System;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chrono
{

public class Xml
{ Xml() { }

  public static string Attr(XmlNode node, string attr)
  { if(node==null) return null;
    XmlAttribute an = node.Attributes[attr];
    return an==null ? null : an.Value;
  }
  public static string Attr(XmlNode node, string attr, string defaultValue)
  { if(node==null) return defaultValue;
    XmlAttribute an = node.Attributes[attr];
    return an==null ? defaultValue : an.Value;
  }

  public static XmlAttribute AttrNode(XmlNode node, string attr) { return node==null ? null : node.Attributes[attr]; }

  public static string[] BlockToArray(string block) { return BlockToArray(block, false); }
  public static string[] BlockToArray(string block, bool collapseLines)
  { if(block==null || block=="") return new string[0];

    block = ltbl.Replace(block.Replace("\r", ""), ""); // remove CRs, and leading and trailing blank lines
    Match m = lspc.Match(block); // TODO: this should be the amount that can be removed uniformly from nonblank lines
    Regex trim = new Regex(@"^\s{0,"+m.Length+@"}|\s+$",
                           collapseLines ? RegexOptions.Singleline : RegexOptions.Multiline);
    if(!collapseLines) return trim.Replace(block, "").Split('\n');

    System.Collections.ArrayList list = new System.Collections.ArrayList();
    int pos, oldPos=0;
    string last=null;
    do
    { string temp;
      pos = block.IndexOf('\n', oldPos);

      if(pos==oldPos)
      { list.Add("");
        last = null;
      }
      else
      { temp = trim.Replace(pos==-1 ? block.Substring(oldPos) : block.Substring(oldPos, pos-oldPos), "");
        if(last==null) list.Add(last=temp);
        else list[list.Count-1] = last = last+' '+temp;
      }
      oldPos = pos+1;
    } while(pos!=-1);

    return (string[])list.ToArray(typeof(string));
  }

  public static string BlockToString(string block) { return BlockToString(block, true); }
  public static string BlockToString(string block, bool collapseLines)
  { if(block==null || block=="") return "";

    block = ltbl.Replace(block.Replace("\r", ""), ""); // remove CRs, and leading and trailing blank lines
    Match m = lspc.Match(block); // TODO: this should be the amount that can be removed uniformly from nonblank lines
    Regex trim = new Regex(@"^\s{0,"+m.Length+@"}|\s+$",
                           collapseLines ? RegexOptions.Singleline : RegexOptions.Multiline);
    if(!collapseLines) return trim.Replace(block, "");

    System.Text.StringBuilder sb = new System.Text.StringBuilder(block.Length);
    int pos, oldPos=0;
    bool nl=false;
    do
    { pos = block.IndexOf('\n', oldPos);

      if(pos==oldPos)
      { sb.Append('\n');
        nl=true;
      }
      else
      { if(nl) sb.Append('\n');
        sb.Append(trim.Replace(pos==-1 ? block.Substring(oldPos) : block.Substring(oldPos, pos-oldPos), ""));
        nl=false;
      }
      oldPos = pos+1;
    } while(pos!=-1);

    return sb.ToString();
  }

  public static int IntValue(XmlAttribute attr, int defaultValue)
  { return attr==null ? defaultValue : int.Parse(attr.Value);
  }

  public static bool IsTrue(XmlAttribute attr) { return attr!=null && IsTrue(attr.Value); }
  public static bool IsTrue(string str) { return str!=null && str!="" && str!="0" && str.ToLower()!="false"; }

  public static int RangeInt(string range)
  { int pos = range.IndexOf(':');
    if(pos==-1) return int.Parse(range);
    return Global.Rand(int.Parse(range.Substring(0, pos)), int.Parse(range.Substring(pos+1)));
  }
  public static int RangeInt(XmlAttribute range, int defaultValue)
  { return range==null ? defaultValue : RangeInt(range.Value);
  }
  
  public static void Range(string range, out int low, out int high)
  { int pos = range.IndexOf(':');
    if(pos==-1) low=high=int.Parse(range);
    else
    { low  = int.Parse(range.Substring(0, pos));
      high = int.Parse(range.Substring(pos+1));
    }
  }
  public static void Range(XmlAttribute range, ref int low, ref int high)
  { if(range!=null) Range(range.Value, out low, out high);
  }
  
  public static string String(XmlAttribute str) { return str==null ? null : str.Value; }
  public static string String(XmlAttribute str, string defaultValue) { return str==null ? defaultValue : str.Value; }
  
  static Regex ltbl = new Regex(@"^(?:\s*\n)+|\s+$", RegexOptions.Singleline);
  static Regex lspc = new Regex(@"^\s+", RegexOptions.Singleline);
}

} // namespace Chrono