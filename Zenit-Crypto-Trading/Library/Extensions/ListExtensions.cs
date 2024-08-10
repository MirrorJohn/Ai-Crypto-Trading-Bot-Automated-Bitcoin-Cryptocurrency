using System;
using System.Collections;
using System.Collections.Generic;

namespace Hallupa.Library.Extensions
{
    public enum BinarySearchMethod
    {
        NextHigherValue,
        NextHigherValueOrValue,
        PrevLowerValue,
        PrevLowerValueOrValue,
        Value
    }

    public static class ListExtensions
    {
        private static readonly Random Rnd = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            for (var i = list.Count; i > 0; i--)
                Swap(list, 0, Rnd.Next(0, i));
        }

        private static void Swap<T>(IList<T> list, int i, int j)
        {
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }

        /// <summary>
        /// Gets the item with the value after the value. List needs to be in the correct order by the value returned by getValue.
        /// The method will return the next index in the list above the value (Assuming the list is ordered from low to high).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="getValue"></param>
        /// <param name="startIndex"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int BinarySearchGetItem<T>(this IList list, Func<int, T> getValue, int startIndex, T value, BinarySearchMethod method) where T : IComparable
        {
            var lowIndex = startIndex;
            var highIndex = list.Count - 1;

            while (highIndex - lowIndex > 5)
            {
                var midIndex = (highIndex - lowIndex) / 2 + lowIndex;
                if (method == BinarySearchMethod.NextHigherValue)
                {
                    if (getValue(midIndex).CompareTo(value) <= 0)
                    {
                        lowIndex = midIndex + 1;
                    }
                    else
                    {
                        highIndex = midIndex;
                    }
                }

                if (method == BinarySearchMethod.PrevLowerValue)
                {
                    if (getValue(midIndex).CompareTo(value) >= 0)
                    {
                        highIndex = midIndex - 1;
                    }
                    else
                    {
                        lowIndex = midIndex;
                    }
                }

                if (method == BinarySearchMethod.PrevLowerValueOrValue || method == BinarySearchMethod.Value)
                {
                    if (getValue(midIndex).CompareTo(value) > 0)
                    {
                        highIndex = midIndex - 1;
                    }
                    else
                    {
                        lowIndex = midIndex;
                    }
                }

                if (method == BinarySearchMethod.NextHigherValueOrValue)
                {
                    if (getValue(midIndex).CompareTo(value) < 0)
                    {
                        lowIndex = midIndex + 1;
                    }
                    else
                    {
                        highIndex = midIndex;
                    }
                }
            }

            if (method == BinarySearchMethod.NextHigherValue)
            {
                for (var i = lowIndex; i <= highIndex; i++)
                {
                    if (getValue(i).CompareTo(value) > 0)
                    {
                        return i;
                    }
                }
            }
            else if (method == BinarySearchMethod.PrevLowerValue)
            {
                for (var i = highIndex; i >= lowIndex; i--)
                {
                    if (getValue(i).CompareTo(value) < 0)
                    {
                        return i;
                    }
                }
            }
            else if (method == BinarySearchMethod.PrevLowerValueOrValue)
            {
                for (var i = highIndex; i >= lowIndex; i--)
                {
                    if (getValue(i).CompareTo(value) <= 0)
                    {
                        return i;
                    }
                }
            }
            else if (method == BinarySearchMethod.NextHigherValueOrValue)
            {
                for (var i = lowIndex; i <= highIndex; i++)
                {
                    if (getValue(i).CompareTo(value) >= 0)
                    {
                        return i;
                    }
                }
            }
            else if (method == BinarySearchMethod.Value)
            {
                for (var i = highIndex; i >= lowIndex; i--)
                {
                    if (getValue(i).CompareTo(value) == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}