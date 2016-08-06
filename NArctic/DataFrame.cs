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
using System.Collections;
using System.Text;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MongoDB.Bson;
using Utilities;
using NumCIL;

namespace NArctic
{
	public class SeriesList : IEnumerable<Series>
	{
		protected List<Series> Series = new List<Series> ();
        protected Dictionary<string, Series> SeriesByName = new Dictionary<string, NArctic.Series>();

		public int Count { get{ return Series.Count; } }

		public IEnumerator<Series> GetEnumerator () { return Series.GetEnumerator (); }

		IEnumerator IEnumerable.GetEnumerator () { return Series.GetEnumerator (); }

		public DType DType = new DType(typeof(IDictionary<string,object>));

		public event Action<SeriesList, IEnumerable<Series>, IEnumerable<Series>> SeriesListChanged;

		public int Add(Series s, string name=null){
            if (name != null)
                s.Name = name;
            else if (s.Name == null)
                s.Name = this.Series.Count.ToString();
			this.Series.Add (s);
            this.SeriesByName[s.Name] = s;
            if(SeriesListChanged!=null)
			    SeriesListChanged(this, new Series[0], new Series[] { s });
			this.DType.Fields.Add (s.DType);
            s.DType.Parent = this.DType;
			return this.Series.Count - 1;
		}

		public Series this[int column] {
			get {
				return Series [column];
			}
		}

        public Series this[string column]
        {
            get {
                return SeriesByName[column];
            }
        }

