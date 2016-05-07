using System;
using System.Linq;
using NumCIL.Generic;
using System.Collections.Generic;
using NumCIL.Generic;
using MongoDB.Driver;
using System.Windows.Markup;
using NumCIL;
using System.Runtime.InteropServices;
using Utilities;
using NumCIL.Boolean;
using NumCIL.Complex128;
using System.Collections;
using System.Text;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MongoDB.Bson;
using Utilities;
using NumCIL.Double;

namespace NumCIL
{

	public static class Builder
	{
		public static T Ret<T>(T arg){
			return arg;
		}

		/*public static Series<T> ToSeries<T>(this T[] data, DType dtype)
		{
			return new Series<T, T>(dtype, new NdArray<T>(data), Ret<T>, Ret<T>);
		}*/

		/*public static Series<T> ToSeries<T>(this NdArray<T> data, DType dtype)
		{
			return new Series<T, T>(dtype, data, Ret<T>, Ret<T>);
		}*/


		public static Double.Series ToDoubleSeries(this double[] data)
		{
			return new Double.Series(new Double.NdArray(data));
		}

		public static Int64.Series ToLongSeries(this long[] data)
		{
			return new Int64.Series(new Int64.NdArray(data));
		}

		public static Time.Series ToDateTimeSeries(this DateTime[] data)
		{
			return data.Select (DateTime64.ToDateTime64).ToArray ().ToDateTimeSeries ();
		}

		public static Time.Series ToDateTimeSeries(this long[] data)
		{
			return new Time.Series(new NdArray<long>(data));
		}
	}

	public abstract class Series : IEnumerable
	{
		public DType DType {get;set;}

		public string Name {
			get { return DType.Name; } 
			set { DType.Name = value; }
		}

		public abstract int Count{ get; }

		public Series<T> As<T>() {
			return this as Series<T>;
		}

		internal abstract object At(int index);

		public static unsafe void ColumnCopy(void *target, int target_col, int target_width, void *source, int source_col, int source_width, int count, int elsize)
		{
			unsafe
			{
				byte* src = (byte*)source;
				byte* dst = (byte*)target;
				src += source_col;
				dst += target_col;

				for (int i = 0; i < count; i++) {
					for (int j = 0; j < elsize; j++)
						dst [j] = src [j];
					dst += target_width;
					src += source_width;
				}
			}
		}

		public unsafe abstract void  ToBuffer (byte[] buf, DType buftype, int iheight, int icol);

		public Double.Series AsDouble {
			get {
				var rtn = this as Double.Series;
				if (rtn == null)
					throw new InvalidOperationException ("Conversion to double not implemented!");
				return rtn;
			}
		}

		public Int64.Series AsLong {
			get {
				var rtn = this as Int64.Series;
				if (rtn == null)
					throw new InvalidOperationException ("Conversion to double not implemented!");
				return rtn;
			}
		}

		public Time.Series AsDateTime {
			get{
				var rtn = this as Time.Series;
				if (rtn == null)
					throw new InvalidOperationException ("Conversion to double not implemented!");
				return rtn;
			}
		}


		public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
		{
			if (buftype.Fields [icol].Type == typeof(double)) {
				return Double.Series.FromBuffer (buf, buftype, iheight, icol);
			}else if (buftype.Fields[icol].Type == typeof(long)) {
				return Int64.Series.FromBuffer (buf, buftype, iheight, icol);
			}else if (buftype.Fields[icol].Type == typeof(DateTime)) {
				return Time.Series.FromBuffer (buf, buftype, iheight, icol);
			}else
				throw new InvalidOperationException("Failed decode {0} type".Args(buftype.Fields[icol].Type));
		}

		public abstract Series this [Range range] {
			get;
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Series (long[] data) {
			return data.ToLongSeries ();
		}

		public static implicit operator Series (double[] data) {
			return data.ToDoubleSeries ();
		}

		public static implicit operator Series (DateTime[] data) {
			return data.ToDateTimeSeries ();
		}

		/*public static implicit operator Series (NdArray<long> data) {
			return data.ToSeries (DType.Long);
		}

		public static implicit operator Series (NdArray<double> data) {
			return data.ToSeries (DType.Double);
		}*/

		public abstract Series Clone ();
	}

	public abstract class Series<T> : Series, IEnumerable<T>
	{
		public abstract IEnumerator<T> GetEnumerator ();

		public static T NoConv<T>(T val) {
			return val;
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return ((IEnumerable<T>)this).GetEnumerator ();
		}

