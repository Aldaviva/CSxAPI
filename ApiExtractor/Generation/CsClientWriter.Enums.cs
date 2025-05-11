using ApiExtractor.Extraction;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ApiExtractor.Generation;

public static partial class CsClientWriter {

    private static async Task WriteEnums(ExtractedDocumentation documentation) {
        await using StreamWriter enumsWriter          = OpenFileStream("Data\\Enums.cs");
        await using StreamWriter enumSerializerWriter = OpenFileStream("Serialization\\EnumSerializer.cs");

        await enumsWriter.WriteAsync($"""
                                      {FileHeader}

                                      using System.CodeDom.Compiler;

                                      namespace {Namespace}.API.Data;

                                      """);

        foreach (DocXConfiguration command in documentation.Commands.Concat(documentation.Configurations)) {
            await enumsWriter.WriteAsync(string.Join(null, command.Parameters.Where(parameter => parameter.Type == DataType.Enum).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = GetEnumName(command, parameter.Name);
                _enumsGenerated++;

                return $$"""

                         /// <summary>For use with <see cref="{{GetInterfaceName(command)}}.{{command.NameWithoutBrackets.Last()}}{{$"({string.Join(", ", command.Parameters.OrderByDescending(p => p.Required).Select(p => $"{p.Type switch {
                             DataType.Integer => "int",
                             DataType.String  => "string",
                             DataType.Enum    => GetEnumName(command, p.Name)
                         }}{(p.Required ? "" : "?")}"))})"}}" /></summary>
                         {{GeneratedAttribute}}
                         public enum {{enumTypeName}} {

                         {{string.Join(",\r\n\r\n", parameter.PossibleValues.Select(value => $"    /// <summary><para><c>{value.Name}</c>{(value.Description is not null ? ": " + value.Description.NewLinesToParagraphs(true) : "")}</para></summary>\r\n    {XapiEnumValueToCsIdentifier(command, parameter, value)}"))}}

                         }

                         """;
            })));
        }

        foreach (DocXStatus xStatus in documentation.Statuses.Where(status => status.ReturnValueSpace.Type == DataType.Enum)) {
            EnumValueSpace valueSpace   = (EnumValueSpace) xStatus.ReturnValueSpace;
            string         enumTypeName = GetEnumName(xStatus);

            await enumsWriter.WriteAsync($$"""

                                           /// <summary>For use with <see cref="{{GetInterfaceName(xStatus)}}.{{xStatus.NameWithoutBrackets.Last()}}{{$"({string.Join(", ", xStatus.ArrayIndexParameters.Select(_ => "int"))})"}}" /></summary>
                                           {{GeneratedAttribute}}
                                           public enum {{enumTypeName}} {

                                           {{string.Join(",\r\n\r\n", valueSpace.PossibleValues.Select(value => $"    /// <summary><para><c>{value.Name}</c>{(value.Description is not null ? ": " + value.Description.NewLinesToParagraphs(true) : "")}</para></summary>\r\n    {XapiEnumValueToCsIdentifier(xStatus, null, value)}"))}}

                                           }

                                           """);

            _enumsGenerated++;
        }

        foreach (DocXEvent xEvent in documentation.Events) {
            await WriteEventParentEnum(xEvent);
            _enumsGenerated++;
        }

        async Task WriteEventParentEnum(IEventParent eventParent) {
            foreach (EventChild eventChild in eventParent.Children) {
                if (eventChild is EnumChild enumChild) {
                    string enumTypeName = GetEnumName(enumChild.Name);

                    await enumsWriter.WriteAsync($$"""
                                                   {{GeneratedAttribute}}
                                                   public enum {{enumTypeName}} {

                                                   {{string.Join(",\r\n\r\n", enumChild.PossibleValues.Select(value => $"    /// <summary><para><c>{value.Name}</c></para></summary>\r\n    {XapiEnumValueToCsIdentifier(null, null, value)}"))}}

                                                   }


                                                   """);
                } else if (eventChild is IEventParent subParent) {
                    await WriteEventParentEnum(subParent);
                }
            }
        }

        await enumSerializerWriter.WriteAsync($$"""
                                                {{FileHeader}}

                                                using {{Namespace}}.API.Data;
                                                using System.CodeDom.Compiler;

                                                namespace {{Namespace}}.API.Serialization;

                                                {{GeneratedAttribute}}
                                                internal static class EnumSerializer {

                                                    private static readonly IDictionary<Type, (Func<Enum, string> serialize, Func<string, Enum> deserialize)> enumSerializers = new Dictionary<Type, (Func<Enum, string>, Func<string, Enum>)>();

                                                    public static T Deserialize<T>(string serialized) where T: Enum => (T) enumSerializers[typeof(T)].deserialize(serialized);

                                                    public static string Serialize<T>(T deserialized) where T: Enum => enumSerializers[typeof(T)].serialize(deserialized);

                                                    private static string DefaultSerializer(Enum o) => o.ToString();

                                                    static EnumSerializer() {

                                                """);

        foreach (DocXConfiguration command in documentation.Commands.Concat(documentation.Configurations)) {
            await enumSerializerWriter.WriteAsync(string.Join(null, command.Parameters.Where(parameter => parameter.Type == DataType.Enum).Cast<EnumParameter>().Select(parameter => {
                string enumTypeName = GetEnumName(command, parameter.Name);

                IEnumerable<string> serializerSwitchArms = parameter.PossibleValues
                    .Select(value => $"{enumTypeName}.{XapiEnumValueToCsIdentifier(command, parameter, value)} => \"{value.Name}\"")
                    .ToList();

                IEnumerable<string> deserializerSwitchArms =
                    parameter.PossibleValues.Select(value => $"\"{value.Name}\" => {enumTypeName}.{XapiEnumValueToCsIdentifier(command, parameter, value)}");

                return $$"""
                                 enumSerializers.Add(typeof({{enumTypeName}}), (
                                     serialize: deserialized => ({{enumTypeName}}) deserialized switch {
                                         {{string.Join(",\r\n                ", serializerSwitchArms.Append("_ => deserialized.ToString()"))}}
                                     },
                                     deserialize: serialized => serialized switch {
                                         {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                                     }));


                         """;

            })));
        }

        foreach (DocXStatus xStatus in documentation.Statuses.Where(status => status.ReturnValueSpace.Type == DataType.Enum)) {
            EnumValueSpace valueSpace   = (EnumValueSpace) xStatus.ReturnValueSpace;
            string         enumTypeName = GetEnumName(xStatus);

            IEnumerable<string> deserializerSwitchArms =
                valueSpace.PossibleValues.Select(value => $"\"{value.Name}\" => {enumTypeName}.{XapiEnumValueToCsIdentifier(xStatus, null, value)}");

            await enumSerializerWriter.WriteAsync($$"""
                                                            enumSerializers.Add(typeof({{enumTypeName}}), (
                                                                serialize: DefaultSerializer,
                                                                deserialize: serialized => serialized switch {
                                                                    {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                                                                }));


                                                    """);
        }

        foreach (DocXEvent xEvent in documentation.Events) {
            await WriteEventSerializer(xEvent);
        }

        async Task WriteEventSerializer(IEventParent eventParent) {
            foreach (EventChild eventChild in eventParent.Children) {
                if (eventChild is EnumChild enumChild) {
                    string enumTypeName = GetEnumName(enumChild.Name);
                    IEnumerable<string> deserializerSwitchArms =
                        enumChild.PossibleValues.Select(value => $"\"{value.Name}\" => {enumTypeName}.{XapiEnumValueToCsIdentifier(null, null, value)}");

                    await enumSerializerWriter.WriteAsync($$"""
                                                                    enumSerializers.Add(typeof({{enumTypeName}}), (
                                                                        serialize: DefaultSerializer,
                                                                        deserialize: serialized => serialized switch {
                                                                            {{string.Join(",\r\n                ", deserializerSwitchArms.Append($"_ => throw new ArgumentOutOfRangeException(nameof(serialized), serialized, $\"Unknown {enumTypeName} enum value {{serialized}}, known values are {{string.Join(\", \", Enum.GetValues<{enumTypeName}>())}}.\")"))}}
                                                                        }));


                                                            """);
                } else if (eventChild is IEventParent subParent) {
                    await WriteEventSerializer(subParent);
                }
            }
        }

        await enumSerializerWriter.WriteAsync("    }\r\n}");

        static string XapiEnumValueToCsIdentifier(AbstractCommand? command, EnumParameter? parameter, EnumValue value) {
            bool isTimeZone = (command?.Name.SequenceEqual(["xConfiguration", "Time", "Zone"]) ?? false) && parameter?.Name == "Zone";
            string name = isTimeZone || (parameter?.PossibleValues.Any(otherValue => otherValue.Name.StartsWith('-')) ?? false) ? value.Name
                : string.Join(null, value.Name.Split('-').Select(s => s.ToUpperFirstLetter()));
            name = Regex.Replace(name, "[^a-z0-9_]", match => match.Value switch {
                "."                                               => "_",
                "/"                                               => "_",      //"Ⳇ",
                "+" when isTimeZone                               => "_Plus_", //ႵᏐǂߙƚϯᵻᵼ
                "-" when isTimeZone && value.Name.Contains("GMT") => "_Minus_",
                "-" when isTimeZone                               => "_",
                "-" when match.Index == 0                         => "NEGATIVE_", // some xConfiguration Bluetooth LEAdvertisementOutputLevel values start with '-' (also don't split on '-' above)
                _                                                 => ""
            }, RegexOptions.IgnoreCase).ToUpperFirstLetter();
            return char.IsLetter(name[0]) ? name : "_" + name;
        }
    }

    private static string GetEnumName(AbstractCommand command, string? parameterName) {
        IEnumerable<string> segments = command.NameWithoutBrackets;
        if (parameterName != null) {
            segments = segments.Append(parameterName);
        }

        return string.Join(null, segments.DistinctConsecutive()).TrimStart('x');
    }

    private static string GetEnumName(DocXStatus command) {
        return GetEnumName(command, null);
    }

    private static string GetEnumName(DocXConfiguration command, string parameterName) {
        return GetEnumName((AbstractCommand) command, parameterName);
    }

    private static string GetEnumName(ICollection<string> eventParameterName) {
        return string.Join(null, eventParameterName.DistinctConsecutive()).TrimStart('x');
    }

}