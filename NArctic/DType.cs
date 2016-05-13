using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using NArctic;
using Utilities;
using System.Runtime.InteropServices;

namespace NArctic
{
	public static class DateTime64
	{
		public static DateTime UnixEpoch = new DateTime(1970,01,01);

		public static DateTime ToDateTime(long ns)
		{
			return UnixEpoch.AddTicks (ns / 100);
		}

		public static TimeSpan ToTimeSpan(long ns)
		{
			return TimeSpan.FromTicks (ns / 100);
		}

		public static long ToDateTime64(this DateTime dt)
		{
			return dt.Subtract (UnixEpoch).Ticks * 100;
		}

		public static long ToDateTime64(this TimeSpan dt)
		{
			return dt.Ticks * 100;
		}

        public static Func<DateTime, long> TimeGrouping(string freq)
        {
            if (freq == "Y")
                return dt => dt.Year - UnixEpoch.Year;
            else if (freq == "M")
                return dt => (dt.Year - UnixEpoch.Year) * 12 + (dt.Month - UnixEpoch.Month);
            else if (freq == "D")
                return dt => (long)(dt - UnixEpoch).TotalDays;
            else if(freq == "s")
                return dt => (long)(dt - UnixEpoch).TotalSeconds;
            else if (freq == "ms")
                return dt => (dt - UnixEpoch).Ticks/10;
            throw new ArgumentException(freq);
        }

        public static TimeSpan TimeFreq(string freq)
        {
            if (freq == "Y")
                return TimeSpan.FromDays(366);
            else if (freq == "M")
                return TimeSpan.FromDays(31);
            else if (freq == "D")
                return TimeSpan.FromDays(1);
            else if (freq == "s")
                return TimeSpan.FromSeconds(1);
            else if (freq == "ms")
                return TimeSpan.FromTicks(10);
            throw new ArgumentException(freq);
        }

    }

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
	 
		public static DType DateTime64 = new DType("'<M8[ns]'");
		public static DType Long = new DType("'<i8'");
        public static DType Int = new DType("'<i4'");
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

		public void ToBuffer<T>(byte[] buf, T[] data, int iheight, int icol) {
			var bytesPerRow = this.FieldOffset (this.Fields.Count);
			var fieldOffset = this.FieldOffset (icol);
			var dtype = this.Fields[icol];
			int elsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
			GCHandle ghsrc = GCHandle.Alloc (data, GCHandleType.Pinned);
			GCHandle ghdst = GCHandle.Alloc (buf, GCHandleType.Pinned);
			UnsafeAPI.ColumnCopy(ghdst.AddrOfPinnedObject(), fieldOffset, bytesPerRow, ghsrc.AddrOfPinnedObject(), 0, elsize, iheight, elsize);
			ghsrc.Free ();
			ghdst.Free ();
		}

		public unsafe void FromBuffer<T>(byte[] buf, T[] data, int iheight, int icol) {
			var bytesPerRow = this.FieldOffset (this.Fields.Count);
			var fieldOffset = this.FieldOffset (icol);
			var dtype = this.Fields[icol];
			int elsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
			GCHandle ghsrc = GCHandle.Alloc (buf, GCHandleType.Pinned);
			GCHandle ghdst = GCHandle.Alloc (data, GCHandleType.Pinned);
			UnsafeAPI.ColumnCopy(ghdst.AddrOfPinnedObject(), 0, elsize, ghsrc.AddrOfPinnedObject(), fieldOffset, bytesPerRow, iheight, elsize);
			ghsrc.Free ();
			ghdst.Free ();
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
                else if (Type == typeof(int))
                    return "<i4";
                else if (Type == typeof(DateTime))
					return "<M8[ns]";
				else
					throw new InvalidOperationException ("unknown numpy dtype '{0}'".Args (Type));
			}
				
		}
	}
}
