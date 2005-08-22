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
    bool nl=true;
    do
    { pos = block.IndexOf('\n', oldPos);

      if(pos==oldPos)
      { if(!nl) { sb.Append('\n'); nl=true; }
        sb.Append('\n');
      }
      else
      { if(!nl) sb.Append(' ');
        else nl=false;
        sb.Append(trim.Replace(pos==-1 ? block.Substring(oldPos) : block.Substring(oldPos, pos-oldPos), ""));
      }
      oldPos = pos+1;
    } while(pos!=-1);

    return sb.ToString();
  }

  public static Color Color(XmlAttribute attr) { return Color(attr.Value); }
  public static Color Color(string color) { return (Color)Enum.Parse(typeof(Color), color); }

  public static EntityClass EntityClass(XmlNode node, string attr) { return EntityClass(node.Attributes[attr].Value); }
  public static EntityClass EntityClass(XmlAttribute attr) { return EntityClass(attr.Value); }
  public static EntityClass EntityClass(string entClass)
  { if(entClass=="Melee") return Chrono.EntityClass.Fighter;
    else if(entClass=="Magic") return Chrono.EntityClass.Wizard;
    else return (EntityClass)Enum.Parse(typeof(EntityClass), entClass);
  }

  public static float Float(XmlAttribute attr) { return Float(attr, 0); }
  public static float Float(XmlAttribute attr, float defaultValue)
  { return attr==null ? defaultValue : float.Parse(attr.Value);
  }
  public static float Float(XmlNode node, string attr) { return Float(node.Attributes[attr], 0); }
  public static float Float(XmlNode node, string attr, float defaultValue)
  { return Float(node.Attributes[attr], defaultValue);
  }

  public static Gender Gender(XmlAttribute attr) { return Gender(attr.Value); }
  public static Gender Gender(string gender)
  { return gender=="Either" ? Global.Coinflip() ? Chrono.Gender.Male : Chrono.Gender.Female :
                            (Gender)Enum.Parse(typeof(Gender), gender);
  }

  public static int Int(XmlAttribute attr) { return Int(attr, 0); }
  public static int Int(XmlAttribute attr, int defaultValue)
  { return attr==null ? defaultValue : int.Parse(attr.Value);
  }
  public static int Int(XmlNode node, string attr) { return Int(node.Attributes[attr], 0); }
  public static int Int(XmlNode node, string attr, int defaultValue)
  { return Int(node.Attributes[attr], defaultValue);
  }

  public static bool IsEmpty(XmlAttribute attr) { return attr==null || IsEmpty(attr.Value); }
  public static bool IsEmpty(string str) { return str==null || str==""; }
  public static bool IsEmpty(XmlNode node, string attr) { return IsEmpty(node.Attributes[attr]); }

  public static bool IsTrue(XmlAttribute attr) { return attr!=null && IsTrue(attr.Value); }
  public static bool IsTrue(string str) { return str!=null && str!="" && str!="0" && str.ToLower()!="false"; }
  public static bool IsTrue(XmlNode node, string attr) { return IsTrue(node.Attributes[attr]); }

  public static string[] List(XmlNode node, string attr) { return List(node.Attributes[attr]); }
  public static string[] List(XmlAttribute attr) { return IsEmpty(attr) ? new string[0] : split.Split(attr.Value); }
  public static string[] List(string data) { return IsEmpty(data) ? new string[0] : split.Split(data); }

  public static Race Race(XmlNode node, string attr) { return Race(node.Attributes[attr].Value); }
  public static Race Race(XmlAttribute attr) { return Race(attr.Value); }
  public static Race Race(string race)
  { return race=="Player" ? App.Player.Race : (Race)Enum.Parse(typeof(Race), race);
  }

  public static int RangeInt(string range) { return RangeInt(range, 0); }
  public static int RangeInt(string range, int defaultValue)
  { return range==null || range=="" ? defaultValue : new Range(range, defaultValue).RandValue();
  }

  public static int RangeInt(XmlAttribute range) { return RangeInt(range, 0); }
  public static int RangeInt(XmlAttribute range, int defaultValue)
  { return range==null ? defaultValue : RangeInt(range.Value);
  }

  public static int RangeInt(XmlNode node, string attr) { return RangeInt(node.Attributes[attr], 0); }
  public static int RangeInt(XmlNode node, string attr, int defaultValue)
  { return RangeInt(node.Attributes[attr], defaultValue);
  }

  public static string String(XmlAttribute str) { return str==null ? null : str.Value; }
  public static string String(XmlAttribute str, string defaultValue) { return str==null ? defaultValue : str.Value; }
  public static string String(XmlNode node, string attr) { return String(node.Attributes[attr]); }
  
  static Regex ltbl  = new Regex(@"^(?:\s*\n)+|\s+$", RegexOptions.Singleline);
  static Regex lspc  = new Regex(@"^\s+", RegexOptions.Singleline);
  static Regex split = new Regex(@"\s+", RegexOptions.Singleline);
}

} // namespace Chrono