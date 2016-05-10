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
			Console.WriteLine ("reading {0} version".Args (symbol));
			IAsyncCursorSource<BsonDocument> versions = this._versions.AsQueryable ()
				.Where (x => x ["symbol"] == symbol)
				.OrderByDescending (x => x ["version"])
				.Take(1);
			var rtn =  await versions.FirstOrDefaultAsync<BsonDocument>();
			Console.WriteLine ("read {0} version: {1}".Args (symbol, rtn));
			return rtn;
		}

		public async Task<DataFrame> ReadDataFrameAsync(string symbol, BsonDocument version=null)
		{
			version = version ?? await ReadVersionAsync(symbol);
			if (version == null)
				return null;
			
			var buf = new ByteBuffer ();
			var id = version ["_id"];
			var parent = version.GetValue ("base_version_id", null);
			if (parent == null)
				parent = id;
			Console.WriteLine ("version: {0}\nRead segments parent {1}".Args (version, parent));
			var bf = Builders<BsonDocument>.Filter;
			var filter = bf.Eq ("symbol", symbol) & bf.Eq ("parent", parent);
			var segments = await this._segments.FindAsync (filter);
			int segcount = 0;
			while (await segments.MoveNextAsync ()) {
				foreach (var segment in segments.Current) {
#if DEBUG
					Console.WriteLine ("read segment: {0}".Args(segment));
#endif
					var chunk  = segment["data"].AsByteArray;
					if (segment ["compressed"].AsBoolean)
						buf.AppendDecompress (chunk);
					else
						buf.Append (chunk);
					segcount++;
				}
			}
			if (segcount == 0)
				throw new InvalidOperationException ("No segments found for {0}".Args (parent));
			
			var nrows = version ["up_to"].AsInt32;
			var dtype = version ["dtype"].AsString;
			var buftype = new DType (dtype);
			var bytes = buf.GetBytes ();
			Console.WriteLine ("converting to dataframe up_to={0} dtype={1} len={2}".Args (nrows, dtype, bytes.Length));
			var df = DataFrame.FromBuffer(buf.GetBytes(), buftype, nrows);
			return df;			
		}

		internal static FilterDefinitionBuilder<BsonDocument> BF {get{ return Builders<BsonDocument>.Filter;} }
		internal static UpdateDefinitionBuilder<BsonDocument> BU {get{ return Builders<BsonDocument>.Update;} }
		internal static UpdateOptions Upsert = new UpdateOptions{ IsUpsert = true };

		public async Task<BsonDocument> AppendDataFrameAsync(string symbol, DataFrame df)
		{
			var version = await GetNewVersion (symbol, await ReadVersionAsync(symbol));

			var previous_version = await (_versions.AsQueryable ()
				.Where (v => v ["symbol"] == symbol && v ["version"] < version ["version"])
				.OrderByDescending (v => v ["version"]) as IAsyncCursorSource<BsonDocument>)
				.FirstOrDefaultAsync ();
			var dtype = version.GetValue ("dtype", null);
			if (dtype!=null && df.DType.ToString()!=dtype) {
				// dtype changed. need reload old data and repack it.
				throw new NotImplementedException("old dtype {0}, new dtype {1}: not implemented".Args(dtype,df.DType));
			}

			version ["dtype"] = df.DType.ToString();
			version ["shape"] = new BsonArray{ {-1} };
			version ["dtype_metadata"] = new BsonDocument { 
				{ "index", new BsonArray { { "index" } } } ,
				{ "columns", new BsonArray(df.Columns.Select(c=>c.Name).ToList()) }
			};
			version ["type"] = "pandasdf";
			version ["segment_count"] =  previous_version != null ? previous_version ["segment_count"].AsInt32 + 1 : 1;
			version ["append_count"] = previous_version != null ? previous_version ["append_count"].AsInt32 + 1 : 0;

			var  segment_index = new BsonArray();
			int segment_offset = 0;

			//version ["base_sha"] = version ["sha"];
			if (previous_version != null) {
				segment_index = previous_version ["segment_index"].AsBsonArray;
				segment_offset = previous_version ["up_to"].AsInt32;
			}

			segment_index.Add (segment_offset);
			version ["segment_index"] = segment_index;
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
				//{"parent", new BsonArray{ version["_id"] }},
			};
#if DEBUG
			Console.WriteLine ("new segment: {0}".Args (segment));
			Console.WriteLine ("new version: {0}".Args (version));
#endif
			await _segments.InsertOneAsync(segment);
			//await _versions.InsertOneAsync(version);	
			await _versions.ReplaceOneAsync(BF.Eq("symbol", symbol), version, Upsert);

			// update parents in versions
			var res = await _segments.UpdateManyAsync (BF.Eq("symbol", symbol), BU.Set ("parent", new BsonArray{ version ["_id"] }));
			Console.WriteLine ("updated segments parents {0}".Args(res.MatchedCount));
			return version;
		}

		public async Task<BsonDocument> GetNewVersion(string symbol, BsonDocument version=null)
		{
			version = version ?? new BsonDocument ();

			var version_num = await _version_numbers.FindOneAndUpdateAsync (
				                       	BF.Eq ("symbol", symbol),
				                       	BU.Inc ("version", 1),
									   	new FindOneAndUpdateOptions<BsonDocument> { 
											IsUpsert = true, 
											ReturnDocument = ReturnDocument.After 
										}
			                       );
			version ["version"] = version_num!=null ? version_num["version"] : 1;
			if(version.GetValue("_id",null)==null)
				version ["_id"] = new BsonObjectId (ObjectId.GenerateNewId ());
			version ["symbol"] = symbol;
			return version;
		}
	}
}

	