using System;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chrono
{

public static class Xml
{
  public static Ability Abilities(XmlAttribute node) { return node==null ? Ability.None : Abilities(node.Value); }
  public static Ability Abilities(string list) { return (Ability)FlagList(typeof(Ability), list); }

  public static Ailment Ailments(XmlAttribute node) { return node==null ? Ailment.None : Ailments(node.Value); }
  public static Ailment Ailments(string list) { return (Ailment)FlagList(typeof(Ailment), list); }

  public static AIFlag AIFlags(XmlAttribute node) { return node==null ? AIFlag.None : AIFlags(node.Value); }
  public static AIFlag AIFlags(string list) { return (AIFlag)FlagList(typeof(AIFlag), list); }

  public static string Attr(XmlNode node, string attr) { return Attr(node, attr, null); }
  public static string Attr(XmlNode node, string attr, string defaultValue)
  {
    if(node==null) return defaultValue;
    XmlAttribute an = node.Attributes[attr];
    return an==null ? defaultValue : an.Value;
  }

  public static XmlAttribute AttrNode(XmlNode node, string attr) { return node==null ? null : node.Attributes[attr]; }

  public static string[] BlockToArray(string block) { return BlockToArray(block, false); }
  public static string[] BlockToArray(string block, bool collapseLines)
  {
    if(block==null || block=="") return new string[0];

    block = ltbl.Replace(block.Replace("\r", ""), ""); // remove CRs, and leading and trailing blank lines
    Match m = lspc.Match(block); // TODO: this should be the amount that can be removed uniformly from nonblank lines
    Regex trim = new Regex(@"^\s{0,"+m.Length+@"}|\s+$",
                           collapseLines ? RegexOptions.Singleline : RegexOptions.Multiline);
    if(!collapseLines) return trim.Replace(block, "").Split('\n');

    System.Collections.ArrayList list = new System.Collections.ArrayList();
    int pos, oldPos=0;
    string last=null;
    do
    {
      string temp;
      pos = block.IndexOf('\n', oldPos);

      if(pos==oldPos)
      {
        list.Add("");
        last = null;
      }
      else
      {
        temp = trim.Replace(pos==-1 ? block.Substring(oldPos) : block.Substring(oldPos, pos-oldPos), "");
        if(last==null) list.Add(last=temp);
        else list[list.Count-1] = last = last+' '+temp;
      }
      oldPos = pos+1;
    } while(pos!=-1);

    return (string[])list.ToArray(typeof(string));
  }

  public static string BlockToString(string block) { return BlockToString(block, true); }
  public static string BlockToString(string block, bool collapseLines)
  {
    if(block==null || block=="") return "";

    block = ltbl.Replace(block.Replace("\r", ""), ""); // remove CRs, and leading and trailing blank lines
    Match m = lspc.Match(block); // TODO: this should be the amount that can be removed uniformly from nonblank lines
    Regex trim = new Regex(@"^\s{0,"+m.Length+@"}|\s+$",
                           collapseLines ? RegexOptions.Singleline : RegexOptions.Multiline);
    if(!collapseLines) return trim.Replace(block, "");

    System.Text.StringBuilder sb = new System.Text.StringBuilder(block.Length);
    int pos, oldPos=0;
    bool nl=true;
    do
    {
      pos = block.IndexOf('\n', oldPos);

      if(pos==oldPos)
      {
        if(!nl) { sb.Append('\n'); nl=true; }
        sb.Append('\n');
      }
      else
      {
        if(!nl) sb.Append(' ');
        else nl=false;
        sb.Append(trim.Replace(pos==-1 ? block.Substring(oldPos) : block.Substring(oldPos, pos-oldPos), ""));
      }
      oldPos = pos+1;
    } while(pos!=-1);

    return sb.ToString();
  }

  public static Color Color(XmlNode node, string attrName) { return Color(node.Attributes[attrName]); }
  public static Color Color(XmlAttribute attr) { return Color(attr.Value); }
  public static Color Color(string color) { return (Color)Enum.Parse(typeof(Color), color); }

  public static EntitySize EntitySize(XmlAttribute attr) { return EntitySize(attr.Value); }
  public static EntitySize EntitySize(string str) { return (EntitySize)Enum.Parse(typeof(EntitySize), str); }

  public static uint FlagList(Type enumType, string list)
  {
    uint flags = 0;
    if(!IsEmpty(list)) foreach(string str in List(list)) flags |= Convert.ToUInt32(Enum.Parse(enumType, str));
    return flags;
  }

  public static float Float(XmlAttribute attr) { return Float(attr, 0); }
  public static float Float(XmlAttribute attr, float defaultValue)
  {
    return attr==null ? defaultValue : float.Parse(attr.Value);
  }
  public static float Float(XmlNode node, string attr) { return Float(node.Attributes[attr], 0); }
  public static float Float(XmlNode node, string attr, float defaultValue)
  {
    return Float(node.Attributes[attr], defaultValue);
  }

  public static Gender Gender(XmlAttribute attr) { return Gender(attr.Value); }
  public static Gender Gender(string gender) { return (Gender)Enum.Parse(typeof(Gender), gender); }

  public static int Int(XmlAttribute attr) { return Int(attr, 0); }
  public static int Int(XmlAttribute attr, int defaultValue)
  {
    return attr==null ? defaultValue : int.Parse(attr.Value);
  }
  public static int Int(XmlNode node, string attr) { return Int(node.Attributes[attr], 0); }
  public static int Int(XmlNode node, string attr, int defaultValue)
  {
    return Int(node.Attributes[attr], defaultValue);
  }

  public static Intrinsic Intrinsics(XmlAttribute node) { return node==null ? Intrinsic.None : Intrinsics(node.Value); }
  public static Intrinsic Intrinsics(string list) { return (Intrinsic)FlagList(typeof(Intrinsic), list); }

  public static bool IsEmpty(XmlAttribute attr) { return attr==null || IsEmpty(attr.Value); }
  public static bool IsEmpty(string str) { return str==null || str==""; }
  public static bool IsEmpty(XmlNode node, string attr) { return IsEmpty(node.Attributes[attr]); }

  public static bool IsTrue(XmlAttribute attr) { return attr!=null && IsTrue(attr.Value); }
  public static bool IsTrue(string str) { return str!=null && str!="" && str!="0" && str.ToLower()!="false"; }
  public static bool IsTrue(XmlNode node, string attr) { return IsTrue(node.Attributes[attr]); }

  public static string[] List(XmlNode node, string attr) { return List(node.Attributes[attr]); }
  public static string[] List(XmlAttribute attr) { return IsEmpty(attr) ? new string[0] : split.Split(attr.Value); }
  public static string[] List(string data) { return IsEmpty(data) ? new string[0] : split.Split(data); }

  public static Race Race(XmlAttribute attr) { return Race(attr.Value); }
  public static Race Race(string str) { return (Race)Enum.Parse(typeof(Race), str); }

  public static int RangeInt(string range) { return RangeInt(range, 0); }
  public static int RangeInt(string range, int defaultValue)
  {
    return range==null || range=="" ? defaultValue : new Range(range, defaultValue).RandValue();
  }

  public static int RangeInt(XmlAttribute range) { return RangeInt(range, 0); }
  public static int RangeInt(XmlAttribute range, int defaultValue)
  {
    return range==null ? defaultValue : RangeInt(range.Value);
  }

  public static int RangeInt(XmlNode node, string attr) { return RangeInt(node.Attributes[attr], 0); }
  public static int RangeInt(XmlNode node, string attr, int defaultValue)
  {
    return RangeInt(node.Attributes[attr], defaultValue);
  }

  public static string String(XmlAttribute str) { return str==null ? null : str.Value; }
  public static string String(XmlAttribute str, string defaultValue) { return str==null ? defaultValue : str.Value; }
  public static string String(XmlNode node, string attr) { return String(node.Attributes[attr]); }

  public static int Weight(XmlNode node, string attr) { return Weight(node.Attributes[attr]); }
  public static int Weight(XmlAttribute attr) { return attr==null ? 0 : Weight(attr.Value); }
  public static int Weight(string str)
  {
    if(IsEmpty(str)) return 0;
    Match m = weight.Match(str);
    if(!m.Success) throw new ArgumentException("'"+str+"' is not a valid weight");
    double d = double.Parse(m.Groups[1].Value);
    if(m.Groups[1].Value=="kg") d *= 1000;
    return (int)Math.Round(d);
  }

  static Regex ltbl   = new Regex(@"^(?:\s*\n)+|\s+$", RegexOptions.Singleline);
  static Regex lspc   = new Regex(@"^\s+", RegexOptions.Singleline);
  static Regex split  = new Regex(@"\s+", RegexOptions.Singleline);
  static Regex weight = new Regex(@"(\d+(?:\.\d+)?|\.\d+)(g|kg)", RegexOptions.Singleline);
}

} // namespace Chrono