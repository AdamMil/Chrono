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

  public static int IntValue(XmlAttribute attr, int defaultValue)
  { return attr==null ? defaultValue : int.Parse(attr.Value);
  }

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
}

} // namespace Chrono