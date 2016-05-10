# NArctic
## Warning, Work in progress.

This is very preliminar experiments to port [arctic database](https://github.com/manahl/arctic/) to .net/mono

## Status

Currently it is possible to read and append Arctic's ndarray store, but versioning is not supported.
So if you append some time series, old versions are not kept.

For usage examples see `NArctic.Tests/Program.cs`

## Performance

Original python/numpy arctic had performance about 4 million reads per second on my i7 ssd laptop.
Ported .net version performs 2 times slower, about 2 million reads per second.

## Dependencies:

[NumCIL](https://github.com/bh107/bohrium/tree/master/bridge/NumCIL/NumCIL))
[Meta-Numerics](https://github.com/cureos/metanumerics)

## License: LGPL

Note: Planned license is BSD, but bohrium code currently included in the source tree, so it should be LGPL



