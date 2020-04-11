using System;
using System.Collections.Generic;

namespace GraphBuilder.Models
{
    public class Graph
    {
        public Graph(string requestId, List<SourceNode> nodes = null, List<Link> links = null)
        {
            this.requestId = requestId;
            this.nodes = nodes;
            this.links = links;
        }
        public DateTime Timestamp { get; private set; } = DateTime.Now;
        public string requestId { get; private set; }
        public string message { get; set; }
        public List<SourceNode> nodes { get; private set; } = new List<SourceNode>();
        public List<Link> links { get; private set; } = new List<Link>();
    }
}
