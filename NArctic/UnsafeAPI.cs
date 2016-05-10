using System;

namespace NArctic
{
	public static class UnsafeAPI
	{
		public static unsafe void ColumnCopy(IntPtr ptarget, int target_col, int target_width, IntPtr psource, int source_col, int source_width, int count, int elsize)
		{
			unsafe
			{
				byte* src = (byte*)psource.ToPointer();
				byte* dst = (byte*)ptarget.ToPointer();
				src += source_col;
				dst += target_col;

				for (int i = 0; i < count; i++) {
					for (int j = 0; j < elsize; j++)
						dst [j] = src [j];
					dst += target_width;
					src += source_width;
				}
			}
		}
	}
}