        public bool Contains(string column)
        {
            return SeriesByName.ContainsKey(column);
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

    public class Ring : IEnumerable<int>
    {
        public int Head;    // Free index (write to it)
        public int Tail;    // First used index (read from it)
        public Func<int> Capacity;

        public Ring(Func<int> capacity)
        {
            this.Capacity = capacity;
            this.Head = 0;
            this.Tail = 0;
        }

        public Range[] Ranges
        {
            get
            {
                if (Tail <= Head)
                    return new[] { Range.R(Tail, Head) };
                else
                    return new[] { Range.R(Tail, Capacity()), Range.R(0, Head) };
            }
        }


        public int Enqueue()
        {
            int next = (Head + 1) % Capacity();
            if (next == Tail)
                return -1;
            int head = Head;
            Head = next;
            //Console.WriteLine("Head={0}", Head);
            return head;
        }

        public int Dequeue()
        {
            if (Head == Tail)
                return -1;
            int tail = Tail;
            Tail = (Tail + 1) % Count;
            //Console.WriteLine("Tail={0}", Tail);
            return tail;
        }

        public void Clear()
        {
            //Console.WriteLine("Clear Tail={0} to Head={1}", Tail, Head);
            Tail = Head = 0;
        }

        public IEnumerator<int> GetEnumerator()
        {
            int head = Head;
            while (head != Tail)
            {
                yield return head;
                head = (head + 1) % Capacity();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<int>)this).GetEnumerator();
        }

        public int Count
        {
            get
            {
                int used = (Head - Tail);
                if (used < 0)
                    used = Head + Capacity() - Tail;
                return used;
            }
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
                    if (s.Count > Count)
                        Count = s.Count;
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

    public class DataMap<K, V> : IDictionary<K, V>
    {
        public DataFrame DataFrame;
        private BaseSeries<K> keys;
        private BaseSeries<V> values;

        public DataMap(DataFrame df, BaseSeries<K> keys, BaseSeries<V> values)
        {
            this.DataFrame = df;
            this.keys = keys;
            this.values = values;
        }

        public V this[K key]
        {
            get
            {
                int index = keys.IndexOf(key);
                if (index < 0)
                    throw new KeyNotFoundException(key.ToString());
                return values[index];
            }

            set
            {
                int index = keys.IndexOf(key);
                if (index < 0)
                    throw new KeyNotFoundException(key.ToString());
                values[index] = value;
            }
        }

        public int Count
        {
            get { return DataFrame.Count; }
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        public ICollection<K> Keys
        {
            get
            {
                return keys;
            }
        }

        public ICollection<V> Values
        {
            get
            {
                return values;
            }
        }

        public void Add(KeyValuePair<K, V> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(K key, V value)
        {
            this.DataFrame.Count++;
            this.keys.Add(key);
            this.values.Add(value);
        }

        public void Clear()
        {
            this.DataFrame.Count = 0;
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(K key)
        {
            var index = keys.IndexOf(key);
            return index >= 0 && index < Count;
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            for(int i=0;i<keys.Count;i++)
            {
                yield return new KeyValuePair<K,V>(keys[i], values[i]);
            }
        }
        public bool Remove(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(K key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out V value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<K,V>>)this).GetEnumerator();
        }
    }

	public class DataFrame : IEnumerable<Series>
	{
		public SeriesList Columns = new SeriesList ();
		public RowsList Rows;
        public Series Index;
        private int usedCount;

        public DType DType {
			get { return Columns.DType; } 
		}

		public DataFrame()
		{
            Rows = new RowsList (this);
			Columns.SeriesListChanged += this.OnColumnsChanged;
		}

        public DataFrame(IDictionary<string, Series> series)
            :this()
        {
            foreach (var x in series)
            {
                this.Columns.Add(x.Value, x.Key);
            }
            //this.Index = index.Get(i => this.Columns[i]);
        }

        public DataFrame(IEnumerable<Series> series, string index=null)
			: this()
		{
			foreach (var x in series) {
				this.Columns.Add (x, x.Name);
			}
            this.Index = index.Get(i => this.Columns[i]);
		}

        public DataFrame(long count, Type[] types = null, string[] names=null, string index=null) 
            : this()
        {
            Rows.Count = (int)count;
            if (types != null)
            {
                for (int i = 0; i < types.Length; i++)
                    Columns.Add(Series.FromType(types[i], count),names!=null && i<names.Length ? names[i] : $"{i}");
            }
        }

        public int Count {
            get {
                return this.usedCount;
            }
            set {
                this.usedCount = value;
                if (this.usedCount > Rows.Count)
                {
                    int maxCount = 0;
                    // need grow
                    foreach(var c in Columns)
                    {
                        if (c.Count < 2 * usedCount)
                            c.Count = 2 * usedCount;
                        if (c.Count > maxCount)
                            maxCount = c.Count;
                    }
                    Rows.Count = Math.Max(usedCount, maxCount);
                }
            }
        }  

        public DataFrame UsedRange {
            get {
                return this[0, Count];
            }
        }

        public TypedFrame<T> As<T>() where T:new()
        {
            return new TypedFrame<T>(this);
        }

        public Ring ToRing()
        {
            return new Ring(() => this.Rows.Count);
        }

        public BaseSeries<T> Col<T>(string col)
        {
            return this[col,typeof(T)].As<T>();
        }

        public Series this[string column]
        {
            get { return Columns[column]; }
        }

        public DataFrame this[int start, int stop]
        {
            get { return this[new Range(start, stop)]; }
        }

        public Series this[string column, Type t]
        {
            get {
                if (Columns.Contains(column))
                    return Columns[column];
                var s = Series.FromType(t, Rows.Count);
                this.Columns.Add(s, column);
                return s;
            }
        }

        public object this[string column, int row]
        {
            get {
                if (!column.Contains(column))
                    return null;
                return this[column].At(row);
            }
            set {
                if (row+1 > this.Count)
                    this.Count = row + 1;

                Series s = this[column, value.GetType()];
                s.Set(row, value);
            }
        }

        public BaseSeries<T> GetOrAdd<T>(string column)
        {
            if (Columns.Contains(column))
                return Columns[column].As<T>();
            var s = new Series<T>(this.Rows.Count);
            this.Columns.Add(s, column);
            return s;
        }

        public DataFrame Clone() 
		{
			var df = new DataFrame (this.Columns.Select(x=>x.Clone()), index:this.Index.Get(i=>i.Name));

			return df;
		}

		public void OnColumnsChanged(SeriesList series, IEnumerable<Series> removed, IEnumerable<Series> added)
		{
			Rows.OnColumnsChanged (series, removed, added);
		}

		public DataFrame this [Range range] {
			get {
                var rtn = new DataFrame(Columns.Select(c=>c[range]), index:this.Index.Get(i=>i.Name));
                return rtn;

            }
        }

        public DataFrame Head(int count)
        {
            return this[Range.R(0, count)];
        }

        public DataFrame Tail(int count)
        {
            return this[Range.R(this.Rows.Count-count-1, count)];
        }

        public DataFrame Loc<T>(T key, int indexColumn=0)
        {
            var s = this[indexColumn].As<T>();
            int row = s.IndexOf(key);
            return this[Range.R(row)];
        }

        public IDictionary<K, V> AsMap<K,V>(int indexColumn, int valuesColumn)
        {
            return new DataMap<K, V>(this, this[indexColumn].As<K>(), this[valuesColumn].As<V>());
        }

		public static DataFrame FromBuffer(byte[] buf, DType buftype, int iheight)
		{
			var df = new DataFrame();
			var bytesPerRow = buftype.FieldOffset (buftype.Fields.Count);
			if(buf.Length < bytesPerRow*iheight)
				throw new InvalidOperationException("buf length is {0} but {1} expected".Args(buf.Length, bytesPerRow*iheight));
			for (int i = 0; i < buftype.Fields.Count; i++) {
				var s = Series.FromBuffer (buf, buftype, iheight, i); 
				s.Name = buftype.Fields[i].Name ?? "[{0}]".Args (i);
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

		public IEnumerator<Series> GetEnumerator ()
		{
			return this.Columns.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return this.Columns.GetEnumerator();
		}
	}
}

