﻿using ApiExtractor.Extraction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task writeStatuses(ExtractedDocumentation documentation) {
        await using StreamWriter statusWriter  = openFileStream("Statuses.cs");
        await using StreamWriter istatusWriter = openFileStream("IStatuses.cs");

        IDictionary<string, ISet<InterfaceChild>> interfaceTree = generateInterfaceTree(documentation.statuses);

        await istatusWriter.WriteAsync($"""
                                        {FILE_HEADER}

                                        using {NAMESPACE}.API.Data;
                                        using {NAMESPACE}.API.Exceptions;
                                        using System.CodeDom.Compiler;

                                        namespace {NAMESPACE}.API;


                                        """);

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            await istatusWriter.WriteAsync($"{GENERATED_ATTRIBUTE}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (InterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXStatus> { command: var status }:
                        (string signature, string returnType) methodSignature = generateMethodSignature(status, true);
                        await istatusWriter.WriteAsync($"""
                                                            /// <summary>
                                                            /// <para><c>{string.Join(' ', status.name)}</c></para>
                                                            /// {status.description.NewLinesToParagraphs()}
                                                            /// </summary>
                                                        {string.Join("\r\n", status.arrayIndexParameters.Select(param => $"    /// <param name=\"{getArgumentName(param, true)}\">{param.description.NewLinesToParagraphs()}</param>"))}
                                                            /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
                                                            /// <exception cref="CommandNotFoundException">The status is not available on the endpoint's software version or hardware</exception>
                                                            {methodSignature.signature};
                                                        
                                                            /// <summary>
                                                            /// <para><c>{string.Join(' ', status.name)}</c></para>
                                                            /// {status.description.NewLinesToParagraphs()}
                                                            /// <para>Fires an event when the status changes.</para>
                                                            /// </summary>
                                                            {generateEventSignature(status, true)};


                                                        """);
                        break;

                    case Subinterface<DocXStatus> s:
                        await istatusWriter.WriteAsync($"    {s.interfaceName} {s.getterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await istatusWriter.WriteAsync("}\r\n\r\n");
        }

        await statusWriter.WriteAsync($$"""
                                        {{FILE_HEADER}}

                                        using {{NAMESPACE}}.API.Data;
                                        using {{NAMESPACE}}.API.Serialization;
                                        using {{NAMESPACE}}.Transport;
                                        using System.CodeDom.Compiler;

                                        namespace {{NAMESPACE}}.API;

                                        {{GENERATED_ATTRIBUTE}}
                                        internal class Statuses: {{string.Join(", ", interfaceTree.Keys)}} {
                                        
                                            private readonly IXapiTransport transport;
                                            private readonly IFeedbackSubscriber feedbackSubscriber;
                                        
                                            public Statuses(IXapiTransport transport, IFeedbackSubscriber feedbackSubscriber) {
                                                this.transport = transport;
                                                this.feedbackSubscriber = feedbackSubscriber;
                                            }
                                            
                                        
                                        """);

        foreach (DocXStatus xStatus in documentation.statuses) {
            (string signature, string returnType) getterImplementationMethod = generateMethodSignature(xStatus, false);

            string path =
                $"new object[] {{ {string.Join(", ", xStatus.name.Select((s, i) => xStatus.arrayIndexParameters.FirstOrDefault(parameter => parameter.indexOfParameterInName == i) is { } pathParameter ? getArgumentName(pathParameter) : $"\"{s}\""))} }}";

            string serializedType       = xStatus.returnValueSpace.type == DataType.INTEGER && xStatus.returnValueSpace is not IntValueSpace { optionalValue: not null } ? "int" : "string";
            string remoteCallExpression = $"await this.transport.GetConfigurationOrStatus<{serializedType}>({path}).ConfigureAwait(false)";
            await statusWriter.WriteAsync($$"""
                                                /// <inheritdoc />
                                                {{getterImplementationMethod.signature}} {
                                                    return {{generateDeserializerExpression(xStatus, remoteCallExpression)}};
                                                }


                                            """);

            methodsGenerated++;

            string eventSignature = generateEventSignature(xStatus, false);
            await statusWriter.WriteAsync($$"""
                                                /// <inheritdoc />
                                                {{eventSignature}} {
                                                    add => feedbackSubscriber.Subscribe<{{serializedType}}, {{getterImplementationMethod.returnType}}>(new[] { {{string.Join(", ", xStatus.name.Where((s, i) => !xStatus.arrayIndexParameters.Any(parameter => parameter is { indexOfParameterInName: { } paramIndex } && paramIndex == i)).Select(s => $"\"{s}\""))}} }, value, serialized => {{generateDeserializerExpression(xStatus, "serialized")}}).Wait();
                                                    remove => feedbackSubscriber.Unsubscribe(value).Wait(feedbackSubscriber.Timeout);
                                                }


                                            """);

            eventsGenerated++;
        }

        foreach (KeyValuePair<string, ISet<InterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXStatus> subinterface in interfaceNode.Value.OfType<Subinterface<DocXStatus>>()) {
                await statusWriter.WriteAsync($"    {subinterface.interfaceName} {interfaceNode.Key}.{subinterface.getterName} => this;\r\n");
            }
        }

        await statusWriter.WriteAsync("}");

        static string generateDeserializerExpression(DocXStatus command, string remoteCallExpression) => command.returnValueSpace.type switch {
            DataType.ENUM                                                                                        => $"ValueSerializer.Deserialize<{getEnumName(command)}>({remoteCallExpression})",
            DataType.INTEGER when command.returnValueSpace is IntValueSpace { optionalValue: { } optionalValue } => $"ValueSerializer.Deserialize({remoteCallExpression}, \"{optionalValue}\")",
            _                                                                                                    => $"ValueSerializer.Deserialize({remoteCallExpression})"
        };
    }

    private static (string signature, string returnType) generateMethodSignature(DocXStatus status, bool isInterfaceMethod) {
        string returnType = $"{status.returnValueSpace.type switch {
            DataType.INTEGER when status.returnValueSpace is IntValueSpace { optionalValue: not null } => "int?",
            DataType.INTEGER                                                                           => "int",
            DataType.STRING                                                                            => "string",
            DataType.ENUM                                                                              => getEnumName(status)
        }}";
        return (
            $"{(isInterfaceMethod ? "" : "async ")}Task<{returnType}> {(isInterfaceMethod ? "" : getInterfaceName(status) + '.')}{status.nameWithoutBrackets.Last()}({string.Join(", ", status.arrayIndexParameters.Select(parameter => $"int {getArgumentName(parameter)}"))})",
            returnType);
    }

    private static string generateEventSignature(DocXStatus status, bool isInterfaceEvent) {
        return $"event FeedbackCallback<{status.returnValueSpace.type switch {
            DataType.INTEGER when status.returnValueSpace is IntValueSpace { optionalValue: not null } => "int?",
            DataType.INTEGER                                                                           => "int",
            DataType.STRING                                                                            => "string",
            DataType.ENUM                                                                              => getEnumName(status)
        }}> {(isInterfaceEvent ? "" : getInterfaceName(status) + '.')}{status.nameWithoutBrackets.Last()}Changed";
    }

}