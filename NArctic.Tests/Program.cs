using System;
using System.Linq;
using System.Collections.Generic;
using Utilities;
using NArctic;
using NumCIL;
using MongoDB.Driver;
using System.Diagnostics;

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

		public static void TestReadArctic(){
			var driver = new MongoClient ("mongodb://localhost");
			var db = driver.GetDatabase ("arctic_bench");

			var arctic = new Arctic (db);
			Stopwatch sw = new Stopwatch ();
			sw.Start ();
			var df = arctic.ReadDataFrameAsync ("S0").Result;
			Console.WriteLine (df);
			sw.Stop ();
			Console.WriteLine ("read {0} took {1}s = {2}/sec".Args (df.Rows.Count, sw.Elapsed.TotalSeconds, df.Rows.Count/sw.Elapsed.TotalSeconds));
		}

		public static void Main (string[] args)
		{
//			TestDTypes ();
			TestReadArctic();
			Console.WriteLine ("DONE");
		}
	}
}
