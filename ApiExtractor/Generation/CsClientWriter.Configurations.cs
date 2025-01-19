using ApiExtractor.Extraction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task writeConfiguration(ExtractedDocumentation documentation) {
        await using StreamWriter configurationWriter  = OpenFileStream("Configurations.cs");
        await using StreamWriter iconfigurationWriter = OpenFileStream("IConfigurations.cs");

        IDictionary<string, ISet<INterfaceChild>> interfaceTree = GenerateInterfaceTree(documentation.Configurations);

        await iconfigurationWriter.WriteAsync($"""
                                               {FileHeader}

                                               using {Namespace}.API.Data;
                                               using {Namespace}.API.Exceptions;
                                               using System.CodeDom.Compiler;

                                               namespace {Namespace}.API;


                                               """);

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            await iconfigurationWriter.WriteAsync($"{GeneratedAttribute}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (INterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXConfiguration> { Command: var configuration } when configuration.Parameters.Any():
                        (string signature, string returnType) setterSignature = GenerateMethodSignature(configuration, true, true);
                        (string signature, string returnType) getterSignature = GenerateMethodSignature(configuration, false, true);
                        await iconfigurationWriter.WriteAsync($"""
                                                                   /// <summary>
                                                                   /// <para><c>{string.Join(' ', configuration.Name)}</c></para>
                                                                   /// {configuration.Description.NewLinesToParagraphs()}
                                                                   /// </summary>
                                                               {string.Join("\r\n", configuration.Parameters.Select(param => $"    /// <param name=\"{GetArgumentName(param, true)}\">{param.Description.NewLinesToParagraphs()}</param>"))}
                                                                   /// <returns>A <see cref="Task"/> that will complete asynchronously when the configuration change has been received by the device.</returns>
                                                                   /// <exception cref="CommandNotFoundException">The configuration is not available on the endpoint's software version or hardware</exception>
                                                                   /// <exception cref="IllegalArgumentException">The passed argument value is invalid</exception>
                                                                   {setterSignature.signature};
                                                               
                                                                   /// <summary>
                                                                   /// <para><c>{string.Join(' ', configuration.Name)}</c></para>
                                                                   /// {configuration.Description.NewLinesToParagraphs()}
                                                                   /// </summary>
                                                               {string.Join("\r\n", configuration.Parameters.Where(parameter => parameter is IntParameter { IndexOfParameterInName: not null }).Select(param => $"    /// <param name=\"{GetArgumentName(param, true)}\">{param.Description.NewLinesToParagraphs()}</param>"))}
                                                                   /// <returns>A <see cref="Task&lt;T&gt;"/> that will complete asynchronously with the response from the device.</returns>
                                                                   /// <exception cref="CommandNotFoundException">The configuration is not available on the endpoint's software version or hardware</exception>
                                                                   {getterSignature.signature};
                                                               
                                                                   /// <summary>
                                                                   /// <para><c>{string.Join(' ', configuration.Name)}</c></para>
                                                                   /// {configuration.Description.NewLinesToParagraphs()}
                                                                   /// <para>Fires an event when the configuration changes.</para>
                                                                   /// </summary>
                                                                   {GenerateEventSignature(configuration, true)};


                                                               """);
                        break;

                    case Subinterface<DocXConfiguration> s:
                        await iconfigurationWriter.WriteAsync($"    {s.InterfaceName} {s.GetterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await iconfigurationWriter.WriteAsync("}\r\n\r\n");
        }

        await configurationWriter.WriteAsync($$"""
                                               {{FileHeader}}

                                               using {{Namespace}}.API.Data;
                                               using {{Namespace}}.API.Serialization;
                                               using {{Namespace}}.Transport;
                                               using System.CodeDom.Compiler;

                                               namespace {{Namespace}}.API;

                                               {{GeneratedAttribute}}
                                               internal class Configurations: {{string.Join(", ", interfaceTree.Keys)}} {
                                               
                                                   private readonly IXapiTransport transport;
                                                   private readonly IFeedbackSubscriber feedbackSubscriber;
                                               
                                                   public Configurations(IXapiTransport transport, IFeedbackSubscriber feedbackSubscriber) {
                                                       this.transport = transport;
                                                       this.feedbackSubscriber = feedbackSubscriber;
                                                   }
                                                   
                                               
                                               """);

        foreach (DocXConfiguration command in documentation.Configurations.Where(configuration => configuration.Parameters.Any())) {
            Parameter configurationParameter = command.Parameters.Last();

            string path =
                $"new object[] {{ {string.Join(", ", command.Name.Select((s, i) => command.Parameters.OfType<IntParameter>().FirstOrDefault(parameter => parameter.IndexOfParameterInName == i) is { } pathParameter ? GetArgumentName(pathParameter) : $"\"{s}\""))} }}";

            // Disallow showing Join Zoom button, but still allow it to be hidden, queried, and notified
            string deserializedExpression = command.Name.SequenceEqual(new[] { "xConfiguration", "UserInterface", "Features", "Call", "JoinZoom" })
                ? GetEnumName(command, configurationParameter.Name) + ".Hidden" : GetArgumentName(configurationParameter);

            await configurationWriter.WriteAsync($$"""
                                                       /// <inheritdoc />
                                                       {{GenerateMethodSignature(command, true, false).signature}} {
                                                           await this.transport.SetConfiguration({{path}}, ValueSerializer.Serialize({{deserializedExpression}})).ConfigureAwait(false);
                                                       }


                                                   """);

            _methodsGenerated++;

            (string signature, string returnType) getterImplementationMethod = GenerateMethodSignature(command, false, false);
            string                                readSerializedType         = configurationParameter.Type == DataType.Integer ? "int" : "string";
            string                                remoteCallExpression       = $"await this.transport.GetConfigurationOrStatus<{readSerializedType}>({path}).ConfigureAwait(false)";
            await configurationWriter.WriteAsync($$"""
                                                       /// <inheritdoc />
                                                       {{getterImplementationMethod.signature}} {
                                                           return {{GenerateDeserializerExpression(configurationParameter, command, remoteCallExpression)}};
                                                       }


                                                   """);

            _methodsGenerated++;

            await configurationWriter.WriteAsync($$"""
                                                       /// <inheritdoc />
                                                       {{GenerateEventSignature(command, false)}} {
                                                           add => feedbackSubscriber.Subscribe<{{readSerializedType}}, {{getterImplementationMethod.returnType}}>(new[] { {{string.Join(", ", command.Name.Where((s, i) => !command.Parameters.Any(parameter => parameter is IntParameter { IndexOfParameterInName: { } paramIndex } && paramIndex == i)).Select(s => $"\"{s}\""))}} }, value, serialized => {{GenerateDeserializerExpression(configurationParameter, command, "serialized")}}).Wait(feedbackSubscriber.Timeout);
                                                           remove => feedbackSubscriber.Unsubscribe(value).Wait(feedbackSubscriber.Timeout);
                                                       }


                                                   """);

            _eventsGenerated++;

            _apiCommandsGenerated++;
        }

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXConfiguration> subinterface in interfaceNode.Value.OfType<Subinterface<DocXConfiguration>>()) {
                await configurationWriter.WriteAsync($"    {subinterface.InterfaceName} {interfaceNode.Key}.{subinterface.GetterName} => this;\r\n");
            }
        }

        await configurationWriter.WriteAsync("}");

        static string GenerateDeserializerExpression(Parameter configurationParameter, DocXConfiguration command, string remoteCallExpression) => configurationParameter.Type == DataType.Enum
            ? $"ValueSerializer.Deserialize<{GetEnumName(command, configurationParameter.Name)}>({remoteCallExpression})"
            : $"ValueSerializer.Deserialize({remoteCallExpression})";
    }

    private static (string signature, string returnType) GenerateMethodSignature(DocXConfiguration configuration, bool isSetter, bool isInterfaceMethod) {
        string returnType = isSetter ? "" : configuration.Parameters.Last() switch {
            { Type: DataType.String }               => "string",
            { Type: DataType.Integer }              => "int",
            { Type: DataType.Enum, Name: var name } => GetEnumName(configuration, name)
        };
        return (
            $"{(isInterfaceMethod ? "" : "async ")}{(returnType == "" ? "Task" : $"Task<{returnType}>")} {(isInterfaceMethod ? "" : GetInterfaceName(configuration) + '.')}{configuration.NameWithoutBrackets.Last()}({string.Join(", ", configuration.Parameters.SkipLast(isSetter ? 0 : 1).Select(parameter => $"{parameter.Type switch {
                DataType.Integer => "int",
                DataType.String  => "string",
                DataType.Enum    => GetEnumName(configuration, parameter.Name)
            }} {GetArgumentName(parameter)}"))})", returnType);
    }

    private static string GenerateEventSignature(DocXConfiguration configuration, bool isInterfaceEvent) {
        return $"event FeedbackCallback<{configuration.Parameters.Last() switch {
            { Type: DataType.String }               => "string",
            { Type: DataType.Integer }              => "int",
            { Type: DataType.Enum, Name: var name } => GetEnumName(configuration, name)
        }}> {(isInterfaceEvent ? "" : GetInterfaceName(configuration) + '.')}{configuration.NameWithoutBrackets.Last()}Changed";
    }

}