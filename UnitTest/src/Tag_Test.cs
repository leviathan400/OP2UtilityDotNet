using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OP2UtilityDotNet;

namespace UnitTest.src
{
	[TestClass]
	public class Tag_Test
	{
		[TestMethod]
		public void MakeTag()
		{
			Tag tag1 = new Tag("VOL ");
			Tag tag2 = new Tag("VOL ");
			Tag tag3 = new Tag("volh");

			// Equality and inequality comparable
			Assert.AreEqual(tag1, tag2);
			Assert.AreNotEqual(tag1, tag3);

			// Convertible to std::string
			string strTag1 = tag1.ToString();
			Assert.AreEqual("VOL ", strTag1);

			// Concatenation with string literals
			string appendString1 = "String literal: " + tag1;
			Assert.AreEqual("String literal: VOL ", appendString1);

			// Concatenation with std::string
			string appendString2 = "std::string: " + tag1;
			Assert.AreEqual("std::string: VOL ", appendString2);
		}

		[TestMethod]
		public void EqualTagsHaveEqualHashCodes()
		{
			// Equals/GetHashCode contract: equal objects MUST hash equal.
			Tag tag1 = new Tag("VOL ");
			Tag tag2 = new Tag("VOL ");
			Tag tag3 = new Tag("volh");

			Assert.AreEqual(tag1, tag2);
			Assert.AreEqual(tag1.GetHashCode(), tag2.GetHashCode());

			// Sanity check: different content should usually produce a
			// different hash (not strictly required, but our impl gives this).
			Assert.AreNotEqual(tag1.GetHashCode(), tag3.GetHashCode());
		}

		[TestMethod]
		public void TagWorksAsHashSetKey()
		{
			HashSet<Tag> set = new HashSet<Tag>();
			set.Add(new Tag("VOL "));

			Assert.IsTrue(set.Contains(new Tag("VOL ")));
			Assert.IsFalse(set.Contains(new Tag("volh")));
		}

		[TestMethod]
		public void TagWorksAsDictionaryKey()
		{
			Dictionary<Tag, int> map = new Dictionary<Tag, int>();
			map[new Tag("VOL ")] = 42;

			Assert.IsTrue(map.ContainsKey(new Tag("VOL ")));
			Assert.AreEqual(42, map[new Tag("VOL ")]);
		}
	}
}
