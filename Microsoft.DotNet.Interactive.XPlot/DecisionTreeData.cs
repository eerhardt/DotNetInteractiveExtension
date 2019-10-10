using System.Collections.Generic;

namespace Microsoft.DotNet.Interactive.XPlot
{
    public class DecisionTreeData
    {
        public NodeData Root { get; set; }
    }

    public class NodeData
    {
        public string Label { get; set; }
        public float Data { get; set; }
        public float Value { get; set; }

        public List<NodeData> Children { get; } = new List<NodeData>();
    }
}
