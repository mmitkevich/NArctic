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
			IAsyncCursorSource<BsonDocument> versions = this._versions.AsQueryable ()
				.Where (x => x ["symbol"] == "S0")
				.OrderByDescending (x => x ["version"])
				.Take(1);
			return await versions.FirstAsync<BsonDocument>();
		}

		public async Task<DataFrame> ReadDataFrameAsync(string symbol, BsonDocument version=null)
		{
			version = version ?? await ReadVersionAsync(symbol);
			var buf = new ByteBuffer ();
			//Console.WriteLine ("version: {0}".Args (version));
			var segments = await this._segments.AsQueryable ()
				.Where (x => 
					x ["symbol"] == symbol
					&& x ["parent"] == version.GetValue ("base_bersion_id", version ["_id"])
				).ToCursorAsync ();
			while (await segments.MoveNextAsync ()) {
				foreach (var segment in segments.Current) {
					Console.WriteLine ("Segment: {0}".Args(segment["segment"]));
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

