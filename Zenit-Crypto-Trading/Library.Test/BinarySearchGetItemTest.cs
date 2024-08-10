using System.Collections.Generic;
using Hallupa.Library.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Library.Test
{
    [TestClass]
    public class BinarySearchGetItemTest
    {
        [TestMethod]
        public void BinarySearchGetItem_PrevLowerValue()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            var index = list.BinarySearchGetItem(i => list[i], 0, 13, BinarySearchMethod.PrevLowerValue);
            Assert.AreEqual(12, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.PrevLowerValue);
            Assert.AreEqual(1, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 15, BinarySearchMethod.PrevLowerValue);
            Assert.AreEqual(14, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 1, BinarySearchMethod.PrevLowerValue);
            Assert.AreEqual(-1, index);

            list = new List<int> { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 4, 5 };
            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.PrevLowerValue);
            Assert.AreEqual(1, list[index]);
        }

        [TestMethod]
        public void BinarySearchGetItem_NextHigherValue()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            var index = list.BinarySearchGetItem(i => list[i], 0, 13, BinarySearchMethod.NextHigherValue);
            Assert.AreEqual(14, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 1, BinarySearchMethod.NextHigherValue);
            Assert.AreEqual(2, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 15, BinarySearchMethod.NextHigherValue);
            Assert.AreEqual(-1, index);

            list = new List<int> { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 4, 5 };
            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.NextHigherValue);
            Assert.AreEqual(3, list[index]);
        }

        [TestMethod]
        public void BinarySearchGetItem_PrevLowerValueOrValue()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            var index = list.BinarySearchGetItem(i => list[i], 0, 13, BinarySearchMethod.PrevLowerValueOrValue);
            Assert.AreEqual(13, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.PrevLowerValueOrValue);
            Assert.AreEqual(2, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 15, BinarySearchMethod.PrevLowerValueOrValue);
            Assert.AreEqual(15, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 1, BinarySearchMethod.PrevLowerValueOrValue);
            Assert.AreEqual(1, list[index]);

            list = new List<int> { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 4, 5 };
            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.PrevLowerValueOrValue);
            Assert.AreEqual(2, list[index]);

            var list2 = new List<double> { 1.5, 2.5, 3.5, 4.5, 5.5, 6.5 };
            index = list2.BinarySearchGetItem(i => list2[i], 0, 3.6, BinarySearchMethod.PrevLowerValueOrValue);
            Assert.AreEqual(3.5, list2[index]);
        }

        [TestMethod]
        public void BinarySearchGetItem_NextHigherValueOrValue()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            var index = list.BinarySearchGetItem(i => list[i], 0, 13, BinarySearchMethod.NextHigherValueOrValue);
            Assert.AreEqual(13, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.NextHigherValueOrValue);
            Assert.AreEqual(2, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 15, BinarySearchMethod.NextHigherValueOrValue);
            Assert.AreEqual(15, list[index]);

            index = list.BinarySearchGetItem(i => list[i], 0, 1, BinarySearchMethod.NextHigherValueOrValue);
            Assert.AreEqual(1, list[index]);

            list = new List<int> { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 4, 5 };
            index = list.BinarySearchGetItem(i => list[i], 0, 2, BinarySearchMethod.NextHigherValueOrValue);
            Assert.AreEqual(2, list[index]);

            var list2 = new List<double> { 1.5, 2.5, 3.5, 4.5, 5.5, 6.5 };
            index = list2.BinarySearchGetItem(i => list2[i], 0, 3.6, BinarySearchMethod.NextHigherValueOrValue);
            Assert.AreEqual(4.5, list2[index]);
        }
    }
}