		public abstract T this[int index]{get;set;}

	}

	public abstract class Series<T, Q> : Series<T>
	{
		public NdArray<Q> Values {get;set;}
		public Func<Q,T> Getter;
		public Func<T,Q> Setter;

		public Series(DType dtype, NdArray<Q> values, Func<Q,T> getter, Func<T,Q> setter)
		{
			DType = dtype;
			Values = values;
			Getter = getter;
			Setter = setter;
		}

		/*public override NumCIL.Series Clone()
		{
			return new Series<T,Q> (this.DType, this.Values.Clone (), this.Getter, this.Setter);
		}*/

		public override int Count
		{
			get{ return (int)Values.Shape.Dimensions [0].Length;}
		}

		internal override object At(int index)
		{
			return this [index];
		}

		public override IEnumerator<T> GetEnumerator() 
		{
			foreach (Q x in Values.Value as IEnumerable<Q>) {
				yield return Getter(x);
			}
		}

		public override T this[int index]
		{
			get {
				Q q = Values.Value[index];
				return Getter(q); 
			}
			set { 
				Values.Value[index] = Setter((T)value); 
			}
		}

		/*public override Series this [Range range] {
			get {
				return new Series<T, Q> (DType, Values[range], Getter, Setter);
			}
		}*/

		public override string ToString ()
		{
			IEnumerable<T> itr = (IEnumerable<T>)this;
			return (Name!=null?Name+"\n":"") + string.Join ("\n",itr.Select(x => "{0}".Args (x)));
		}
	}

	namespace Time {
		public class Series : Series<DateTime>
		{
			public Int64.NdArray Values {get;set;}

			public Series(Int64.NdArray values)
			{
				DType = DType.DateTime64;
				Values = values;
			}

			public static implicit operator Series(Int64.NdArray values) {
				return new Series (values);
			}

			public static implicit operator Int64.NdArray(Series series) {
				return series.Values;
			}

			public override int Count
			{
				get{ return (int)Values.Shape.Dimensions [0].Length;}

			}

			public override NumCIL.Series Clone()
			{
				return new Series (this.Values.Clone ());
			}

			internal override object At(int index)
			{
				return this [index];
			}

			public override IEnumerator<DateTime> GetEnumerator() 
			{
				foreach (var x in Values.Value) {
					yield return DateTime64.ToDateTime(x);
				}
			}

			public override System.DateTime this[int index]
			{
				get {
					return DateTime64.ToDateTime(Values.Value [index]);
				}
				set { 
					Values.Value [index] = value.ToDateTime64 ();
				}
			}

			public override NumCIL.Series this [Range range] {
				get {
					return new Series(Values[range]);
				}
			}

			public unsafe override void  ToBuffer(byte[] buf, DType buftype, int iheight, int icol)
			{
				var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
				var fieldOffset = buftype.FieldOffset (icol);
				var dtype = buftype.Fields[icol];
				long[] data = this.Values.DataAccessor.AsArray();
				int elsize = sizeof(long);
				fixed(void *dst = &buf[0])
				fixed(void *src = &data[0])
				ColumnCopy(dst, fieldOffset, bytesPerRow, src, 0, elsize, iheight, elsize);
			}

			public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
			{
				var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
				var fieldOffset = buftype.FieldOffset (icol);
				var dtype = buftype.Fields[icol];
				var data = new long[iheight];
				int elsize = sizeof(long);
				fixed(void *src = &buf[0])
				fixed(void *dst = &data[0])
				ColumnCopy(dst, 0, elsize, src, fieldOffset, bytesPerRow, iheight, elsize);
				return data.ToDateTimeSeries();
			}


			public override string ToString ()
			{
				IEnumerable<long> itr = (IEnumerable<long>)this;
				return (Name!=null?Name+"\n":"") + string.Join ("\n",itr.Select(x => "{0}".Args (x)));
			}
		}
	}

	namespace Double {
		public class Series : Series<double>
		{
			public NdArray Values {get;set;}

			public Series(NdArray values)
			{
				DType = DType.Double;
				Values = values;
			}

			public static implicit operator Series(NdArray values) {
				return new Series (values);
			}

			public static implicit operator NdArray(Series series) {
				return series.Values;
			}

			public override int Count
			{
				get{ return (int)Values.Shape.Dimensions [0].Length;}
			}

			public override NumCIL.Series Clone()
			{
				return new Series (this.Values.Clone ());
			}

			internal override object At(int index)
			{
				return this [index];
			}

