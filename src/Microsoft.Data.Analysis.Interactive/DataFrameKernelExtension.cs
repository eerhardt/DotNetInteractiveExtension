// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using static Microsoft.DotNet.Interactive.Formatting.PocketViewTags;

namespace Microsoft.Data.Analysis.Interactive
{
    public class DataFrameKernelExtension : IKernelExtension
    {
        public Task OnLoadAsync(IKernel kernel)
        {
            //Formatter<DataFrame>.Register((tree, writer) =>
            //{
            //    writer.Write("");
            //}, "text/html");

            Formatter<DataFrame>.Register((df, writer) =>
            {
                const int TAKE = 500;
                const int SIZE = 20;

                var uniqueId = $"table_{DateTime.Now.Ticks}";

                var title = h3[style: "text-align: center;"]($"DataFrame ({df.Columns.Count} columns, {df.Rows.Count} rows) | MAX rows: {TAKE}");

                var header = new List<IHtmlContent>
                {
                    th(i("index"))
                };
                header.AddRange(df.Columns.Select(c => (IHtmlContent)th(c.Name)));

                // table body
                var rows = new List<List<IHtmlContent>>();
                for (var index = 0; index < Math.Min(TAKE, df.Rows.Count); index++)
                {
                    var cells = new List<IHtmlContent>
                    {
                        td(i((index)))
                    };
                    foreach (var obj in df.Rows[index])
                    {
                        cells.Add(td(obj));
                    }
                    rows.Add(cells);
                }

                BuildHideRowsScript(uniqueId);

                var footer = new List<IHtmlContent>();
                footer.Add(b[style: "margin: 2px;"]("Page"));
                for (var page = 0; page < TAKE / SIZE; page++)
                {
                    var paginateScript = BuildHideRowsScript(uniqueId) + BuildPageScript(page, SIZE, uniqueId);
                    footer.Add(button[style: "margin: 2px;", onclick: paginateScript](page));
                }

                //table
                var t = table[id: $"{uniqueId}"](
                    caption(title),
                    thead(tr(header)),
                    tbody(rows.Select(r => tr[style: "display: none"](r))),
                    tfoot(tr(td[colspan: df.Columns.Count + 1](footer)))
                );
                writer.Write(t);

                //show first page
                writer.Write($"<script>{BuildPageScript(0, SIZE, uniqueId)}</script>");

            }, "text/html");

            var kernelBase = kernel as KernelBase;

            var directive = new Command("#!doit")
            {
                Handler = CommandHandler.Create(async (FileInfo csv, string typeName, KernelInvocationContext context) =>
                {
                    // do the job
                    var command = new SubmitCode(@$"public class {typeName}{{}}");
                    context.Publish(new DisplayedValueProduced($"emitting {typeName} from {csv.FullName}", context.Command));
                    await context.HandlingKernel.SendAsync(command);
                })
            };

            directive.AddOption(new Option<FileInfo>(
                "csv").ExistingOnly());

            directive.AddOption(new Option<string>(
                "typeName",
                getDefaultValue: () => "Foo"));

            kernelBase.AddDirective(directive);

            return Task.CompletedTask;

            static string BuildPageScript(int page, int size, string uniqueId)
            {
                var script = string.Empty;
                script += $"var els = document.querySelectorAll('#{uniqueId} tbody tr:nth-child(n+{page * size + 1})'); ";
                script += $"for (var j = 0; j < {size}; j++) {{ els[j].style.display='table-row'; }}";
                return script;
            }

            static string BuildHideRowsScript(string uniqueId)
            {
                string script = string.Empty;
                script += $"var els = document.querySelectorAll('#{uniqueId} tbody tr:nth-child(n)'); ";
                script += "for (var i = 0; i < els.length; i++) { els[i].style.display='none'; } ";
                return script;
            }
        }
    }
}
