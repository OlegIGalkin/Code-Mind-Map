namespace CodeMindMap
{
    internal struct SelectedText
    {
        public string Text;
        public int TopLine;
        public string DocumentPath;

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Text) ? string.Empty : Text.ToString();
        }
    }
}
