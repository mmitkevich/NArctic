using System;
using System.Collections.Generic;
using MongoDB.Bson;
using System.Text;
using LZ4;
using System.Linq;
using System.Configuration;
using System.Collections;

namespace Utilities
{
	public static class String
	{
		public static string Args(this string fmt, params object[] args)
		{
			return string.Format (fmt, args);
		}
		public static string Joined(this string sep, params object[] args)
		{
			return sep.Joined (args.Select (x => "{0}".Args (x)));
		}
		public static string Joined(this string sep, IEnumerable<string> args)
		{
			return string.Join (sep, args);
		}
	}

	public static class Mongo
	{
		public static string ToString(this IList<BsonDocument> lst, string sep)
		{
			var sb = new StringBuilder ();
			foreach (var doc in lst) {
				sb.Append (doc.ToString ());
				sb.Append (sep);
			}
			return sb.ToString ();
		}
	}

	public static class Unsafe
	{
		public static unsafe void Memmove(byte *dest, byte *src, ulong len)
		{
			for (ulong i = 0; i < len; i++)
				dest [i] = src [i];
		}
	}

	public class ByteBuffer
	{
		public byte[] Data;
		public int Length;

		public ByteBuffer(int capacity = 1024168)
		{
			Data = new byte[capacity];
		}

		public void EnsureCapacity(int capacity)
		{
			if (Data.Length<capacity) {
				byte[] next = new byte[capacity * 2];
				Buffer.BlockCopy (Data, 0, next, 0, Length);
				Data = next;
			}
		}

		public void Append(byte[] more, int len=-1)
		{
			if (len < 0)
				len = more.Length;
			EnsureCapacity (Length + len);
			Buffer.BlockCopy (more, 0, Data, Length, len);
			Length += len;
		}

		public void AppendDecodedLZ4(byte[] encoded, int len=-1)
		{
			if(len<0)
				len = (int)((uint)encoded[0] + ((uint)encoded[1]<<8) + ((uint)encoded[2]<<16)  + ((uint)encoded[3]<<24));
			EnsureCapacity (Length + len);
			LZ4Codec.Decode(encoded, 4, encoded.Length-4, Data, Length, len, true);
			Length += len;
		}
	}

	public static class Dictionary
	{
		public static TValue Get<TKey,TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue def = default(TValue))
		{
			TValue val;
			if (!dict.TryGetValue(key, out val))
				return def;
			return val;
		}

		public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue val)
		{
			TValue v;
			if (dict.TryGetValue(key, out v))
				return v;
			dict.Add(key, val);
			return val;
		}

		public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key) where TValue :class,new()
		{
			TValue v;
			if (dict.TryGetValue(key, out v))
				return v;
			v = new TValue();
			dict.Add(key, v);
			return v;
		}


		public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TKey,TValue> getval)
		{
			TValue val;
			if (dict.TryGetValue(key, out val))
				return val;
			dict.Add(key, val=getval(key));
			return val;
		}
	}
}

