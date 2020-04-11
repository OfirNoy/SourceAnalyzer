namespace GraphBuilder.Models
{
    public class SourceNode
    {
        public SourceNode(string id, string nodeType)
        {            
            this.type = nodeType;
            this.id = id;
        }

        public string id { get; private set; }        
        public string type { get; private set; }
    }
}
