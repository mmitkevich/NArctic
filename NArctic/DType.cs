using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NArctic;
using Utilities;

namespace NumCIL
{
	public class DType
	{
		public Type Type;
		public List<DType> Fields = new List<DType>();
		public DType Parent;
		public string Name;
		public int Size;
		public string Format = null;
		public static Dictionary<Type,string> Formats = new Dictionary<Type,string> ();
		public static string sep = ", ";
		static DType()
		{
			Formats [typeof(DateTime)] = "{0:yyyy-MM-dd HH:mm:ss.fff}";
			Formats [typeof(double)] = "{0: 0.0000e+0}";
		}

		public DType(Type type=null, DType parent=null)
		{
			Type = type;
			Parent = null;
		}

		public DType(string spec)
		{
			new DTypeParser ().Parse (spec, 0, this);
		}

		public int FieldOffset(int ifield)
		{
			int offset = 0;
			for (int i = 0; i < ifield; i++)
				offset += Fields [i].Size;
			return offset;
		}

		public string ToString(object value)
		{
			var fmt = Format ?? Formats.Get(value.GetType()) ?? "{0}";
			return fmt.Args (value);
		}

		public override string ToString ()
		{
			if (Type == null) {
				return "null";
			}else if (Type == typeof(List<object>)) {
				string str = string.Join(DType.sep, Fields.Select(f=>f.ToString()));
				return "[{0}]".Args(str);
			} else if (Type == typeof(Dictionary<string, object>)) {
				string str = string.Join(DType.sep, Fields.Select(f=>"'{0}':'{1}'".Args(f.Name, f)));
				return "{" + str + "}";
			} else {
				return Type.Name;
			}
				
		}
	}
}
