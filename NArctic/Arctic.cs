using System;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using NumCIL;
using System.ComponentModel;
using Utilities;
using MongoDB.Bson.IO;

namespace NArctic
{
	public class Arctic
	{
		protected IMongoDatabase _db;
		protected IMongoCollection<BsonDocument> _versions;
		protected IMongoCollection<BsonDocument> _segments;

		public static string SEGMENTS  = "securities";
		public static string VERSIONS = SEGMENTS + "." + "versions";
		public Arctic(IMongoDatabase db)
		{
			this._db = db;
			this._versions = _db.GetCollection<BsonDocument>(VERSIONS);
			this._segments = _db.GetCollection<BsonDocument>(SEGMENTS);
		}

		public async Task<BsonDocument> ReadVersionAsync(string symbol)
		{
			Console.WriteLine ("read version {0}".Args (symbol));
			IAsyncCursorSource<BsonDocument> versions = this._versions.AsQueryable ()
				.Where (x => x ["symbol"] == symbol)
				.OrderByDescending (x => x ["version"])
				.Take(1);
			var rtn =  await versions.FirstAsync<BsonDocument>();
			Console.WriteLine ("DONE read version {0}".Args (symbol));
			return rtn;
		}

		public async Task<DataFrame> ReadDataFrameAsync(string symbol, BsonDocument version=null)
		{
			version = version ?? await ReadVersionAsync(symbol);
			var buf = new ByteBuffer ();
			var id = version ["_id"];
			var parent = version.GetValue ("base_bersion_id", id);
			Console.WriteLine ("version: {0}\nRead segments parent {1}".Args (version, parent));
			var bf = Builders<BsonDocument>.Filter;
			var filter = bf.Eq ("symbol", symbol) & bf.Eq ("parent", parent);
			var segments = await this._segments.FindAsync (filter);
			//Console.WriteLine ("Got cursor");
			while (await segments.MoveNextAsync ()) {
				foreach (var segment in segments.Current) {
					//Console.WriteLine ("Segment: {0}".Args(segment["segment"]));
					var chunk  = segment["data"].AsByteArray;
					buf.AppendDecodedLZ4(chunk);
				}
			}
			var nrows = version ["up_to"].AsInt32;
			var dtype = version ["dtype"].AsString;
			var df = ConstructDataFrame (buf.Data, dtype, nrows);
			return df;			
		}


		public static DataFrame ConstructDataFrame(byte[] buf, string dtype, int iheight)
		{
			var buftype = new DType (dtype);
			return DataFrame.FromBuffer(buf, buftype, iheight);
		}	
	}

}

