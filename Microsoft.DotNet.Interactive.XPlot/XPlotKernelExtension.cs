// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Rendering;
using XPlot.Plotly;
using static Microsoft.DotNet.Interactive.Rendering.PocketViewTags;

namespace Microsoft.DotNet.Interactive.XPlot
{
    public class XPlotKernelExtension : IKernelExtension
    {
        public Task OnLoadAsync(IKernel kernel)
        {
            KernelBase kernelBase = (KernelBase)kernel;
            HookAssemblyLoad();

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

            var path = Path.Combine(currentAssemblyDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(path))
            {
                return context.LoadFromAssemblyPath(path);
            }

            return null;
        }

        private async Task HandleSubmitCode(
            SubmitCode submitCode,
            KernelPipelineContext pipelineContext,
            KernelPipelineContinuation next)
        {
            EnsureDataFrameFormatter();

            pipelineContext.OnExecute(invocationContext =>
            {
                XPlotExtensions.OnShow = chart =>
                {
                    string chartHtml = GetChartHtml(chart);

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

        private bool _dataFrameFormatterInit = false;
        private void EnsureDataFrameFormatter()
        {
            if (!_dataFrameFormatterInit)
            {
                Formatter<DataFrame>.Register((df, writer) =>
                {
                    var headers = new List<dynamic>();
                    headers.Add(th(i("index")));
                    headers.AddRange(df.Columns.Select(c => th(c)));

                    var rows = new List<List<dynamic>>();

                    for (var i = 0; i < Math.Min(15, df.RowCount); i++)
                    {
                        var cells = new List<dynamic>();

                        cells.Add(td(i));

                        foreach (object obj in df[i])
                        {
                            cells.Add(td(obj));
                        }

                        rows.Add(cells);
                    }

                    PocketView t = table(
                        thead(
                            headers
                        ),
                        tbody(
                            rows.Select(
                                r => tr(r))));

                    writer.Write(t);
                }, "text/html");

                _dataFrameFormatterInit = true;
            }
        }

        private string GetChartHtml(PlotlyChart chart)
        {
            string chartHtml = chart.GetInlineHtml();

            int scriptStart = chartHtml.IndexOf("<script>") + "<script>".Length;
            int scriptEnd = chartHtml.IndexOf("</script>");

            StringBuilder html = new StringBuilder(chartHtml.Length);
            html.Append(chartHtml.AsSpan().Slice(0, scriptStart));

            html.Append(@"
require.config({paths:{plotly:'https://cdn.plot.ly/plotly-latest.min'}});
require(['plotly'], function(Plotly) { 
");

            html.Append(chartHtml.AsSpan().Slice(scriptStart + 1, scriptEnd - scriptStart - 1));

            html.AppendLine(@"});");

            html.Append(chartHtml.AsSpan().Slice(scriptEnd));

            return html.ToString();
        }
    }
}
