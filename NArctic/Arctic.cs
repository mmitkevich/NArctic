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
using MongoDB.Bson.Serialization.IdGenerators;

namespace NArctic
{
	public class Arctic
	{
		protected IMongoDatabase _db;
		protected IMongoCollection<BsonDocument> _versions;
		protected IMongoCollection<BsonDocument> _segments;
		protected IMongoCollection<BsonDocument> _version_numbers;

		public static string SEGMENTS  = "securities";
		public static string VERSIONS = SEGMENTS + "." + "versions";
		public static string VERSION_NUMBERS = SEGMENTS + "." + "version_nums";

		public Arctic(IMongoDatabase db)
		{
			this._db = db;
			this._versions = _db.GetCollection<BsonDocument>(VERSIONS);
			this._segments = _db.GetCollection<BsonDocument>(SEGMENTS);
			this._version_numbers = _db.GetCollection<BsonDocument> (VERSION_NUMBERS);
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
			Console.WriteLine ("Got cursor");
			while (await segments.MoveNextAsync ()) {
				foreach (var segment in segments.Current) {
					Console.WriteLine ("Segment: {0}".Args(segment["segment"]));
					var chunk  = segment["data"].AsByteArray;
					buf.AppendDecompress(chunk);
				}
			}
			var nrows = version ["up_to"].AsInt32;
			var dtype = version ["dtype"].AsString;
			var buftype = new DType (dtype);
			var df = DataFrame.FromBuffer(buf.GetBytes(), buftype, nrows);
			return df;			
		}

		public async Task<BsonDocument> AppendDataFrameAsync(string symbol, DataFrame df)
		{
			var version = await GetNewVersion (symbol);
			int segment_offset = 0;

			var bf = Builders<BsonDocument>.Filter;

			var previous_version = await (_versions.AsQueryable ()
				.Where (v => v ["symbol"] == symbol && v ["version"] < version ["version"])
				.OrderByDescending (v => v ["version"]) as IAsyncCursorSource<BsonDocument>)
				.FirstOrDefaultAsync ();

			version ["dtype"] = df.DType.ToString();
			version ["shape"] = new BsonArray{ {-1} };
			version ["dtype_metadata"] = new BsonDocument { 
				{ "index", new BsonArray { { "index" } } } ,
				{ "columns", new BsonArray(df.Columns.Select(c=>c.Name).ToList()) }
			};
			version ["type"] = "pandasdf";
			version ["segment_count"] = previous_version != null ? previous_version ["segment_count"].AsInt32 + 1 : 1;

			var  segment_index = new BsonArray();

			//version ["base_sha"] = version ["sha"];
			if (previous_version != null) {
				segment_offset = previous_version ["segment"].AsInt32 + 1;
				segment_index = previous_version ["segment_index"].AsBsonArray;
			}

			version ["up_to"] = segment_offset + df.Rows.Count;

			var buf = new ByteBuffer();
			var bin = df.ToBuffer ();
			buf.AppendCompress(bin);

#if false
			var buf2 = new ByteBuffer ();
			buf2.AppendDecompress (buf.GetBytes());
			var bin2 = buf2.GetBytes ();
			if (!bin.SequenceEqual (bin2))
				throw new InvalidOperationException ();
			var df2 = DataFrame.FromBuffer(bin2, df.DType, df.Rows.Count);
#endif

			var segment = new BsonDocument { 
				{"symbol", symbol },
				{"data", new BsonBinaryData(buf.GetBytes())},
				{"compressed", true},
				{"segment", segment_offset + df.Rows.Count - 1},
				{"parent", new BsonArray{ version["_id"] }},
			};
			Console.WriteLine ("new segment\n{0}".Args (segment));
			await _segments.InsertOneAsync(segment);
			await _versions.InsertOneAsync(version);
			return version;
		}

		public async Task<BsonDocument> GetNewVersion(string symbol)
		{
			var version = new BsonDocument ();

			var bf = Builders<BsonDocument>.Filter;
			var bu = Builders<BsonDocument>.Update;

			var version_num = await _version_numbers.FindOneAndUpdateAsync (
				                       bf.Eq ("symbol", symbol),
				                       bu.Inc ("version", 1),
				                       new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true }
			                       );

			version ["version"] = version_num!=null ? version_num["version"] : 1;
			version ["_id"] = new BsonObjectId (ObjectId.GenerateNewId ());
			version ["symbol"] = symbol;

			return version;
		}
	}
}

	