			public override IEnumerator<double> GetEnumerator() 
			{
				foreach (var x in Values.Value as IEnumerable<double>) {
					yield return x;
				}
			}

			public override double this[int index]
			{
				get {
					return Values.Value[index];
				}
				set { 
					Values.Value [index] = value;
				}
			}

			public override NumCIL.Series this [Range range] {
				get {
					return new Series(Values[range]);
				}
			}

			public unsafe override void  ToBuffer(byte[] buf, DType buftype, int iheight, int icol)
			{
				var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
				var fieldOffset = buftype.FieldOffset (icol);
				var dtype = buftype.Fields[icol];
				double[] data = (this.Values as Double.NdArray).DataAccessor.AsArray();
				int elsize = sizeof(double);
				fixed(void *dst = &buf[0])
				fixed(void *src = &data[0])
				ColumnCopy(dst, fieldOffset, bytesPerRow, src, 0, elsize, iheight, elsize);
			}

			public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
			{
				var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
				var fieldOffset = buftype.FieldOffset (icol);
				var dtype = buftype.Fields[icol];
				var data = new double[iheight];
				int elsize = sizeof(double);
				fixed(void *src = &buf[0])
				fixed(void *dst = &data[0])
				ColumnCopy(dst, 0, elsize, src, fieldOffset, bytesPerRow, iheight, elsize);
				return data.ToDoubleSeries();
			}

			public override string ToString ()
			{
				IEnumerable<double> itr = (IEnumerable<double>)this;
				return (Name!=null?Name+"\n":"") + string.Join ("\n",itr.Select(x => "{0}".Args (x)));
			}
		}
	}

	namespace Int64 {
		public class Series : Series<long>
		{
			public NdArray Values {get;set;}

			public Series(NdArray values)
			{
				DType = DType.Long;
				Values = values;
			}

			public static implicit operator Series(NdArray values) {
				return new Series (values);
			}

			public static implicit operator NdArray(Series series) {
				return series.Values;
			}

			public override int Count
			{
				get{ return (int)Values.Shape.Dimensions [0].Length;}
			}

			public override NumCIL.Series Clone()
			{
				return new Series (this.Values.Clone ());
			}

			internal override object At(int index)
			{
				return this [index];
			}

			public override IEnumerator<long> GetEnumerator() 
			{
				foreach (var x in Values.Value as IEnumerable<long>) {
					yield return x;
				}
			}

			public override long this[int index]
			{
				get {
					return Values.Value[index];
				}
				set { 
					Values.Value [index] = value;
				}
			}

			public override NumCIL.Series this [Range range] {
				get {
					return new Series(Values[range]);
				}
			}

			public unsafe override void  ToBuffer(byte[] buf, DType buftype, int iheight, int icol)
			{
				var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
				var fieldOffset = buftype.FieldOffset (icol);
				var dtype = buftype.Fields[icol];
				long[] data = (this.Values as Int64.NdArray).DataAccessor.AsArray();
				int elsize = sizeof(long);
				fixed(void *dst = &buf[0])
				fixed(void *src = &data[0])
				ColumnCopy(dst, fieldOffset, bytesPerRow, src, 0, elsize, iheight, elsize);
			}

			public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
			{
				var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
				var fieldOffset = buftype.FieldOffset (icol);
				var dtype = buftype.Fields[icol];
				var data = new long[iheight];
				int elsize = sizeof(long);
				fixed(void *src = &buf[0])
				fixed(void *dst = &data[0])
				ColumnCopy(dst, 0, elsize, src, fieldOffset, bytesPerRow, iheight, elsize);
				return data.ToLongSeries ();
			}

			public override string ToString ()
			{
				IEnumerable<double> itr = (IEnumerable<double>)this;
				return (Name!=null?Name+"\n":"") + string.Join ("\n",itr.Select(x => "{0}".Args (x)));
			}
		}
	}

	public class SeriesList : IEnumerable<Series>
	{
		protected List<Series> Series = new List<Series> ();

		public int Count { get{ return Series.Count; } }

		public IEnumerator<NumCIL.Series> GetEnumerator () { return Series.GetEnumerator (); }

		IEnumerator IEnumerable.GetEnumerator () { return Series.GetEnumerator (); }

		public DType DType = new DType(typeof(IDictionary<string,object>));

		public event Action<SeriesList, IEnumerable<Series>, IEnumerable<Series>> SeriesListChanged;

