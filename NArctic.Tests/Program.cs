
using System;
using System.Linq;
using System.Collections.Generic;
using Utilities;
using NArctic;
using NumCIL;
using MongoDB.Driver;
using System.Diagnostics;
using System.Threading;
using NArctic.Randoms;
using Serilog;
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

		public static void TestReadArctic(string lib="bench.securities", string host="localhost", string symbol="S1"){
			var driver = new MongoClient ("mongodb://"+host);
			var arctic = new Arctic (driver, lib);
			Stopwatch sw = new Stopwatch ();
			sw.Start ();
			var df = arctic.ReadAsync (symbol).Result;
			sw.Stop ();
			if (df != null) {
				Console.WriteLine (df);
				Console.WriteLine ("read {0} took {1}s = {2}/sec".Args (df.Rows.Count, sw.Elapsed.TotalSeconds, df.Rows.Count / sw.Elapsed.TotalSeconds));
			} else {
				Console.WriteLine ("Not found {0}".Args (symbol));
			}
		}

		public static DataFrame RandomWalk(int count, DateTime start, DateTime stop)
		{
            Console.WriteLine("RandomWalk generating {0}".Args(count));
			var df = new DataFrame { 
				DateTimeSeries.Range(count, start, stop),
				Series.Random(count, new BoxMullerNormal()).Apply(v => (v*1e-4).CumSum().Exp()),
			};
            df.Index = df[0];
			Console.WriteLine (df);
			return df;
		}

		public static DataFrame SampleDataFrame(DateTime start=default(DateTime)) {
			start = start == default(DateTime) ? DateTime.Now : start;
			var df = new DataFrame();
			df.Columns.Add (new []{start,start.AddDays(1),start.AddDays(2),start.AddDays(3)}, "index");
			//df.Columns.Add (new long[]{1, 2, 3, 4}, "long");
			df.Columns.Add (new double[]{1, 2, 3, 4}, "double");
			Console.WriteLine ("new dataframe:\n {0}".Args(df));
	
			//var df2 = df.Clone ();
			//df2 [0].AsDateTime64 += TimeSpan.FromDays (30).ToDateTime64 ();
			//Console.WriteLine ("and append dataframe:\n{0}".Args (df2));
			return df;
		}
        //public const int SIZE = 24 * 60 * 60 * 365;
        public const int SIZE = 1000000;
        public const int CHUNKSIZE = 100000;
		public static DataFrame RandomDataFrame(DateTime start=default(DateTime), TimeSpan delta = default(TimeSpan), int count=SIZE) {
			start = start == default(DateTime) ? DateTime.Now : start;
            delta = delta == default(TimeSpan) ? TimeSpan.FromSeconds(1): delta;
			return RandomWalk (count, start, start+delta.Mul(count));
		}

		public static void TestWriteArctic(string lib="net.securities", string host="localhost", bool purge=true, bool del=true, string symbol="S1") {
			var driver = new MongoClient ("mongodb://"+host);
			var arctic = new Arctic (driver, lib, purge:purge);

			if(del) {
				var delcnt = arctic.DeleteAsync(symbol).Result;
				Console.WriteLine("Deleted {0} versi\tons for {1}".Args(delcnt, symbol));
			}

			//var df = SampleDataFrame ();
			var df = RandomDataFrame();
			//var df2 = SampleDataFrame (df[0].AsDateTime()[-1]);

			Stopwatch sw = new Stopwatch ();
			sw.Start ();
			var version = arctic.AppendAsync (symbol, df, CHUNKSIZE).Result;
			//var version2 = arctic.AppendAsync (symbol, df2, CHUNKSIZE).Result;
			sw.Stop ();
            long rows = df.Rows.Count;// + df2.Rows.Count;
			Console.WriteLine ("write {0} took {1}s = {2}/sec -> ver:\n {3}".Args (rows, sw.Elapsed.TotalSeconds, rows/sw.Elapsed.TotalSeconds, version));
		}

        public static void TestCircularDataframe()
        {
            var df = new DataFrame(10, new[] { typeof(double), typeof(long) },new[] { "Dbl", "Lng"});
            var rng = df.ToRing();

            for(int i=0;i<20;i++)
            {
                var head = rng.Enqueue();
                df[0].As<double>()[head] = i;
                var tail = rng.Dequeue();
                Console.WriteLine("step {0}\n{1}".Args(i,df));
            }
        }

        class TestClass
        {
            public double Dbl { get; set; }
            public long Lng { get; set; }

            public override string ToString()
            {
                return "TestClass(Dbl:{0}, Lng:{1})".Args(Dbl, Lng);
            }
        }

        public static DataFrame TestReflection(int count=100000)
        {
            var tf = new TypedFrame<TestClass>(count);
            for (int i = 0; i < tf.Count; i++)
            {
                tf[i] = new TestClass { Dbl = 10.5*i, Lng = i };
            }
            var rng = tf.ToRing();
            rng.Enqueue(new TestClass { Dbl = 1000, Lng = 2222 });
            rng.Enqueue(new TestClass { Dbl = 1001, Lng = 2222 });

            Console.WriteLine(tf.DataFrame);
            Console.WriteLine("tf[5]="+tf[5]);
            Console.WriteLine("tf.dequeue="+rng.Dequeue());

            var tfx = new TypedFrame<TestClass>(tf.DataFrame);

            Console.WriteLine("tfx[0]="+tfx[0]);

            return tf.DataFrame;
        }

        public static void TestIndex()
        {
            var start = new DateTime(2015, 1, 1);
            var df = SampleDataFrame(start);
            Console.WriteLine("Sample\n"+df);
            start = start.AddDays(1);
            var dr = df.Loc<DateTime>(start);
            Console.WriteLine("[{0}]={1}".Args(start, dr));

            var val = dr[1].As<double>()[0];
            Console.WriteLine("[{0}]={1}", start, val);

            IList<DateTime> keys = df[0].As<DateTime>();
            Console.WriteLine("keys[-1]={0}", keys[-1]);

            IDictionary<DateTime, double> map = df.AsMap<DateTime, double>(0, 1);
            map[start] = 100;
            Console.WriteLine("df[1]={0},{1}", df[0].As<DateTime>()[1], df[1].As<double>()[1]);

            foreach(var kv in map)
            {
                Console.WriteLine("map {0}={1}", kv.Key, kv.Value);
            }
        }

        public static void TestArcticDateTimeIndex(string lib = "net.securities", string host = "localhost", bool purge = true, bool del = true, string symbol = "S1")
        {
            var driver = new MongoClient("mongodb://" + host);
            var arctic = new Arctic(driver, lib, purge: purge);

            if (del)
            {
                var delcnt = arctic.DeleteAsync(symbol).Result;
                Console.WriteLine("Deleted {0} versi\tons for {1}".Args(delcnt, symbol));
            }

            var df = new DataFrame(new Series[] {
                new DateTimeSeries(new[] { new DateTime(2014, 1, 1), new DateTime(2014, 2, 1) }, name:"date"),
                new Series<long>(new long[] { 1, 2 }, name:"val")
            }, index: "date");

            var df2 = new DataFrame(new Series[] {
                new DateTimeSeries(new[] { new DateTime(2014, 3, 1), new DateTime(2014, 4, 1) }, name:"date"),
                new Series<long>(new long[] { 3, 4 }, name:"val")
            }, index: "date");

            var df4 = new DataFrame(new Series[] {
                new DateTimeSeries(new[] { new DateTime(2014, 3, 1), new DateTime(2014, 4, 1), new DateTime(2014, 5, 1) }, name:"date"),
                new Series<long>(new long[] { 3, 4, 5 }, name:"val")
            }, index: "date");

            var df3 = new DataFrame(new Series[] {
                new DateTimeSeries(new[] { new DateTime(2014, 5, 1), new DateTime(2014, 6, 1) }, name:"date"),
                new Series<long>(new long[] { 5, 6 }, name:"val")
            }, index: "date");

            Stopwatch sw = new Stopwatch();
            sw.Start();
            var version = arctic.AppendAsync(symbol, df, CHUNKSIZE).Result;
            var version2 = arctic.AppendAsync (symbol, df2, CHUNKSIZE).Result;
            var version3 = arctic.AppendAsync(symbol, df2, CHUNKSIZE).Result;
            var version4 = arctic.AppendAsync(symbol, df4, CHUNKSIZE).Result;

            var read1 = arctic.Read(symbol, new DateRange(new DateTime(2014, 3, 1)));
            Console.WriteLine(read1);

            var read2 = arctic.Read(symbol, new DateRange(new DateTime(2014, 1, 1), new DateTime(2014, 2, 1)));
            Console.WriteLine(read2);
            sw.Stop();
            long rows = df.Rows.Count;// + df2.Rows.Count;
            Console.WriteLine("write {0} took {1}s = {2}/sec -> ver:\n {3}".Args(rows, sw.Elapsed.TotalSeconds, rows / sw.Elapsed.TotalSeconds, version));
        }

        public static void TestMetadata(string lib = "net.securities", string host = "localhost", bool purge = true, bool del = true, string symbol = "S1")
        {
            var driver = new MongoClient("mongodb://" + host);
            var arctic = new Arctic(driver, lib, purge: purge);

            if (del)
            {
                var delcnt = arctic.DeleteAsync(symbol).Result;
                Console.WriteLine("Deleted {0} versi\tons for {1}".Args(delcnt, symbol));
            }
            var df = new DataFrame();
            df.Count = 2;
            df.Col<DateTime>("date")[0] = new DateTime(2015,1,1);
            df.Col<DateTime>("date")[1] = new DateTime(2015,2,1);
            df.Col<long>("value")[0] = 15;
            df.Metadata["sector"] = "internet";
            df.Index = df["date"];
            arctic.Append("AAPL", df);

            var df1 = arctic.Read("AAPL");
            Console.WriteLine($"AAPL metadata {df1.Metadata["sector"]}");
        }

        public static void TestGrowingCase()
        {
            var df = new DataFrame();
            df.Count = 3;   // "use" space
            Console.WriteLine($"Initial df {df}");
            for (var i = 0; i < 10; i++)
            {
                df["a", i] = i;
                df["b", i+1] = -i;
                Console.WriteLine($"Count={df.Count}, Rows.Count={df.Rows.Count}");
            }
            Console.WriteLine($"Final df {df}");
            df.Count = 0;
            Console.WriteLine($"Final df after free {df}");
        }

        public static void TestNMem()
        {
            ByteBuffer b = new ByteBuffer();
            b.Append<long>(50);
            var v = b.Read<long>(0);
            if ( v != 50)
                throw new InvalidOperationException();
        }
        public static void Main (string[] args)
		{
			Serilog.Log.Logger = new Serilog.LoggerConfiguration()
				.MinimumLevel.Debug()
				.WriteTo.Console()
				.CreateLogger();

            /* TestNMem();
             TestIndex();

             TestDTypes ();

             TestCircularDataframe();

             TestWriteArctic("net.securities",purge:true,del:true);
                         TestReadArctic("net.securities");
             TestReflection();
             
             TestGrowingCase();

             */
            //TestArcticDateTimeIndex("net.securities", purge: true, del: true);
            TestMetadata();
            Console.WriteLine ("DONE");
		}
	}
}
