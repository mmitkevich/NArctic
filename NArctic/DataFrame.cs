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
using NumCIL.Unsafe;
using NumCIL.Boolean;
using NumCIL.Complex128;
using System.Collections;
using System.Text;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MongoDB.Bson;
using Utilities;

namespace NumCIL
{
	public abstract class Series : IEnumerable
	{
		public string Name {get;set;}
		public DType DType {get;set;}

		public abstract int Count{ get; }

		public Series<T> As<T>() {
			return this as Series<T>;
		}

		internal abstract object At(int index);

		public static unsafe Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
		{
			var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
			var fieldOffset = buftype.FieldOffset (icol);
			Series rtn = null;
			var gh = GCHandle.Alloc(buf, GCHandleType.Pinned);
			try{
				IntPtr adr = gh.AddrOfPinnedObject();
				if (buftype.Fields[icol].Type == typeof(double)) {
					double[] data = new double[iheight];
					CopyTable.CopyColumn ((double[])data, adr, bytesPerRow, fieldOffset);
					return new Series<double, double>(buftype.Fields[icol], new NdArray<double>(data), g=>g, s=>s);
				}else if (buftype.Fields[icol].Type == typeof(long)) {
					long [] data = new long[iheight];
					CopyTable.CopyColumn ((long[])data, adr, bytesPerRow, fieldOffset);
					return new Series<long, long>(buftype.Fields[icol], new NdArray<long>(data), g=>g, s=>s);
				}else if (buftype.Fields[icol].Type == typeof(DateTime)) {
					long [] data = new long[iheight];
					CopyTable.CopyColumn ((long[])data, adr, bytesPerRow, fieldOffset);
					var epoch = new DateTime(1970,01,01);
					return new Series<DateTime, long>(buftype.Fields[icol], new NdArray<long>(data), g=>epoch.AddTicks(g/100), s=>s.Subtract(epoch).Ticks*100);
				}else
					throw new InvalidOperationException("Failed decode {0} type".Args(buftype.Fields[icol].Type));
				return rtn;
			}finally{
				gh.Free ();
			}
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}
	}

	public abstract class Series<T>:Series, IEnumerable<T>
	{
		public abstract IEnumerator<T> GetEnumerator ();

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

		public int Add(Series s){
			this.Series.Add (s);
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
		public Shape Shape;

		public DataFrame (Shape shape)
		{
			Shape = shape;
			Rows = new RowsList (this);
		}

		public static DataFrame FromBuffer(byte[] buf, DType buftype, int iheight)
		{
			var df = new DataFrame(new Shape(new long[]{iheight, buftype.Fields.Count}));
			for (int i = 0; i < buftype.Fields.Count; i++) {
				var s = NumCIL.Series.FromBuffer (buf, buftype, iheight, i); 
				s.Name = buftype.Name ?? "[{0}]".Args (i);
				df.Columns.Add (s);
				df.Rows.Count = Math.Max (df.Rows.Count, s.Count);
			}
			return df;
		}

		public Series this[int index] 
		{
			get{ return this.Columns [index];}
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

