using System;
using System.Linq;
using System.Collections.Generic;
using Meta.Numerics;
using Meta.Numerics.Statistics;

namespace NArctic
{
	public static class MetaNumericsMixin
	{
		public static MultivariateSample ToSample(this IEnumerable<Series> series)
		{
			var sl = series.ToArray ();
			var sample = new MultivariateSample (sl.Length);
			var row = new double[sl.Length];
			var count = series.Select (s => s.Count).Max ();
			for (int i = 0; i < sl.Length; i++) {
				row [i] = sl[i].As<double>()[i];
				sample.Add (row);
			}
			return sample;
		}

		public static IEnumerable<double> Generate(this Randoms.IRandomGenerator gen, int count, Random rng=null)
		{
			for(int i=0; i<count; i++)
				yield return gen.GetNext(rng);
		}
	}


	public static class Generate
	{
		public static NumCIL.Double.NdArray Random(int count, Randoms.IRandomGenerator gen = null)
		{
			gen = gen ?? new Randoms.Uniform ();
			return new NumCIL.Double.NdArray(gen.Generate(count).ToArray());
		}
	}
}

