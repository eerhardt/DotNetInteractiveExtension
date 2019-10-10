// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Rendering;
using static Microsoft.DotNet.Interactive.Rendering.PocketViewTags;
using static Microsoft.ML.Interactive.XPlot.PocketViewTagsExtensions;

namespace Microsoft.ML.Interactive.XPlot
{
    public class DecisionTreeData
    {

    }

    public static class PocketViewTagsExtensions
    {
        public static dynamic svg => PocketViewTags._.svg;
    }

    public class MlKernelExtension : IKernelExtension
    {
        public Task OnLoadAsync(IKernel kernel)
        {
            Formatter<DecisionTreeData>.Register((tree, writer) =>
            {
                writer.Write(GenerateTreeView(tree));
            }, "text/html");

            return Task.CompletedTask;
        }

        private string GenerateTreeView(DecisionTreeData tree)
        {
            var newHtmlDocument = new HtmlDocument();

            var renderingId = "a" + Guid.NewGuid().ToString();

            newHtmlDocument.DocumentNode.ChildNodes.Add(HtmlNode.CreateNode($"<svg id=\"{renderingId}\"></svg>"));
            newHtmlDocument.DocumentNode.ChildNodes.Add(GetRenderingScript());
            newHtmlDocument.DocumentNode.ChildNodes.Add(GetScriptNodeWithRequire(renderingId, tree));

            return newHtmlDocument.DocumentNode.WriteContentTo();
        }

        private HtmlNode GetRenderingScript()
        {
            var newScript = new StringBuilder();
            newScript.AppendLine("<script type=\"text/javascript\">");

            var assembly = typeof(MlKernelExtension).Assembly;
            var resourceStream = assembly.GetManifestResourceStream("Microsoft.DotNet.Interactive.XPlot.RegressionTree.js");
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
    var mlNetRequire = requirejs.config({context:'microsoft.ml-1.3.1',paths:{d3:'https://d3js.org/d3.v5.min'}});
    mlNetRequire(['d3'], function(d3) {");
            newScript.AppendLine();
            newScript.Append($"var sampleData = {GenerateData(tree)};");
            newScript.AppendLine();
            newScript.Append($@"dnRegressionTree.render(d3.select(""#{renderingId}""), sampleData, d3);");
            newScript.AppendLine();
            newScript.AppendLine(@"});
};
if ((typeof(requirejs) !==  typeof(Function)) || (typeof(requirejs.config) !== typeof(Function))) { 
    var script = document.createElement(""script""); 
    script.setAttribute(""src"", ""https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js""); 
    script.onload = function(){
        dotnet_regressiontree_renderTree();
    };
    document.getElementsByTagName(""head"")[0].appendChild(script); 
}
else {
    dotnet_regressiontree_renderTree();
}");
            newScript.AppendLine("</script>");
            return HtmlNode.CreateNode(newScript.ToString());
        }

        private static string GenerateData(DecisionTreeData tree)
        {
            return @"{ 
                label: ""f1 > 2"", 
                data: 1.0,
                children: [
                    { 
                        value: 51 ,
                        data: 0.1
                    }, 
                    { 
                        label: ""f1 > 2"", 
                        data:0.9,
                        children: [
                            { 
                                value: 1, 
                                data:0.8
                            }, 
                            { 
                                value: 2,
                                data:0.1 
                            }] 
                        }] 
                    }";
        }
    }
}
