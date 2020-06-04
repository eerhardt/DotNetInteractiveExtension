// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.CSharp;

namespace Microsoft.Data.Analysis.Interactive
{
    public class DataFrameKernelExtension : IKernelExtension
    {
        bool _generateCsvMethod = false;

        public string GetFriendlyName(Type type)
        {
            string friendlyName = type.Name;
            if (type.IsArray)
            {
                // Not handled yet
                return "DataFrameColumn";
            }
            if (type.IsGenericType)
            {
                int backTick = friendlyName.IndexOf('`');
                if (backTick > 0)
                {
                    friendlyName = friendlyName.Remove(backTick);
                }
                friendlyName += "<";
                Type[] typeParameters = type.GetGenericArguments();
                for (int i = 0; i < typeParameters.Length; ++i)
                {
                    string typeParamName = GetFriendlyName(typeParameters[i]);
                    friendlyName += (i == 0 ? typeParamName : "," + typeParamName);
                }
                friendlyName += ">";
            }

            return friendlyName;
        }

        public StringBuilder GetTypedDataFrameWithProperties(DataFrame dataFrame, string resultTypeName, out StringBuilder prettyFormatter)
        {
            prettyFormatter = new StringBuilder();
            StringBuilder stringBuilder = new StringBuilder();
            string constructor = @$"
public class {resultTypeName} : DataFrame 
{{
    public {resultTypeName}(DataFrame dataFrame)
    {{
        foreach (var column in dataFrame.Columns)
        {{
            Columns.Add(column);
        }}
    }}
";
            stringBuilder.Append(constructor);
            prettyFormatter.Append(constructor);

            foreach (var column in dataFrame.Columns)
            {
                string columnName = column.Name.Replace(" ", string.Empty);
                string typeName = GetFriendlyName(column.GetType());
                stringBuilder.Append($@"
    public {typeName} {columnName} 
    {{
        get
        {{
            int columnIndex = Columns.IndexOf(""{columnName}"");
            return Columns[columnIndex] as {typeName};
        }}
    }}");
                stringBuilder.AppendLine();
                prettyFormatter.Append($@"
    public {typeName} {columnName} {{ get; }}");
                prettyFormatter.AppendLine();
            }

            stringBuilder.AppendLine();
            stringBuilder.Append(@"}");
            prettyFormatter.AppendLine();
            prettyFormatter.Append(@"}");
            prettyFormatter.AppendLine();
            if (_generateCsvMethod)
            {
                AddLoadCsvToTypedDataFrame(stringBuilder, resultTypeName, prettyFormatter);
            }
            return stringBuilder;
        }

        public void AddLoadCsvToTypedDataFrame(StringBuilder stringBuilder, string resultTypeName, StringBuilder prettyFormatter)
        {
            string loadCsv = $@"
public static new {resultTypeName} LoadCsv(string filename,
                            char separator = ',', bool header = true,
                            string[] columnNames = null, Type[] dataTypes = null,
                            int numRows = -1, int guessRows = 10,
                            bool addIndexColumn = false)";
            stringBuilder.Append($@"{loadCsv}
        {{
            DataFrame df = DataFrame.LoadCsv(filename: filename, separator: separator, header: header, columnNames: columnNames, dataTypes: dataTypes, numRows: numRows,
      guessRows: guessRows, addIndexColumn: addIndexColumn);
            {resultTypeName} ret = new {resultTypeName}(df);
            return ret;
        }}"

            );
            prettyFormatter.Append(loadCsv);
            prettyFormatter.AppendLine();
        }

        public async Task HandleCsvAsync(CSharpKernel kernel, FileInfo csv, string typeName, string variableName, KernelInvocationContext context)
        {
            _generateCsvMethod = true;
            // Infer the type and generated name from fileName
            string fileName = csv.Name.Split('.')[0]; //Something like housing.A.B.csv would return housing
            typeName ??= fileName.Replace(" ", "");
            StringBuilder strBuilder;
            StringBuilder prettyFormatter;
            using (var stream = csv.Open(FileMode.Open))
            {
                DataFrame df = DataFrame.LoadCsv(stream);
                strBuilder = GenerateTypedDataFrame(df, typeName, context, out StringBuilder outPrettyFormatter);
                prettyFormatter = outPrettyFormatter;
            }
            _generateCsvMethod = false;

            // Create a new DataFrame var called dataFrameName
            variableName ??= typeName + "DataFrame";
            strBuilder.AppendLine();
            string buildNamedDataFrame = $@"
DataFrame _df = DataFrame.LoadCsv(filename: @""{csv.FullName}"");
{typeName} {variableName} = new {typeName}(_df);
";
            strBuilder.Append(buildNamedDataFrame);
            prettyFormatter.AppendLine();
            prettyFormatter.Append(buildNamedDataFrame);

            context.Display($"Created variable {variableName} of type {typeName}");

            await kernel.SubmitCodeAsync(strBuilder.ToString());
        }

        public StringBuilder GenerateTypedDataFrame(DataFrame df, string typeName, KernelInvocationContext context, out StringBuilder prettyFormatter)
        {
            StringBuilder typedDataFrame = GetTypedDataFrameWithProperties(df, typeName, out prettyFormatter);
            return typedDataFrame;
        }

        public async Task HandleDataFrameAsync(CSharpKernel csharpKernel, string dataFrameName, string typeName, KernelInvocationContext context)
        {
            var variables = csharpKernel.ScriptState.Variables;
            for (int i = 0; i < variables.Length; i++)
            {
                CodeAnalysis.Scripting.ScriptVariable variable = variables[i];
                if ((dataFrameName == null || variable.Name == dataFrameName) && variable.Value is DataFrame df)
                {
                    var strBuilder = GenerateTypedDataFrame(df, typeName, context, out StringBuilder prettyFormatter);
                    await context.DisplayAsync(prettyFormatter.ToString());
                    await csharpKernel.SubmitCodeAsync(strBuilder.ToString());
                }
            }
        }

        public Task OnLoadAsync(IKernel kernel)
        {
            if (kernel.FindKernel("csharp") is CSharpKernel csharpKernel)
            {
                var directive = new Command("#!generatedataframe")
                {
                    Handler = CommandHandler.Create(async (FileInfo csv, string variableName, string typeName, KernelInvocationContext context) =>
                    {
                        // do the job
                        try
                        {
                            if (csv != null)
                            {
                                await HandleCsvAsync(csharpKernel, csv, typeName, variableName, context);
                            }
                            else
                            {
                                await HandleDataFrameAsync(csharpKernel, variableName, typeName, context);
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Fail(ex, $"Encountered an exception. Could not create type { (csv != null ? csv.FullName : typeName)}");
                        }
                    })
                };

                directive.AddOption(new Option<FileInfo>(
                    "--csv", "Read in a csv file into a DataFrame with strong properties. Also emits the generated DataFrame type").ExistingOnly());

                directive.AddOption(new Option<string>(
                    "--type-name",
                    getDefaultValue: () => "InteractiveDataFrame",
                    "The name of the generated DataFrame type. Defaults to InteractiveDataFrame"));

                directive.AddOption(new Option<string>(
                    "--variable-name",
                    "The DataFrame variable name to initialize"));
                
                csharpKernel.AddDirective(directive);
            }

            return Task.CompletedTask;
        }
    }
}
