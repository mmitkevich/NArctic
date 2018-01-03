using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NArctic.Tests.Unit
{
    [TestFixture]
    public class DTypeAssertions
    {
        [Test]
        public void ParseLEFloat()
        {
            var cur = new DType();
            int i = new DTypeParser().Parse("'<f8'", 0, cur);
            Assert.IsNotNull(cur);
            Assert.AreEqual(typeof(double), cur.Type);
            Assert.AreEqual(EndianType.Little, cur.Endian);
            Assert.AreEqual(8, cur.Size);
        }
    }
}
