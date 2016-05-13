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
using Serilog;
using System.Security.Cryptography;

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
			Log.Debug("reading {symbol} version", symbol);
			IAsyncCursorSource<BsonDocument> versions = this._versions.AsQueryable ()
				.Where (x => x ["symbol"] == symbol)
				.OrderByDescending (x => x ["version"])
				.Take(1);
			var rtn =  await versions.FirstOrDefaultAsync<BsonDocument>();
			Log.Debug("read {0} version: {1}".Args (symbol, rtn));
			return rtn;
		}

        public DataFrame Read(string symbol, DateRange daterange=null, BsonDocument version=null)
        {
            return this.ReadAsync(symbol, daterange, version).Result;
        }

        public FilterDefinition<BsonDocument> GetSegmentsFilter(string symbol, DateRange range, BsonDocument version)
        {
            var id = version["_id"];
            var parent = version.GetValue("base_version_id", null);
            if (parent == null)
                parent = id;

            var filter = BF.Eq("symbol", symbol) & BF.Eq("parent", version);
            if (range!=null)
            {
                var seg_ind_buf = new ByteBuffer();
                seg_ind_buf.AppendDecompress(version["segment_index"].AsByteArray);
            }
            return filter;
        }

        public async Task<DataFrame> ReadAsync(string symbol, DateRange daterange=null, BsonDocument version=null)
		{
			version = version ?? await ReadVersionAsync(symbol);
			if (version == null)
				return null;
			
			var buf = new ByteBuffer ();
			Log.Debug ("version: {0}".Args (version));
            var filter = GetSegmentsFilter(symbol, daterange, version);
			var segments = await this._segments.FindAsync (filter);
			int segcount = 0;
			while (await segments.MoveNextAsync ()) {
				foreach (var segment in segments.Current) {
#if DEBUG
					//Log.Debug ("read segment: {0}".Args(segment));
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
				throw new InvalidOperationException ("No segments found for {0}".Args (version));
			
			var nrows = version ["up_to"].AsInt32;
			var dtype = version ["dtype"].AsString;
			var buftype = new DType (dtype);
			var bytes = buf.GetBytes ();
			Log.Debug ("converting to dataframe up_to={0} dtype={1} len={2}".Args (nrows, dtype, bytes.Length));
			var df = DataFrame.FromBuffer(buf.GetBytes(), buftype, nrows);
			return df;			
		}

		internal static FilterDefinitionBuilder<BsonDocument> BF {get{ return Builders<BsonDocument>.Filter;} }
		internal static UpdateDefinitionBuilder<BsonDocument> BU {get{ return Builders<BsonDocument>.Update;} }
		internal static UpdateOptions Upsert = new UpdateOptions{ IsUpsert = true };

		public async Task<long> DeleteAsync(string symbol)
		{
			var rtn = await _segments.DeleteManyAsync (BF.Eq ("symbol", symbol));
			Log.Information ("Deleted {count} segments for {symbol}", rtn.DeletedCount, symbol);
			rtn = await _versions.DeleteManyAsync (BF.Eq ("symbol", symbol));
			Log.Information ("Deleted {count} versions for {symbol}", rtn.DeletedCount, symbol);
			return rtn.DeletedCount;
		}

        public BsonDocument Append(string symbol, DataFrame df, int chunksize = 0)
        {
            return this.AppendAsync(symbol, df, chunksize).Result;
        }

		public async Task<BsonDocument> AppendAsync(string symbol, DataFrame df, int chunksize=0)
		{
            if(chunksize>0 && df.Rows.Count>chunksize)
            {
                var rng = Range.R(0, chunksize);
                BsonDocument ver = null;
				int chunkscount = 0;
                while (rng.First<df.Rows.Count) {
                    var chunk = df[rng];
                    ver = await AppendAsync(symbol, chunk);
                    rng = Range.R(rng.First + chunksize, rng.Last + chunksize);
					chunkscount++;
                }
                return ver;
            }

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

            var seg_ind_buf = new ByteBuffer();
			int segment_offset = 0;

			//version ["base_sha"] = version ["sha"];
			if (previous_version != null) {
				var seg_ind = previous_version ["segment_index"].AsByteArray;
                seg_ind_buf.AppendDecompress(seg_ind);
				segment_offset = previous_version ["up_to"].AsInt32;
			}

            //seg_ind_buf.Append();
            var seg_ind_buf2 = new ByteBuffer();
            seg_ind_buf2.AppendCompress(seg_ind_buf.GetBytes());
			version ["segment_index"] = seg_ind_buf2.GetBytes();
			version ["up_to"] = segment_offset + df.Rows.Count;
			var buf = new ByteBuffer();
			var bin = df.ToBuffer ();
			buf.AppendCompress(bin);

			var sha1 = SHA1.Create();

			var sha = version.GetValue ("sha", null);
			if (sha == null) {
				byte[] hashBytes = sha1.ComputeHash(bin);
				version ["sha"] = new BsonBinaryData (hashBytes);
			}

#if false
			var buf2 = new ByteBuffer ();
			buf2.AppendDecompress (buf.GetBytes());
			var bin2 = buf2.GetBytes ();
			if (!bin.SequenceEqual (bin2))
				throw new InvalidOperationException ();
			var df2 = DataFrame.FromBuffer(bin2, df.DType, df.Rows.Count);
#endif
			var segment = new BsonDocument { 
				{"symbol", symbol},
				{"data", new BsonBinaryData(buf.GetBytes())},
				{"compressed", true},
				{"segment", segment_offset + df.Rows.Count - 1},
				{"parent", new BsonArray{ version["_id"] }},
			};

			var hash = new ByteBuffer();
			hash.Append(Encoding.ASCII.GetBytes(symbol));
			foreach(var key in segment.Names.OrderByDescending(x=>x)) {
				var value = segment.GetValue (key);
				if (value is BsonBinaryData)
					hash.Append (value.AsByteArray);
				else {
					var str = value.ToString ();
					hash.Append (Encoding.ASCII.GetBytes (str));
				}
			}
			segment ["sha"] = sha1.ComputeHash (hash.GetBytes ());

			await _segments.InsertOneAsync(segment);
			//await _versions.InsertOneAsync(version);	
			await _versions.ReplaceOneAsync(BF.Eq("symbol", symbol), version, Upsert);

			Log.Information ("inserted new segment {segment} for symbol {symbol}", segment["_id"], symbol);
			Log.Information ("replaced version {0} for symbol {symbol} sha1 {sha}", version["_id"], symbol, sha);

			// update parents in versions
			//var res = await _segments.UpdateManyAsync (BF.Eq("symbol", symbol), BU.Set ("parent", new BsonArray{ version ["_id"] }));
			//Log.Debug ("updated segments parents {0}".Args(res.MatchedCount));
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

	