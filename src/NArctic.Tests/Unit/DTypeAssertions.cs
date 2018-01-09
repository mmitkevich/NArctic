﻿using NUnit.Framework;
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
            TestFloat(cur, EndianType.Little);
        }

        [Test]
        public void ParseLEInt()
        {
            var cur = new DType();
            int i = new DTypeParser().Parse("'<i4'", 0, cur);
            TestInt(cur, EndianType.Little);
        }

        [Test]
        public void ParseLELong()
        {
            var cur = new DType();
            int i = new DTypeParser().Parse("'<i8'", 0, cur);
            TestLong(cur, EndianType.Little);
        }

        [Test]
        public void ParseLEDateTime()
        {
            var cur = new DType();
            int i = new DTypeParser().Parse("'<M8[ns]'", 0, cur);
            TestDateTime(cur, EndianType.Little);
        }

        [Test]
        public void ParseLEString()
        {
            var cur = new DType();
            int i = new DTypeParser().Parse("'S32'", 0, cur);
            TestString(cur, EndianType.Native, Encoding.UTF8, 32);
        }

        [Test]
        public void ParseLEUnicode()
        {
            var cur = new DType();
            int i = new DTypeParser().Parse("'<U32'", 0, cur);
            TestString(cur, EndianType.Little, Encoding.Unicode, 32 * 4);
        }

        private void TestFloat(DType cur, EndianType endian)
        {
            Assert.IsNotNull(cur);
            Assert.AreEqual(typeof(double), cur.Type);
            Assert.AreEqual(endian, cur.Endian);
            Assert.AreEqual(8, cur.Size);
        }

        private void TestInt(DType cur, EndianType endian)
        {
            Assert.IsNotNull(cur);
            Assert.AreEqual(typeof(int), cur.Type);
            Assert.AreEqual(endian, cur.Endian);
            Assert.AreEqual(4, cur.Size);
        }

        private void TestLong(DType cur, EndianType endian)
        {
            Assert.IsNotNull(cur);
            Assert.AreEqual(typeof(Int64), cur.Type);
            Assert.AreEqual(endian, cur.Endian);
            Assert.AreEqual(8, cur.Size);
        }

        private void TestDateTime(DType cur, EndianType endian)
        {
            Assert.IsNotNull(cur);
            Assert.AreEqual(typeof(DateTime), cur.Type);
            Assert.AreEqual(endian, cur.Endian);
            Assert.AreEqual(8, cur.Size);
        }

        private void TestString(DType cur, EndianType endian, Encoding encoding, int size)
        {
            Assert.IsNotNull(cur);
            Assert.AreEqual(typeof(string), cur.Type);
            Assert.AreEqual(endian, cur.Endian);
            Assert.AreEqual(encoding, cur.EncodingStyle);
            Assert.AreEqual(size, cur.Size);
        }
    }
}
