namespace BBCode.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using NUnit.Framework;

    public class BBCodeInterpreterTests
    {
        [Test]
        public void Interpret_WhenChildContextIsLeftOpen_ClosedChildContext()
        {
            var doc = new XmlDocument();
            doc.LoadXml(@"<bbcode>
  <tags>
    <tag name=""A"" type=""newcontext"" parameter=""false"">
      <open><![CDATA[A]]></open>
      <close><![CDATA[a]]></close>
    </tag>
    <tag name=""B"" type=""newcontext"" parameter=""false"">
      <open><![CDATA[B]]></open>
      <close><![CDATA[b]]></close>
    </tag>
  </tags>
</bbcode>");
            var bbc = new BBCodeInterpreter(doc);

            var output = bbc.Interpret("[A][B]c[/A]");

            Assert.That(output, Is.EqualTo("ABcba"));
        }

        [Test]
        public void Interpret_WhenChildTagIsLeftOpen_ClosedChildTag()
        {
            var doc = new XmlDocument();
            doc.LoadXml(@"<bbcode>
  <tags>
    <tag name=""A"" type=""newcontext"" parameter=""false"">
      <open><![CDATA[A]]></open>
      <close><![CDATA[a]]></close>
    </tag>
    <tag name=""B"" type=""normal"" parameter=""false"">
      <open><![CDATA[B]]></open>
      <close><![CDATA[b]]></close>
    </tag>
  </tags>
</bbcode>");
            var bbc = new BBCodeInterpreter(doc);

            var output = bbc.Interpret("[A][B]c[/A]");

            Assert.That(output, Is.EqualTo("ABcba"));
        }

        [Test]
        public void Interpret_WhenClosingTagsAreOutOfOrder_ClosedAllTagsReopeningAsNecessary()
        {
            var doc = new XmlDocument();
            doc.LoadXml(@"<bbcode>
  <tags>
    <tag name=""A"" type=""normal"" parameter=""false"">
      <open><![CDATA[A]]></open>
      <close><![CDATA[a]]></close>
    </tag>
    <tag name=""B"" type=""normal"" parameter=""false"">
      <open><![CDATA[B]]></open>
      <close><![CDATA[b]]></close>
    </tag>
  </tags>
</bbcode>");
            var bbc = new BBCodeInterpreter(doc);

            var output = bbc.Interpret("[A][B]c[/A][/B]");

            Assert.That(output, Is.EqualTo("ABcbaBb"));
        }

    }
}
