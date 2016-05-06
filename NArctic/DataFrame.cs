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

namespace NumCIL
{

	public static class Builder
	{
		public static T Return<T>(T arg){
			return arg;
		}

		public static Series<T> ToSeries<T>(this T[] data, DType dtype)
		{
			return new Series<T, T>(dtype, new NdArray<T>(data), Return<T>, Return<T>);
		}

		public static Series<T> ToSeries<T>(this NdArray<T> data, DType dtype)
		{
			return new Series<T, T>(dtype, data, Return<T>, Return<T>);
		}


		public static Series<DateTime> ToDateTimeSeries(this DateTime[] data)
		{
			return data.Select (DType.DateTimeToNanos).ToArray ().ToDateTimeSeries ();
		}

		public static Series<DateTime> ToDateTimeSeries(this long[] data)
		{
			return new Series<DateTime, long> (DType.DateTime64, 
				new NdArray<long>(data), 
				DType.NanosToDateTime, DType.DateTimeToNanos);
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

		public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
		{
			var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
			var fieldOffset = buftype.FieldOffset (icol);
			var dtype = buftype.Fields[icol];
			if (buftype.Fields [icol].Type == typeof(double)) {
				var data = new double[iheight];
				int elsize = sizeof(double);
				fixed(void *src = &buf[0])
					fixed(void *dst = &data[0])
						ColumnCopy(dst, 0, elsize, src, fieldOffset, bytesPerRow, iheight, elsize);
				return data.ToSeries (dtype);
			}else if (buftype.Fields[icol].Type == typeof(long)) {
				var data = new long[iheight];
				int elsize = sizeof(long);
				fixed(void *src = &buf[0])
					fixed(void *dst = &data[0])
						ColumnCopy(dst, 0, elsize, src, fieldOffset, bytesPerRow, iheight, elsize);
				return data.ToSeries (dtype);
			}else if (buftype.Fields[icol].Type == typeof(DateTime)) {
				var data = new long[iheight];
				int elsize = sizeof(long);
				fixed(void *src = &buf[0])
					fixed(void *dst = &data[0])
						ColumnCopy(dst, 0, elsize, src, fieldOffset, bytesPerRow, iheight, elsize);
				return data.ToDateTimeSeries();
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
			return data.ToSeries (DType.Long);
		}

		public static implicit operator Series (double[] data) {
			return data.ToSeries (DType.Double);
		}

		public static implicit operator Series (DateTime[] data) {
			return data.ToDateTimeSeries ();
		}

		public static implicit operator Series (NdArray<long> data) {
			return data.ToSeries (DType.Long);
		}

		public static implicit operator Series (NdArray<double> data) {
			return data.ToSeries (DType.Double);
		}
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

	public class Series<T,Q> : Series<T>
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

		public override Series this [Range range] {
			get {
				return new Series<T,Q> (DType, Values[range], Getter, Setter);
			}
		}

		public unsafe override void  ToBuffer(byte[] buf, DType buftype, int iheight, int icol)
		{
			var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
			var fieldOffset = buftype.FieldOffset (icol);
			var dtype = buftype.Fields[icol];
			if (buftype.Fields [icol].Type == typeof(double)) {
				double[] data = (this.Values as NdArray<double>).DataAccessor.AsArray();
				int elsize = sizeof(double);
				fixed(void *dst = &buf[0])
					fixed(void *src = &data[0])
						ColumnCopy(dst, fieldOffset, bytesPerRow, src, 0, elsize, iheight, elsize);
			}else if (buftype.Fields[icol].Type == typeof(long)) {
				long[] data = (this.Values as NdArray<long>).DataAccessor.AsArray();
				int elsize = sizeof(long);
				fixed(void *dst = &buf[0])
				fixed(void *src = &data[0])
				ColumnCopy(dst, fieldOffset, bytesPerRow, src, 0, elsize, iheight, elsize);
			}else if (buftype.Fields[icol].Type == typeof(DateTime)) {
				long[] data = (this.Values as NdArray<long>).DataAccessor.AsArray();
				int elsize = sizeof(long);
				fixed(void *dst = &buf[0])
				fixed(void *src = &data[0])
				ColumnCopy(dst, fieldOffset, bytesPerRow, src, 0, elsize, iheight, elsize);
			}else
				throw new InvalidOperationException("Failed decode {0} type".Args(buftype.Fields[icol].Type));
		}

		public override string ToString ()
		{
			IEnumerable<T> itr = (IEnumerable<T>)this;
			return (Name!=null?Name+"\n":"") + string.Join ("\n",itr.Select(x => "{0}".Args (x)));
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

		public DataFrame(IDictionary<string, Series> series)
			: this()
		{
			foreach (var x in series) {
				this.Columns.Add (x.Value, x.Key);
			}
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

