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
	 
		public static DateTime UnixEpoch = new DateTime(1970,01,01);

		public static DateTime NanosToDateTime(long ns)
		{
			return UnixEpoch.AddTicks (ns / 100);
		}

		public static long DateTimeToNanos(DateTime dt)
		{
			return dt.Subtract (UnixEpoch).Ticks * 100;
		}

		public static DType DateTime64 = new DType("'<M8[ns]'");
		public static DType Long = new DType("'<i8'");
		public static DType Double = new DType("'<f8'");

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
			}else if (Type == typeof(IList<object>)) {
				string str = string.Join(DType.sep, Fields.Select(f=>f.ToString()));
				return "[{0}]".Args(str);
			} else if (Type == typeof(IDictionary<string, object>)) {
				string str = string.Join(DType.sep, Fields.Select(f=>"('{0}','{1}')".Args(f.Name, f)));
				return "[" + str + "]";
			} else {
				if (Type == typeof(double))
					return "<f8";
				else if (Type == typeof(long))
					return "<i8";
				else if (Type == typeof(DateTime))
					return "<M8[ns]";
				else
					throw new InvalidOperationException ("unknown numpy dtype '{0}'".Args (Type));
			}
				
		}
	}
}
