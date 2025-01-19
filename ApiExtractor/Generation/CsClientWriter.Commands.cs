using ApiExtractor.Extraction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task WriteCommands(ExtractedDocumentation documentation) {
        await using StreamWriter icommandsWriter = OpenFileStream("ICommands.cs");
        await using StreamWriter commandsWriter  = OpenFileStream("Commands.cs");

        IDictionary<string, ISet<INterfaceChild>> interfaceTree = GenerateInterfaceTree(documentation.Commands);

        await icommandsWriter.WriteAsync($"""
                                          {FileHeader}

                                          using {Namespace}.API.Data;
                                          using {Namespace}.API.Exceptions;
                                          using System.CodeDom.Compiler;

                                          namespace {Namespace}.API;


                                          """);

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            await icommandsWriter.WriteAsync($"{GeneratedAttribute}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (INterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXCommand> { Command: var command }:
                        (string signature, string returnType) methodSignature = GenerateMethodSignature(command, true);
                        await icommandsWriter.WriteAsync($"""
                                                              /// <summary>
                                                              /// <para><c>{string.Join(' ', command.Name)}</c></para>
                                                              /// {command.Description.NewLinesToParagraphs()}
                                                              /// </summary>
                                                          {string.Join("\r\n", command.Parameters.Select(param => $"    /// <param name=\"{GetArgumentName(param, true)}\">{param.Description.NewLinesToParagraphs()}</param>"))}
                                                              /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
                                                              /// <exception cref="CommandNotFoundException">The command is not available on the endpoint's software version or hardware</exception>{(command.Parameters.Count == 0 ? "" : "\n    /// <exception cref=\"IllegalArgumentException\">One of the passed argument values is invalid</exception>")}
                                                              {methodSignature.signature};


                                                          """);
                        break;

                    case Subinterface<DocXCommand> s:
                        await icommandsWriter.WriteAsync($"    {s.InterfaceName} {s.GetterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await icommandsWriter.WriteAsync("}\r\n\r\n");
        }

        await commandsWriter.WriteAsync($$"""
                                          {{FileHeader}}

                                          using {{Namespace}}.API.Data;
                                          using {{Namespace}}.API.Serialization;
                                          using {{Namespace}}.Transport;
                                          using System.CodeDom.Compiler;

                                          namespace {{Namespace}}.API;

                                          {{GeneratedAttribute}}
                                          internal class Commands: {{string.Join(", ", interfaceTree.Keys)}} {
                                          
                                              private readonly IXapiTransport transport;
                                          
                                              public Commands(IXapiTransport transport) {
                                                  this.transport = transport;
                                              }

                                          """);

        foreach (DocXCommand command in documentation.Commands) {
            string path = $"new[] {{ {string.Join(", ", command.Name.Select(s => $"\"{s}\""))} }}";
            string parameters = command.Parameters.Any()
                ? $"new Dictionary<string, object?> {{ {string.Join(", ", command.Parameters.Select(parameter => $"{{ \"{parameter.Name}\", {(parameter.Type == DataType.Enum ? $"ValueSerializer.Serialize({GetArgumentName(parameter)})" : GetArgumentName(parameter))} }}"))} }}"
                : "null";

            await commandsWriter.WriteAsync($$"""
                                                  /// <inheritdoc />
                                                  {{GenerateMethodSignature(command, false).signature}} {
                                                      return await this.transport.CallMethod({{path}}, {{parameters}}).ConfigureAwait(false);
                                                  }


                                              """);

            _methodsGenerated++;
            _apiCommandsGenerated++;
        }

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXCommand> subinterface in interfaceNode.Value.OfType<Subinterface<DocXCommand>>()) {
                await commandsWriter.WriteAsync($"    {subinterface.InterfaceName} {interfaceNode.Key}.{subinterface.GetterName} => this;\r\n");
            }
        }

        await commandsWriter.WriteAsync("}");
    }

    private static (string signature, string returnType) GenerateMethodSignature(DocXCommand command, bool isInterfaceMethod) {
        const string returnType = "IDictionary<string, object>";
        return
            ($"{(isInterfaceMethod ? "" : "async ")}Task<{returnType}> {(isInterfaceMethod ? "" : GetInterfaceName(command) + '.')}{command.NameWithoutBrackets.Last()}({string.Join(", ", command.Parameters.OrderByDescending(parameter => parameter.Required).Select(parameter => $"{parameter.Type switch {
                DataType.Integer => "int",
                DataType.String  => "string",
                DataType.Enum    => GetEnumName(command, parameter.Name)
            }}{(parameter.Required ? "" : "?")} {GetArgumentName(parameter)}{(parameter.Required || !isInterfaceMethod ? "" : " = null")}"))})", returnType);
    }

}