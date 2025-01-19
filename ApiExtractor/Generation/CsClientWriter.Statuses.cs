using ApiExtractor.Extraction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task WriteStatuses(ExtractedDocumentation documentation) {
        await using StreamWriter statusWriter  = OpenFileStream("Statuses.cs");
        await using StreamWriter istatusWriter = OpenFileStream("IStatuses.cs");

        IDictionary<string, ISet<INterfaceChild>> interfaceTree = GenerateInterfaceTree(documentation.Statuses);

        await istatusWriter.WriteAsync($"""
                                        {FileHeader}

                                        using {Namespace}.API.Data;
                                        using {Namespace}.API.Exceptions;
                                        using System.CodeDom.Compiler;

                                        namespace {Namespace}.API;


                                        """);

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            await istatusWriter.WriteAsync($"{GeneratedAttribute}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (INterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXStatus> { Command: var status }:
                        (string signature, string returnType) methodSignature = GenerateMethodSignature(status, true);
                        await istatusWriter.WriteAsync($"""
                                                            /// <summary>
                                                            /// <para><c>{string.Join(' ', status.Name)}</c></para>
                                                            /// {status.Description.NewLinesToParagraphs()}
                                                            /// </summary>
                                                        {string.Join("\r\n", status.ArrayIndexParameters.Select(param => $"    /// <param name=\"{GetArgumentName(param, true)}\">{param.Description.NewLinesToParagraphs()}</param>"))}
                                                            /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
                                                            /// <exception cref="CommandNotFoundException">The status is not available on the endpoint's software version or hardware</exception>
                                                            {methodSignature.signature};
                                                        
                                                            /// <summary>
                                                            /// <para><c>{string.Join(' ', status.Name)}</c></para>
                                                            /// {status.Description.NewLinesToParagraphs()}
                                                            /// <para>Fires an event when the status changes.</para>
                                                            /// </summary>
                                                            {GenerateEventSignature(status, true)};


                                                        """);
                        break;

                    case Subinterface<DocXStatus> s:
                        await istatusWriter.WriteAsync($"    {s.InterfaceName} {s.GetterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await istatusWriter.WriteAsync("}\r\n\r\n");
        }

        await statusWriter.WriteAsync($$"""
                                        {{FileHeader}}

                                        using {{Namespace}}.API.Data;
                                        using {{Namespace}}.API.Serialization;
                                        using {{Namespace}}.Transport;
                                        using System.CodeDom.Compiler;

                                        namespace {{Namespace}}.API;

                                        {{GeneratedAttribute}}
                                        internal class Statuses: {{string.Join(", ", interfaceTree.Keys)}} {
                                        
                                            private readonly IXapiTransport transport;
                                            private readonly IFeedbackSubscriber feedbackSubscriber;
                                        
                                            public Statuses(IXapiTransport transport, IFeedbackSubscriber feedbackSubscriber) {
                                                this.transport = transport;
                                                this.feedbackSubscriber = feedbackSubscriber;
                                            }
                                            
                                        
                                        """);

        foreach (DocXStatus xStatus in documentation.Statuses) {
            (string signature, string returnType) getterImplementationMethod = GenerateMethodSignature(xStatus, false);

            string path =
                $"new object[] {{ {string.Join(", ", xStatus.Name.Select((s, i) => xStatus.ArrayIndexParameters.FirstOrDefault(parameter => parameter.IndexOfParameterInName == i) is { } pathParameter ? GetArgumentName(pathParameter) : $"\"{s}\""))} }}";

            string serializedType       = xStatus.ReturnValueSpace.Type == DataType.Integer && xStatus.ReturnValueSpace is not IntValueSpace { OptionalValue: not null } ? "int" : "string";
            string remoteCallExpression = $"await this.transport.GetConfigurationOrStatus<{serializedType}>({path}).ConfigureAwait(false)";
            await statusWriter.WriteAsync($$"""
                                                /// <inheritdoc />
                                                {{getterImplementationMethod.signature}} {
                                                    return {{GenerateDeserializerExpression(xStatus, remoteCallExpression)}};
                                                }


                                            """);

            _methodsGenerated++;

            string eventSignature = GenerateEventSignature(xStatus, false);
            await statusWriter.WriteAsync($$"""
                                                /// <inheritdoc />
                                                {{eventSignature}} {
                                                    add => feedbackSubscriber.Subscribe<{{serializedType}}, {{getterImplementationMethod.returnType}}>(new[] { {{string.Join(", ", xStatus.Name.Where((s, i) => !xStatus.ArrayIndexParameters.Any(parameter => parameter is { IndexOfParameterInName: { } paramIndex } && paramIndex == i)).Select(s => $"\"{s}\""))}} }, value, serialized => {{GenerateDeserializerExpression(xStatus, "serialized")}}).Wait();
                                                    remove => feedbackSubscriber.Unsubscribe(value).Wait(feedbackSubscriber.Timeout);
                                                }


                                            """);

            _eventsGenerated++;

            _apiCommandsGenerated++;
        }

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXStatus> subinterface in interfaceNode.Value.OfType<Subinterface<DocXStatus>>()) {
                await statusWriter.WriteAsync($"    {subinterface.InterfaceName} {interfaceNode.Key}.{subinterface.GetterName} => this;\r\n");
            }
        }

        await statusWriter.WriteAsync("}");

        static string GenerateDeserializerExpression(DocXStatus command, string remoteCallExpression) => command.ReturnValueSpace.Type switch {
            DataType.Enum                                                                                        => $"ValueSerializer.Deserialize<{GetEnumName(command)}>({remoteCallExpression})",
            DataType.Integer when command.ReturnValueSpace is IntValueSpace { OptionalValue: { } optionalValue } => $"ValueSerializer.Deserialize({remoteCallExpression}, \"{optionalValue}\")",
            _                                                                                                    => $"ValueSerializer.Deserialize({remoteCallExpression})"
        };
    }

    private static (string signature, string returnType) GenerateMethodSignature(DocXStatus status, bool isInterfaceMethod) {
        string returnType = $"{status.ReturnValueSpace.Type switch {
            DataType.Integer when status.ReturnValueSpace is IntValueSpace { OptionalValue: not null } => "int?",
            DataType.Integer                                                                           => "int",
            DataType.String                                                                            => "string",
            DataType.Enum                                                                              => GetEnumName(status)
        }}";
        return (
            $"{(isInterfaceMethod ? "" : "async ")}Task<{returnType}> {(isInterfaceMethod ? "" : GetInterfaceName(status) + '.')}{status.NameWithoutBrackets.Last()}({string.Join(", ", status.ArrayIndexParameters.Select(parameter => $"int {GetArgumentName(parameter)}"))})",
            returnType);
    }

    private static string GenerateEventSignature(DocXStatus status, bool isInterfaceEvent) {
        return $"event FeedbackCallback<{status.ReturnValueSpace.Type switch {
            DataType.Integer when status.ReturnValueSpace is IntValueSpace { OptionalValue: not null } => "int?",
            DataType.Integer                                                                           => "int",
            DataType.String                                                                            => "string",
            DataType.Enum                                                                              => GetEnumName(status)
        }}> {(isInterfaceEvent ? "" : GetInterfaceName(status) + '.')}{status.NameWithoutBrackets.Last()}Changed";
    }

}