// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using System.Text.Json;

namespace Microsoft.ML.DotNet.Interactive
{
    public static class DecisionTreeDataFormatting
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        internal static string GenerateTreeView(DecisionTreeData tree)
        {
            var newHtmlDocument = new HtmlDocument();

            var renderingId = $"a{Guid.NewGuid()}";

            newHtmlDocument.DocumentNode.ChildNodes.Add(HtmlNode.CreateNode($"<svg id=\"{renderingId}\"></svg>"));
            newHtmlDocument.DocumentNode.ChildNodes.Add(GetRenderingScript());
            newHtmlDocument.DocumentNode.ChildNodes.Add(GetScriptNodeWithRequire(renderingId, tree));

            return newHtmlDocument.DocumentNode.WriteContentTo();
        }

        private static HtmlNode GetRenderingScript()
        {
            var newScript = new StringBuilder();
            newScript.AppendLine("<script type=\"text/javascript\">");

            var assembly = typeof(DecisionTreeDataFormatting).Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(n => n.EndsWith("RegressionTree.js"));
            var resourceStream = assembly.GetManifestResourceStream(resourceName);
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                newScript.AppendLine(reader.ReadToEnd());
            }

            newScript.AppendLine("</script>");
            return HtmlNode.CreateNode(newScript.ToString());
        }

        private static HtmlNode GetScriptNodeWithRequire(string renderingId, DecisionTreeData tree)
        {
            var newScript = new StringBuilder();
            newScript.AppendLine("<script type=\"text/javascript\">");
            newScript.AppendLine(@"
var dotnet_regressiontree_renderTree = function() {
    let mlNetDTreeLoader = window.configureRequire({paths:{d3:'https://d3js.org/d3.v5.min'}});
    mlNetDTreeLoader(['d3'], function (d3) {");
            newScript.AppendLine();
            newScript.Append($"var treeData = {GenerateData(tree)};");
            newScript.AppendLine();
            newScript.Append($@"dnRegressionTree.render(d3.select(""#{renderingId}""), treeData, d3);");
            newScript.AppendLine();
            newScript.AppendLine(@"});
};

dotnet_regressiontree_renderTree();");
            newScript.AppendLine("</script>");
            return HtmlNode.CreateNode(newScript.ToString());
        }
      
        private static string GenerateData(DecisionTreeData tree)
        {
            return JsonSerializer.Serialize(tree.Root, JsonSerializerOptions);
        }
    }
}
