namespace GraphBuilder.Models
{
    public class Link
    {
        public Link(string source, string target)
        {
            this.source = source;
            this.target = target;
        }

        public string source { get; private set; }        
        public string target { get; private set; }        
    }
}
