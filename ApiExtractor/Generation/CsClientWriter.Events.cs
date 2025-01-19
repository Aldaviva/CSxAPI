using ApiExtractor.Extraction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task WriteEvents(ExtractedDocumentation documentation) {
        await using StreamWriter eventWriter             = OpenFileStream("Events.cs");
        await using StreamWriter ieventWriter            = OpenFileStream("IEvents.cs");
        await using StreamWriter eventDataWriter         = OpenFileStream("Data\\Events.cs");
        await using StreamWriter eventDeserializerWriter = OpenFileStream("Serialization\\EventDeserializer.cs");

        IDictionary<string, ISet<INterfaceChild>> interfaceTree = GenerateInterfaceTree(documentation.Events);

        await ieventWriter.WriteAsync($"""
                                       {FileHeader}

                                       using {Namespace}.API.Data;
                                       using System.CodeDom.Compiler;

                                       namespace {Namespace}.API;


                                       """);

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            await ieventWriter.WriteAsync($"{GeneratedAttribute}\r\npublic interface {interfaceNode.Key} {{\r\n\r\n");

            foreach (INterfaceChild interfaceChild in interfaceNode.Value) {
                switch (interfaceChild) {
                    case InterfaceMethod<DocXEvent> { Command: var xEvent }:
                        await ieventWriter.WriteAsync($"""
                                                           /// <summary>
                                                           /// <para><c>{string.Join(' ', xEvent.Name)}</c></para>
                                                           /// <para>Fired when the event is received from the device.</para>
                                                           /// </summary>
                                                           {GenerateEventSignature(xEvent, true).signature};


                                                       """);
                        break;

                    case Subinterface<DocXEvent> s:
                        await ieventWriter.WriteAsync($"    {s.InterfaceName} {s.GetterName} {{ get; }}\r\n\r\n");
                        break;
                }
            }

            await ieventWriter.WriteAsync("}\r\n\r\n");
        }

        await eventDataWriter.WriteAsync($"""
                                          {FileHeader}

                                          using System.CodeDom.Compiler;

                                          namespace {Namespace}.API.Data;


                                          """);

        await eventDeserializerWriter.WriteAsync($$"""
                                                   {{FileHeader}}

                                                   using {{Namespace}}.API.Data;
                                                   using Newtonsoft.Json.Linq;
                                                   using System.CodeDom.Compiler;
                                                   using System.Collections.ObjectModel;

                                                   namespace {{Namespace}}.API.Serialization;

                                                   {{GeneratedAttribute}}
                                                   internal static class EventDeserializer {
                                                       
                                                       private static readonly IDictionary<Type, Func<JToken, object>> eventDeserializers = new Dictionary<Type, Func<JToken, object>>();
                                                   
                                                       public static T Deserialize<T>(JObject serialized) => (T) eventDeserializers[typeof(T)](serialized);
                                                   
                                                       static EventDeserializer() {

                                                   """);

        Stack<IEventParent> eventClassesToGenerate = new(documentation.Events.Where(xEvent => xEvent.Children.Any()));

        while (eventClassesToGenerate.TryPop(out IEventParent? eventClassToGenerate)) {
            await WriteEventDataClass(eventClassToGenerate);
            await WriteEventDeserializer(eventClassToGenerate);
        }

        await eventDeserializerWriter.WriteAsync("    }\r\n\r\n}");

        async Task WriteEventDataClass(IEventParent eventParent) {
            await eventDataWriter.WriteAsync($"{GeneratedAttribute}\r\npublic class {GenerateEventDataClassName(eventParent)} {{\r\n\r\n");

            foreach (EventChild eventChild in eventParent.Children) {
                switch (eventChild) {
                    case ValueChild valueChild: {
                        string childType = valueChild.Type switch {
                            DataType.Integer => "int",
                            DataType.String  => "string",
                            DataType.Enum    => GetEnumName(valueChild.Name)
                        };
                        await eventDataWriter.WriteAsync(
                            $"    public {childType}{(valueChild.Required ? "" : "?")} {XapiEventKeyToCsIdentifier(valueChild.Name.Last())} {{ get; init; }}{(valueChild is { Required: true, Type: DataType.String } ? " = null!;" : "")}\r\n");
                        break;
                    }
                    case ListContainer listChild: {
                        string childType = GenerateEventDataClassName(listChild);
                        await eventDataWriter.WriteAsync($"    public IDictionary<int, {childType}> {listChild.Name.Last()} {{ get; init; }} = null!;\r\n");
                        eventClassesToGenerate.Push(listChild);
                        break;
                    }
                    case ObjectContainer objectContainer: {
                        string childType = GenerateEventDataClassName(objectContainer);
                        await eventDataWriter.WriteAsync(
                            $"    public {childType}{(objectContainer.Required ? "" : "?")} {objectContainer.Name.Last()} {{ get; init; }}{(objectContainer.Required ? " = null!;" : "")}\r\n");
                        eventClassesToGenerate.Push(objectContainer);
                        break;
                    }
                }
            }

            await eventDataWriter.WriteAsync("\r\n}\r\n\r\n");
        }

        async Task WriteEventDeserializer(IEventParent eventParent) {
            string typeName = GenerateEventDataClassName(eventParent);

            int serializedParameterCounter = 0;
            await eventDeserializerWriter.WriteAsync($$"""
                                                               eventDeserializers.Add(typeof({{typeName}}), json => new {{typeName}} {
                                                                   {{string.Join(",\r\n            ", eventParent.Children.Select(child => {
                                                                       string lastChildName = child.Name.Last();
                                                                       string childClass    = GenerateEventDataClassName(child);
                                                                       return XapiEventKeyToCsIdentifier(lastChildName) + " = " + child switch {
                                                                           IntChild { ImplicitAnonymousSingleton: true } => "json.Value<int>()",
                                                                           IntChild { Required: true } => $"json.Value<int>(\"{lastChildName}\")",
                                                                           IntChild { Required: false } => $"json.Value<int?>(\"{lastChildName}\")",
                                                                           StringChild { Required: true } => $"json.Value<string>(\"{lastChildName}\")!",
                                                                           StringChild { Required: false } => $"json.Value<string>(\"{lastChildName}\")",
                                                                           EnumChild { Required: true } => $"EnumSerializer.Deserialize<{GetEnumName(child.Name)}>(json.Value<string>(\"{lastChildName}\")!)",
                                                                           EnumChild { Required: false } => $"json.Value<string>(\"{lastChildName}\") is {{ }} serialized{++serializedParameterCounter} ? EnumSerializer.Deserialize<{GetEnumName(child.Name)}>(serialized{serializedParameterCounter}) : null",
                                                                           ListContainer => $"(IDictionary<int, {childClass}>?) json[\"{lastChildName}\"]?.Children().ToDictionary(innerObject => innerObject.Value<int>(\"id\"), innerObject => Deserialize<{childClass}>((JObject) innerObject)) ?? new ReadOnlyDictionary<int, {childClass}>(new Dictionary<int, {childClass}>(0))",
                                                                           ObjectContainer { Required: true } => $"Deserialize<{childClass}>((JObject) json[\"{lastChildName}\"]!)",
                                                                           ObjectContainer { Required: false } => $"json[\"{lastChildName}\"] is JObject serialized{++serializedParameterCounter} ? Deserialize<{childClass}>(serialized{serializedParameterCounter}) : null"
                                                                       };
                                                                   }))}}
                                                               });


                                                       """);
        }

        await eventWriter.WriteAsync($$"""
                                       {{FileHeader}}

                                       using {{Namespace}}.API.Data;
                                       using {{Namespace}}.API.Serialization;
                                       using System.CodeDom.Compiler;

                                       namespace {{Namespace}}.API;

                                       {{GeneratedAttribute}}
                                       internal class Events: {{string.Join(", ", interfaceTree.Keys)}} {
                                       
                                           private readonly IFeedbackSubscriber feedbackSubscriber;
                                       
                                           public Events(IFeedbackSubscriber feedbackSubscriber) {
                                               this.feedbackSubscriber = feedbackSubscriber;
                                           }
                                           
                                       
                                       """);

        foreach (DocXEvent xEvent in documentation.Events) {
            (string eventSignature, string? returnType) = GenerateEventSignature(xEvent, false);
            await eventWriter.WriteAsync($$"""
                                               /// <inheritdoc />
                                               {{eventSignature}} {
                                                   add => feedbackSubscriber.Subscribe(new[] { {{string.Join(", ", xEvent.Name.Select(s => $"\"{s}\""))}} }, value{{(returnType != null ? $", ValueSerializer.DeserializeEvent<{returnType}>" : "")}}).Wait(feedbackSubscriber.Timeout);
                                                   remove => feedbackSubscriber.Unsubscribe(value).Wait(feedbackSubscriber.Timeout);
                                               }


                                           """
            );

            _eventsGenerated++;

            _apiCommandsGenerated++;
        }

        foreach (KeyValuePair<string, ISet<INterfaceChild>> interfaceNode in interfaceTree) {
            foreach (Subinterface<DocXEvent> subinterface in interfaceNode.Value.OfType<Subinterface<DocXEvent>>()) {
                await eventWriter.WriteAsync($"    {subinterface.InterfaceName} {interfaceNode.Key}.{subinterface.GetterName} => this;\r\n");
            }
        }

        await eventWriter.WriteAsync("}");
    }

    private static (string signature, string? returnType) GenerateEventSignature(DocXEvent xEvent, bool isInterfaceEvent) {
        string? payloadType = xEvent.Children.Any() ? GenerateEventDataClassName(xEvent) : null;
        return ($"event FeedbackCallback{(payloadType != null ? $"<{payloadType}>" : "")} {(isInterfaceEvent ? "" : GetInterfaceName(xEvent) + '.')}{xEvent.NameWithoutBrackets.Last()}", payloadType);
    }

    private static string GenerateEventDataClassName(IPathNamed c) {
        return string.Join(null, c.Name.Skip(1).Append(c.Name[0][1..]).Append("Data"));
    }

    private static string XapiEventKeyToCsIdentifier(string key) => key.Replace('.', '_');

}