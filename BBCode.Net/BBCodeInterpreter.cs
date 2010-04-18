namespace BBCode
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;

    public sealed class BBCodeInterpreter
    {
        private readonly Regex validUrlSchemes = new Regex("^(http|https|ftp)://");
        private readonly Regex tagParser = new Regex(@"\A\[(?<endflag>/?)(?<tagname>\w+)(?:(?<paramseperator>=)(?<parameter>[^\r\n\]]*))?]\z", RegexOptions.Compiled);

        private readonly List<BBCodeTag> tags = new List<BBCodeTag>();
        private readonly Dictionary<string, string> replacements = new Dictionary<string, string>();
        private readonly Dictionary<string, string> literalReplacements = new Dictionary<string, string>();

        public BBCodeInterpreter(XmlDocument configuration)
        {
            try
            {
                XmlNodeList nodes = configuration.SelectNodes(@"/bbcode/tags/tag");
                foreach (XmlNode node in nodes)
                {
                    string tagName = node.Attributes["name"].Value.ToUpperInvariant();
                    BBCodeTagTypes tagType = BBCodeTagTypes.Normal;

                    if (node.Attributes["type"] != null)
                    {
                        string tagTypeName = node.Attributes["type"].Value;
                        if (tagTypeName.ToUpperInvariant() == "NORMAL")
                        {
                            tagType = BBCodeTagTypes.Normal;
                        }
                        else if (tagTypeName.ToUpperInvariant() == "LITERAL")
                        {
                            tagType = BBCodeTagTypes.Literal;
                        }
                        else if (tagTypeName.ToUpperInvariant() == "NEWCONTEXT")
                        {
                            tagType = BBCodeTagTypes.NewContext;
                        }
                        else
                        {
                            throw new InvalidOperationException("The document provided did not conform to the BBCode document specifications.");
                        }
                    }

                    string openTag = string.Empty;
                    string closeTag = string.Empty;

                    bool parameter = bool.Parse(node.Attributes["parameter"].Value);

                    if (node.SelectSingleNode("./open") != null)
                    {
                        openTag = node.SelectSingleNode("./open").InnerText;
                    }

                    if (node.SelectSingleNode("./close") != null)
                    {
                        closeTag = node.SelectSingleNode("./close").InnerText;
                    }

                    this.tags.Add(new BBCodeTag(tagName, parameter, openTag, closeTag, tagType));
                }

                nodes = configuration.SelectNodes(@"/bbcode/replacements/replacement");
                foreach (XmlNode node in nodes)
                {
                    string oldvalue = node.SelectSingleNode("oldvalue").InnerText;
                    string newvalue = node.SelectSingleNode("newvalue").InnerText;

                    this.replacements[oldvalue] = newvalue;
                }

                nodes = configuration.SelectNodes(@"/bbcode/literalReplacements/replacement");
                foreach (XmlNode node in nodes)
                {
                    string oldvalue = node.SelectSingleNode("oldvalue").InnerText;
                    string newvalue = node.SelectSingleNode("newvalue").InnerText;

                    this.literalReplacements[oldvalue] = newvalue;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The document provided did not conform to the BBCode document specifications.", ex);
            }
        }

        public string Interpret(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            Regex tokenizer = new Regex(@"([^[]+|\[/?\w+(=[^\r\n\]]*)?]|\[)", RegexOptions.Compiled);

            // Tokenize the input.
            MatchCollection matches = tokenizer.Matches(input);
            Queue<string> tokens = new Queue<string>(matches.Count);
            foreach (Match match in matches)
            {
                if (!String.IsNullOrEmpty(match.Value))
                {
                    tokens.Enqueue(match.Value);
                }
            }

            // Process the resulting tokens.
            string formatted = this.ProcessTokens(tokens);

            return formatted;
        }

        private string FormatNormal(string literalText)
        {
            string formatted = Microsoft.Security.Application.AntiXss.HtmlEncode(literalText);

            foreach (string replace in this.replacements.Keys)
            {
                formatted = Regex.Replace(formatted, replace, this.replacements[replace], RegexOptions.Multiline);
            }

            return formatted;
        }

        private string FormatLiteral(string literalText)
        {
            string formatted = Microsoft.Security.Application.AntiXss.HtmlEncode(literalText);

            foreach (string replace in this.literalReplacements.Keys)
            {
                formatted = Regex.Replace(formatted, replace, this.literalReplacements[replace]);
            }

            return formatted;
        }

        private string SanitizeUrl(string url)
        {
            var ret = url.TrimStart();

            if (!this.validUrlSchemes.IsMatch(ret))
            {
                ret = ret.Replace(":", string.Empty);
            }

            if (!ret.Contains(":"))
            {
                ret = "http://" + ret;
            }

            return ret;
        }

        private string ProcessTokens(Queue<string> tokens)
        {
            var formatted = string.Empty;
            var literalContents = string.Empty;

            var stack = new Stack<Stack<BBCodeTagContext>>();
            stack.Push(new Stack<BBCodeTagContext>());

            while (tokens.Count > 0)
            {
                Stack<BBCodeTagContext> context = stack.Peek();

                // Grab a token.
                string token = tokens.Dequeue();

                // Match the token against the tag validation rules.
                Match match = this.tagParser.Match(token);

                // If our match failed, the token is a literal string.
                if (!match.Success)
                {
                    if (context.Count > 0 && context.Peek().Tag.IsLiteral)
                    {
                        string append = this.FormatLiteral(token);
                        formatted += append;
                        literalContents += append;
                    }
                    else
                    {
                        formatted += this.FormatNormal(token);
                    }
                }
                else if (context.Count > 0 && context.Peek().Tag.IsLiteral && !(match.Groups["endflag"].Value == "/" && match.Groups["tagname"].Value.ToUpperInvariant() == context.Peek().Tag.TagName.ToUpperInvariant()))
                {
                    // If we are in a literal context and the tag does not match the ending tag of the context, use the token as though it were a literal string.
                    string append = this.FormatLiteral(token);
                    formatted += append;
                    literalContents += append;
                }
                else
                {
                    string tagName = match.Groups["tagname"].Value.ToUpperInvariant();
                    bool paramFlag = match.Groups["paramseperator"].Value.Equals("=");
                    string parameter = match.Groups["parameter"].Value;
                    bool endFlag = match.Groups["endflag"].Value.Equals("/");

                    if (!endFlag)
                    {
                        var tag = (from t in this.tags
                                   where t.TagName == tagName
                                   where t.Parameterized == paramFlag
                                   select t).FirstOrDefault();
                        
                        if (tag == null)
                        {
                            // If we dont have a definition of the tag, treat it as a literal string.
                            formatted += this.FormatNormal(token);
                            continue;
                        }

                        // If we recieved an open tag, add it to the context, and output its open tag.
                        context.Push(new BBCodeTagContext(tag, parameter));
                        formatted += tag.OpenTag(this.FormatLiteral(parameter));

                        if (tag.IsNewContext)
                        {
                            // If the tag creates a new context, push a new context onto the stack.
                            stack.Push(new Stack<BBCodeTagContext>());
                        }
                    }
                    else
                    {
                        var potentialTags = (from t in this.tags
                                             where t.TagName == tagName
                                             select t).ToList();

                        if (potentialTags.Count == 0)
                        {
                            // If we dont have a definition of the tag, treat it as a literal string.
                            formatted += this.FormatNormal(token);
                            continue;
                        }

                        // Parent contexts that match the closing tag.
                        var parentContexts = from s in stack
                                             let p = (s.Count > 0 ? s.Peek() : null)
                                             let t = potentialTags.Where(t => p != null && t == p.Tag).FirstOrDefault()
                                             where t != null
                                             select new { Stack = s, Tag = t };

                        var pc = parentContexts.FirstOrDefault();

                        // If the closing tag ends a context, and this is not the root context, and
                        // the tag matches the opening tag of one of the parent contexts,
                        if (pc != null && pc.Tag.IsNewContext && stack.Count >= 2)
                        {
                            var tag = pc.Tag;

                            while (true)
                            {
                                // close all of the tags in the current context,
                                while (context.Count > 0)
                                {
                                    BBCodeTagContext unclosedContext = context.Pop();
                                    formatted += unclosedContext.Tag.CloseTag(unclosedContext.Parameter, literalContents);
                                    literalContents = string.Empty;
                                }

                                // reomve the internal context,
                                stack.Pop();
                                context = stack.Peek();

                                // remove the calling context tag,
                                var closingTag = context.Pop();

                                // and output the closing tag.
                                formatted += closingTag.Tag.CloseTag(closingTag.Parameter, literalContents);
                                literalContents = string.Empty;

                                // If the context we closed was the one we were looking for, stop.
                                if (closingTag.Tag == tag)
                                {
                                    break;
                                }
                                else
                                {
                                    Debug.WriteLine(string.Format("Closed a sub context: {0} != {1}", closingTag.Tag.TagName, tag.TagName));
                                }
                            }
                        }
                        else
                        {
                            var contained = (from c in context
                                             let t = potentialTags.Where(t => t == c.Tag).FirstOrDefault()
                                             where t != null
                                             select new { Context = c, Tag = t }).FirstOrDefault();

                            if (contained == null)
                            {
                                // If the closing tag is not contained by our current context, treat the
                                // tag as a literal string.
                                formatted += this.FormatNormal(token);
                            }
                            else
                            {
                                var tag = contained.Tag;

                                // If our context contains the tag, but the closing tag is out-of-order,
                                // close each of the tags that are still open,
                                Stack<BBCodeTagContext> removed = new Stack<BBCodeTagContext>();
                                while (context.Peek().Tag != tag)
                                {
                                    BBCodeTagContext rem = context.Pop();
                                    formatted += rem.Tag.CloseTag(rem.Parameter, literalContents);
                                    literalContents = string.Empty;
                                    removed.Push(rem);
                                }

                                // Remove the now-closed tag,
                                var popped = context.Pop();
                                formatted += tag.CloseTag(popped.Parameter, literalContents);
                                literalContents = string.Empty;

                                // and reopen the still-open tags.
                                while (removed.Count > 0)
                                {
                                    BBCodeTagContext rem = removed.Pop();
                                    context.Push(rem);
                                    formatted += rem.Tag.OpenTag(this.FormatLiteral(rem.Parameter));
                                }
                            }
                        }
                    }
                }
            }

            while (stack.Count > 0)
            {
                Stack<BBCodeTagContext> context = stack.Pop();

                while (context.Count > 0)
                {
                    BBCodeTagContext unclosedContext = context.Pop();
                    formatted += unclosedContext.Tag.CloseTag(unclosedContext.Parameter, literalContents);
                    literalContents = string.Empty;
                }
            }

            return formatted;
        }
    }
}