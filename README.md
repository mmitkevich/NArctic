# NArctic

This is a port of [Arctic](https://github.com/manahl/arctic/) to .net/mono

## Status

Currently it is possible to read and append Arctic's ndarray store, but versioning is not supported.
So if you append some time series, old versions are not kept.

For usage examples see `NArctic.Tests/Program.cs`

## Performance

This port takes at least twice as long as the original python/numpy version

## Dependencies:

 * [NumCIL](https://github.com/bh107/bohrium/tree/master/bridge/NumCIL/NumCIL))
 * [Meta-Numerics](https://github.com/cureos/metanumerics)

## License

NArctic is licensed under the GNU LGPL v2.1.  A copy of which is included in [LICENSE](LICENSE)