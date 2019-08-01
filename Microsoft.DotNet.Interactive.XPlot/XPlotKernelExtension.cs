// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using XPlot.Plotly;

namespace Microsoft.DotNet.Interactive.XPlot
{
    public class XPlotKernelExtension : IKernelExtension
    {
        private bool _jsInitialized = false;

        public Task OnLoadAsync(IKernel kernel)
        {
            KernelBase kernelBase = (KernelBase)kernel;

            kernelBase.Pipeline.AddMiddleware(
                (command, pipelineContext, next) =>
                    command switch
                    {
                        SubmitCode submitCode =>
                            HandleSubmitCode(
                                submitCode,
                                pipelineContext,
                                next),

                        _ => next(command, pipelineContext)
                    });

            HookAssemblyLoad();
            return Task.CompletedTask;
        }

        private void HookAssemblyLoad()
        {
            var loadContext = AssemblyLoadContext.GetLoadContext(typeof(XPlotKernelExtension).Assembly);
            loadContext.Resolving += LoadContext_Resolving;
        }

        private Assembly LoadContext_Resolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            string currentAssemblyDirectory = Path.GetDirectoryName(typeof(XPlotKernelExtension).Assembly.Location);
            return context.LoadFromAssemblyPath(Path.Combine(currentAssemblyDirectory, $"{assemblyName.Name}.dll"));
        }

        private async Task HandleSubmitCode(
            SubmitCode submitCode,
            KernelPipelineContext pipelineContext,
            KernelPipelineContinuation next)
        {
            pipelineContext.OnExecute(invocationContext =>
            {
                XPlotExtensions.OnShow = chart =>
                {
                    EnsureJSInitialized(submitCode, invocationContext);

                    string chartHtml = chart.GetInlineHtml();

                    var formattedValues = new List<FormattedValue>
                    {
                        new FormattedValue("text/html", chartHtml)
                    };

                    invocationContext.OnNext(
                        new ValueProduced(
                            chartHtml,
                            submitCode,
                            false,
                            formattedValues));
                };

                return Task.CompletedTask;
            });

            await next(submitCode, pipelineContext)
                .ConfigureAwait(false);
        }

        private void EnsureJSInitialized(SubmitCode submitCode, KernelInvocationContext invocationContext)
        {
            if (!_jsInitialized)
            {
                string js = GetPlotlyJS();

                var formattedValues = new List<FormattedValue>
                    {
                        new FormattedValue("text/html", js)
                    };

                invocationContext.OnNext(
                    new ValueProduced(
                        js,
                        submitCode,
                        false,
                        formattedValues));

                _jsInitialized = true;
            }
        }

        private static string GetPlotlyJS()
        {
            const string prePlotlyInclude = @"
<script type=""text/javascript"">
var require_save = require;
var requirejs_save = requirejs;
var define_save = define;
var MathJax_save = MathJax;
MathJax = require = requirejs = define = undefined;
";

            const string postPlotlyInclude = @"
require = require_save;
requirejs = requirejs_save;
define = define_save;
MathJax = MathJax_save;
function ifsharpMakeImage(gd, fmt)
{
    return Plotly.toImage(gd, { format: fmt})
        .then(function(url) {
                var img = document.createElement('img');
                img.setAttribute('src', url);
                var div = document.createElement('div');
                div.appendChild(img);
                gd.parentNode.replaceChild(div, gd);
            });
        }
        function ifsharpMakePng(gd)
        {
            var fmt =
                (document.documentMode || / Edge /.test(navigator.userAgent)) ?
                    'svg' : 'png';
            return ifsharpMakeImage(gd, fmt);
        }
        function ifsharpMakeSvg(gd)
        {
            return ifsharpMakeImage(gd, 'svg');
        }
</script>
";
            StringBuilder js = new StringBuilder();
            string plotlyjs;
            using (Stream stream = typeof(XPlotKernelExtension).Assembly.GetManifestResourceStream("Microsoft.DotNet.Interactive.XPlot.plotly-latest.min.js"))
            using (StreamReader reader = new StreamReader(stream))
            {
                plotlyjs = reader.ReadToEnd();
            }

            js.Append(prePlotlyInclude);
            js.Append(plotlyjs);
            js.Append(postPlotlyInclude);

            return js.ToString();
        }
    }
}
