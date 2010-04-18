namespace LanLordz.SiteTools
{
    using System;

    internal sealed class BBCodeTagContext
    {
        private BBCodeTag tag;
        private string parameter;

        public BBCodeTagContext(BBCodeTag tag, string parameter)
        {
            this.tag = tag;
            this.parameter = parameter;
        }

        public BBCodeTag Tag
        {
            get
            {
                return this.tag;
            }
        }

        public string Parameter
        {
            get
            {
                return this.parameter;
            }
        }
    }
}