using System;
using System.Linq;
using System.Collections.Generic;
using Utilities;
using NArctic;
using NumCIL;
using MongoDB.Driver;
using System.Diagnostics;
using System.Threading;

namespace NArctic.Tests
{
	public class MainClass
	{
		public static void TestDType(string str)
		{
			Console.WriteLine("Begin Parse<{0}>".Args(str));
			try {
				var dt = new DType (str);
				Console.WriteLine("Done Parse<{0}> ===> {1}".Args(str, dt.ToString()));
			}catch(InvalidOperationException e){
				Console.WriteLine("Failed Parse<{0}> ===> {1}".Args(str, e.Message));
			}
				
		}
		public static void TestDTypes()
		{
			//TestDType ("'f8'");
			//TestDType ("'i8'");
			//TestDType ("[('f1','f8')]");
			//TestDType ("[('f1','f8'),('f2','i8')]");
			//TestDType ("[('f1','f8'),('f2','i8')");
			//TestDType ("[('f1','f8'),('f2','i8'");
			//TestDType ("[('f1,'f8'),('f2','i8'");
			TestDType ("[('index', '<M8[ns]'), ('Open', '<f8'), ('Close', '<f8'), ('Adj Close', '<f8'), ('High', '<f8'), ('Low', '<f8'), ('Volume', '<i8')]");
		}

		public static void TestReadArctic(string dbname="arctic_bench", string host="localhost"){
			var driver = new MongoClient ("mongodb://"+host);
			var db = driver.GetDatabase (dbname);

			var arctic = new Arctic (db);
			Stopwatch sw = new Stopwatch ();
			sw.Start ();
			var df = arctic.ReadDataFrameAsync ("S0").Result;
			Console.WriteLine (df);
			sw.Stop ();
			Console.WriteLine ("read {0} took {1}s = {2}/sec".Args (df.Rows.Count, sw.Elapsed.TotalSeconds, df.Rows.Count/sw.Elapsed.TotalSeconds));
		}

		public static DataFrame RandomWalk(DateTime start, DateTime stop, int count)
		{
			var df = new DataFrame { 
				NumCIL.Time.Series.DateTimeRange(start, stop, count)
			};
			return df;
		}

		public static void TestWriteArctic(string dbname="arctic_net", string host="localhost", bool purge=true) {
			var driver = new MongoClient ("mongodb://"+host);
			if (purge)
				driver.DropDatabase (dbname);
			
			var db = driver.GetDatabase (dbname);

			var arctic = new Arctic (db);
			Stopwatch sw = new Stopwatch ();
			var df = new DataFrame();
			df.Columns.Add (new []{new DateTime(2015,1,1),new DateTime(2015,1,2),new DateTime(2015,1,3),new DateTime(2015,1,4),new DateTime(2015,1,5)}, "index");
			df.Columns.Add (new long[]{1, 2, 3, 4, 5}, "long");
			df.Columns.Add (new double[]{1, 2, 3, 4, 5}, "double");
			var s = df.ToString ();
			Console.WriteLine (s);

			var df2 = df.Clone ();
			df2 [0].AsDateTime.Values += TimeSpan.FromDays (30).ToDateTime64 ();
			Console.WriteLine ("added 1 day:\n{0}".Args (df2));
			sw.Start ();
			var version = arctic.AppendDataFrameAsync ("S1", df).Result;
			var version2 = arctic.AppendDataFrameAsync ("S1", df2	).Result;
			sw.Stop ();
			Console.WriteLine ("write {0} took {1}s = {2}/sec -> ver:\n {3}".Args (df.Rows.Count, sw.Elapsed.TotalSeconds, df.Rows.Count/sw.Elapsed.TotalSeconds, version));
		}

		public static void Main (string[] args)
		{
//			TestDTypes ();
			TestWriteArctic("arctic_net");
			TestReadArctic("arctic_net");


			Console.WriteLine ("DONE");
		}
	}
}
