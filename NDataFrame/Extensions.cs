namespace NDataFrame
{
	public static class Extensions
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

		public static Double.Series ToDoubleSeries(this Double.NdArray values)
		{
			return new Double.Series(values);
		}

		public static Int64.Series ToLongSeries(this long[] data)
		{
			return new Int64.Series(new Int64.NdArray(data));
		}

		public static Int64.Series ToLongSeries(this Int64.NdArray values)
		{
			return new Int64.Series(values);
		}

		public static Time.Series ToDateTimeSeries(this DateTime[] data)
		{
			return data.Select (DateTime64.ToDateTime64).ToArray ().ToDateTimeSeries ();
		}

		public static Time.Series ToDateTimeSeries(this long[] data)
		{
			return new Time.Series(new Int64.NdArray(data));
		}

		public static Time.Series ToDateTimeSeries(this Int64.NdArray values)
		{
			return new Time.Series(values);
		}

		public static MultivariateSample ToSample(this IEnumerable<Series> series)
		{
			var sl = series.ToArray ();
			var sample = new MultivariateSample (sl.Length);
			var row = new double[sl.Length];
			var count = series.Select (s => s.Count).Max ();
			for (int i = 0; i < sl.Length; i++) {
				row [i] = (double)sl[i].At(i);
				sample.Add (row);
			}
		}
	}
}