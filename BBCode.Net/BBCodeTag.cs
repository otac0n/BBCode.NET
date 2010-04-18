namespace BBCode
{
    using System;

    public sealed class BBCodeTag
    {
        private string tagName;
        private bool parameterized;
        private string openTag;
        private string closeTag;
        private BBCodeTagTypes tagType;

        public BBCodeTag(string tagName, bool parameterized, string openTag, string closeTag, BBCodeTagTypes tagType)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                throw new ArgumentNullException("tagName", "The tag name of every BBCode tag must be a non-empty string.");
            }
            else if ((tagType & (BBCodeTagTypes.NewContext | BBCodeTagTypes.Literal)) == (BBCodeTagTypes.NewContext | BBCodeTagTypes.Literal))
            {
                throw new ArgumentOutOfRangeException("tagType", "The tag type of a BBCode tag may not be both Literal and NewContext.");
            }

            this.tagName = tagName;
            this.openTag = openTag;
            this.parameterized = parameterized;
            this.closeTag = closeTag;
            this.tagType = tagType;
        }

        public string TagName
        {
            get
            {
                return this.tagName;
            }
        }

        public bool Parameterized
        {
            get
            {
                return this.parameterized;
            }
        }

        public bool IsLiteral
        {
            get
            {
                return (this.tagType & BBCodeTagTypes.Literal) > 0;
            }
        }

        public bool IsNewContext
        {
            get
            {
                return (this.tagType & BBCodeTagTypes.NewContext) > 0;
            }
        }

        public string OpenTag(string parameter)
        {
            return string.Format(this.openTag, parameter);
        }

        public string CloseTag(string parameter, string literalContents)
        {
            return string.Format(this.closeTag, parameter, literalContents);
        }
    }
}