		public int Add(Series s, string name=null){
			if (name != null)
				s.Name = name;
			this.Series.Add (s);
			SeriesListChanged(this, new Series[]{s}, new Series[0]);
			this.DType.Fields.Add (s.DType);
			return this.Series.Count - 1;
		}

		public Series this[int i] {
			get {
				return Series [i];
			}
		}

		public string ToString(object[] args)
		{
			return DType.sep.Joined (
				Series.Select((s, i)=>s.DType.ToString(args[i]))
			);
		}

		public override string ToString ()
		{
			return string.Join (DType.sep, this.Series.Select (x => "{0}".Args(x.Name)));
		}
	}

	public class RowsList : IEnumerable<object[]>
	{
		protected DataFrame df;
		public int Count;

		public RowsList(DataFrame df) 
		{
			this.df = df;
		}

		public object[] this[int row] 
		{
			get{ return this.df.Columns.Select (col => col.At(row)).ToArray(); }
		}

		public IEnumerator<object[]> GetEnumerator ()
		{
			for (int i = 0; i < Count; i++)
				yield return this [i];
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return ((IEnumerable<object[]>)this).GetEnumerator ();
		}

		public override string ToString ()
		{
			return ToString (5, 5);
		}

		public void OnColumnsChanged(SeriesList series, IEnumerable<Series> removed, IEnumerable<Series> added)
		{
			Series[] rm = removed.ToArray ();
			if(rm.Length==0)
				foreach (var s in added) {
					Count = Math.Max (Count, s.Count);
				}
			else {
				int count = 0;
				foreach (var s in series)
					count = Math.Max (count, s.Count);
				Count = count;
			}
		}

		public string ToString (int head, int tail)
		{
			var sb = new StringBuilder ();
			int row = 0;
			for(row=0;row<Math.Min(head,this.Count);row++)
				sb.AppendFormat("[{0}] {1}\n".Args(row, DType.sep.Joined(this.df.Columns.ToString(this[row]))));
			sb.Append ("...\n");
			for(row=Math.Max(row+1,this.Count-tail);row<this.Count;row++)
				sb.AppendFormat("[{0}] {1}\n".Args(row, DType.sep.Joined(this.df.Columns.ToString(this[row]))));
			return sb.ToString ();
		}
	}

	public class DataFrame 
	{
		public SeriesList Columns = new SeriesList ();
		public RowsList Rows;

		public DType DType {
			get { return Columns.DType; } 
		}

		public DataFrame()
		{
			Rows = new RowsList (this);
			Columns.SeriesListChanged += this.OnColumnsChanged;
		}

		public DataFrame(IEnumerable<Series> series)
			: this()
		{
			foreach (var x in series) {
				this.Columns.Add (x);
			}
		}

		public DataFrame Clone() 
		{
			var df = new DataFrame (this.Columns.Select(x=>x.Clone()));
			return df;
		}

		public void OnColumnsChanged(SeriesList series, IEnumerable<Series> removed, IEnumerable<Series> added)
		{
			Rows.OnColumnsChanged (series, removed, added);
		}

		public DataFrame this [Range range] {
			get {
				var df = new DataFrame ();
				foreach (var col in df.Columns) {
					df.Columns.Add (col [range]);
				}
				return df;
			}
		}


		public static DataFrame FromBuffer(byte[] buf, DType buftype, int iheight)
		{
			var df = new DataFrame();
			var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
			if(buf.Length < bytesPerRow*iheight)
				throw new InvalidOperationException("buf length is {0} but {1} expected".Args(buf.Length, bytesPerRow*iheight));
			for (int i = 0; i < buftype.Fields.Count; i++) {
				var s = NumCIL.Series.FromBuffer (buf, buftype, iheight, i); 
				s.Name = buftype.Name ?? "[{0}]".Args (i);
				df.Columns.Add (s);
				df.Rows.Count = Math.Max (df.Rows.Count, s.Count);
			}
			return df;
		}

		public byte[] ToBuffer() 
		{
			byte[] buf = new byte[this.Rows.Count*this.DType.FieldOffset(this.DType.Fields.Count)];
			for (int i = 0; i < Columns.Count; i++) {
				Columns [i].ToBuffer(buf, this.DType, this.Rows.Count, i);
			}
			return buf;
		}

		public Series this[int index] 
		{
			get{ return this.Columns [index]; }
		}

		public int Add(Series series, string name=null)
		{
			return this.Columns.Add (series, name);
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ();
			sb.Append (Columns.ToString ());
			sb.Append ("\n");
			sb.Append (Rows.ToString ());
			return sb.ToString ();
		}
	}
}

