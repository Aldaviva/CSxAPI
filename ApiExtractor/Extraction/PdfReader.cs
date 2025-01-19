using MoreLinq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Util;

namespace ApiExtractor.Extraction;

public static class PdfReader {

    private const int Dpi = 72;

    private static readonly ISet<string> XstatusDescriptionValueSpaceHeadingWords = new HashSet<string> { "Value", "space", "of", "the", "result", "returned:" };
    private static readonly IColor       ProductNameColor                         = new RGBColor(0.035, 0.376, 0.439);

    public static void Main() {
        // Console.WriteLine(string.Join("\n", guessEnumRange("Microphone.1/../Microphone.4/Line.1/Line.2/HDMI.2".Split('/')).Select(value => value.name)));
        // return;

        try {
            const string pdfFilename = @"c:\Users\Ben\Documents\Work\Blue Jeans\Cisco in-room controls for Verizon\api-reference-guide-roomos-111.pdf";

            Stopwatch              stopwatch = Stopwatch.StartNew();
            ExtractedDocumentation xapi      = new();
            ParsePdf(pdfFilename, xapi);
            Console.WriteLine($"Parsed PDF in {stopwatch.Elapsed:g}");

            /*IEnumerable<IGrouping<string, AbstractCommand>> duplicates = xapi.statuses
                .Concat<AbstractCommand>(xapi.configurations)
                .Concat(xapi.commands)
                .GroupBy(command => string.Join(' ', command.name.Skip(1)))
                .Where(grouping => grouping.Count() > 1);

            foreach (IGrouping<string, AbstractCommand> duplicate in duplicates) {
                foreach (AbstractCommand command in duplicate) {
                    Console.WriteLine(string.Join(' ', command.name));
                }

                Console.WriteLine();
            }*/

            // foreach (AbstractCommand command in ) {
            //     string name = ;
            //
            // }

            /*foreach (DocXConfiguration command in xapi.commands.Concat(xapi.configurations)) {
                Console.WriteLine($@"
{command.GetType().Name} {string.Join(' ', command.name)}
    Applies to: {string.Join(", ", command.appliesTo)}
    Requires user role: {string.Join(", ", command.requiresUserRole)}
    Body: {command.description}
    Parameters:");
                foreach (Parameter parameter in command.parameters) {
                    Console.WriteLine($@"
        {parameter.name}:
            {parameter.valueSpaceDescription}
            Type: {parameter.type}
            Default value: {parameter.defaultValue}
            Required: {parameter.required}
            Description: {parameter.description}");

                    switch (parameter) {
                        case StringParameter param:
                            Console.WriteLine($@"           Length: [{param.minimumLength}, {param.maximumLength}]");
                            break;
                        case IntParameter param:
                            if (param.ranges.Any()) {
                                Console.WriteLine($@"           Range: [{param.ranges.Min(range => range.minimum)}, {param.ranges.Max(range => range.maximum)}");
                            }

                            if (param.arrayIndexItemParameterPosition is not null) {
                                Console.WriteLine($@"           Position in name: {param.arrayIndexItemParameterPosition}");
                            }

                            break;
                        case EnumParameter param:
                            Console.WriteLine(@"           Possible values:");
                            foreach (EnumValue possibleValue in param.possibleValues) {
                                Console.WriteLine($@"               {possibleValue.name}: {possibleValue.description}");
                            }

                            break;
                    }
                }
            }

            foreach (DocXStatus status in xapi.statuses) {
                Console.WriteLine($@"
{status.GetType().Name} {string.Join(' ', status.name)}
    Applies to: {string.Join(", ", status.appliesTo)}
    Requires user role: {string.Join(", ", status.requiresUserRole)}
    Body: {status.description}
    Return value space:");

                switch (status.returnValueSpace) {
                    case IntValueSpace intValueSpace:
                        Console.Write("       Integer");
                        if (intValueSpace.ranges.Any()) {
                            Console.Write($" - Range: [{intValueSpace.ranges.Min(range => range.minimum)}, {intValueSpace.ranges.Max(range => range.maximum)}");
                        }

                        Console.WriteLine();
                        break;
                    case StringValueSpace stringValueSpace:
                        Console.WriteLine("       String");
                        break;
                    case EnumValueSpace enumValueSpace:
                        Console.WriteLine($"       Enum - {string.Join('/', enumValueSpace.possibleValues.Select(value => value.name))}");
                        break;
                }
            }*/

        } catch (ParsingException e) {
            Letter firstLetter = getFirstNonQuotationMarkLetter(e.Word.Letters);
            Console.WriteLine($"Failed to parse page {e.Page.Number}: {e.Message} (word: {e.Word.Text}, character style: {e.CharacterStyle}, parser state: {e.State}, position: " +
                $"({firstLetter.StartBaseLine.X / Dpi:N}\", {(e.Page.Height - firstLetter.StartBaseLine.Y) / Dpi:N}\"))");
            Console.WriteLine($"Font: {firstLetter.PointSize:N2}pt {firstLetter.FontName}");
        }
    }

    public static void ParsePdf(string filename, ExtractedDocumentation xapi) {
        Console.WriteLine($"Reading {Path.GetFileName(filename)}");
        using PdfDocument pdf = PdfDocument.Open(filename);
        try {
            parseSection(pdf, xapi.Configurations);
            Console.WriteLine($"Parsed {xapi.Configurations.Count:N0} xConfigurations from PDF");

            parseSection(pdf, xapi.Commands);
            Console.WriteLine($"Parsed {xapi.Commands.Count:N0} xCommands from PDF");

            parseSection(pdf, xapi.Statuses);
            Console.WriteLine($"Parsed {xapi.Statuses.Count:N0} xStatuses from PDF");
        } catch (ParsingException e) {
            Letter firstLetter = getFirstNonQuotationMarkLetter(e.Word.Letters);
            Console.WriteLine($"Failed to parse page {e.Page.Number}: {e.Message} (word: {e.Word.Text}, character style: {e.CharacterStyle}, parser state: {e.State}, position: " +
                $"({firstLetter.StartBaseLine.X / Dpi:N}\", {(e.Page.Height - firstLetter.StartBaseLine.Y) / Dpi:N}\"))");
            Console.WriteLine($"Font: {firstLetter.PointSize:N2}pt {firstLetter.FontName}");
            throw;
        }
    }

    private static void parseSection<T>(PdfDocument pdf, ICollection<T> xapiDestinationCollection) where T: AbstractCommand, new() {
        Range commandPages;

        if (typeof(T) == typeof(DocXConfiguration)) {
            commandPages = getPagesForSection(pdf, "xConfiguration commands", "xCommand commands");
        } else if (typeof(T) == typeof(DocXCommand)) {
            commandPages = getPagesForSection(pdf, "xCommand commands", "xStatus commands");
        } else if (typeof(T) == typeof(DocXStatus)) {
            commandPages = getPagesForSection(pdf, "xStatus commands", "Command overview");
        } else {
            throw new ArgumentOutOfRangeException(nameof(xapiDestinationCollection), typeof(T), "Unknown command type");
        }

        IEnumerable<(Word word, Page page)> wordsOnPages = getWordsOnPages(pdf, commandPages);

        ParserState state                = ParserState.Start;
        double?     previousWordBaseline = null;
        Word?       previousWord         = null;

        ISet<string> requiredParameters = new HashSet<string>();
        string?      parameterName;
        Parameter?   parameter;
        string?      parameterDescription;
        string?      partialProductName;
        int          parameterUsageIndex;
        EnumValue?   enumValue;
        string?      statusValueSpace;
        string       enumListDelimiter;
        string?      partialEnumValue;

        ResetMethodParsingState();

        void ResetMethodParsingState() {
            partialProductName  = null;
            parameterUsageIndex = 0;
            statusValueSpace    = null;
            requiredParameters.Clear();
            ResetParameterParsingState();
        }

        void ResetParameterParsingState() {
            parameter            = null;
            parameterName        = null;
            parameterDescription = null;
            enumValue            = null;
            enumListDelimiter    = "/";
            partialEnumValue     = null;
        }

        T command = new();

        foreach ((Word word, Page page) in wordsOnPages) {
            CharacterStyle characterStyle = GetCharacterStyle(word);

            // Console.WriteLine($"Parsing {word.Text}\t(character style = {characterStyle}, parser state = {state})");

            if (command is DocXStatus status && statusValueSpace is not null && characterStyle != CharacterStyle.ValuespaceOrDisclaimer) {
                status.ReturnValueSpace = statusValueSpace switch {
                    "Integer" => new IntValueSpace(),
                    "String"  => new StringValueSpace(),

                    _ when Regex.Match(statusValueSpace, @"^Integer \((?<min>-?\d+)\.\.(?<max>-?\d+)\)$") is { Success: true } match => new IntValueSpace
                        { Ranges = new List<IntRange> { new() { Minimum = int.Parse(match.Groups["min"].Value), Maximum = int.Parse(match.Groups["max"].Value) } } },
                    _ when Regex.Match(statusValueSpace, @"^(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match => new IntValueSpace
                        { Ranges = new List<IntRange> { new() { Minimum = int.Parse(match.Groups["min"].Value), Maximum = int.Parse(match.Groups["max"].Value) } } },

                    _ when statusValueSpace.Split(", ") is { Length: > 1 } split                                    => new EnumValueSpace { PossibleValues = ParseEnumValueSpacePossibleValues(split) },
                    _ when statusValueSpace.Split('/') is { Length: > 1 } split && !statusValueSpace.Contains("..") => new EnumValueSpace { PossibleValues = ParseEnumValueSpacePossibleValues(split) },
                    _ when statusValueSpace.Split('/') is { Length: > 1 } split && split.Contains("..")             => new EnumValueSpace { PossibleValues = GuessEnumRange(split) },
                    _ when Regex.Match(statusValueSpace, @"^Off/(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match => new IntValueSpace
                        { OptionalValue = "Off", Ranges = new List<IntRange> { new() { Minimum = int.Parse(match.Groups["min"].Value), Maximum = int.Parse(match.Groups["max"].Value) } } },

                    _ => new EnumValueSpace { PossibleValues = new HashSet<EnumValue> { new(statusValueSpace) } }
                    // _ => throw new ParsingException(word, state, characterStyle, page, "Could not parse xStatus returned value space " + statusValueSpace)
                };

                statusValueSpace = null;
            }

            // Omit duplicate methods
            // Sometimes Cisco documents the same exact method twice in the same PDF. Examples:
            //     xStatus Video Output Connector [n] Connected
            //     xStatus MediaChannels DirectShare [n] Channel [n] Netstat LastIntervalReceived
            if (state == ParserState.MethodNameHeading && characterStyle != CharacterStyle.MethodNameHeading &&
                xapiDestinationCollection.Any(cmd => cmd != command && cmd.Name.Skip(1).SequenceEqual(command.Name.Skip(1)))) {
                xapiDestinationCollection.Remove(command);
            }

            switch (characterStyle) {
                case CharacterStyle.MethodFamilyHeading:
                    if (state == ParserState.UsageDefaultValue && parameter is StringParameter && parameter.DefaultValue is not null) {
                        parameter.DefaultValue = parameter.DefaultValue.TrimEnd('"');
                    }

                    ResetMethodParsingState();

                    state = ParserState.Start;
                    //skip, not useful information, we'll get the method name from the METHOD_NAME_HEADING below
                    break;
                case CharacterStyle.MethodNameHeading:
                    // ReSharper disable once MergeIntoPattern - broken null checking if you apply this suggestion
                    if (state == ParserState.UsageDefaultValue && parameter is StringParameter && parameter.DefaultValue is not null) {
                        parameter.DefaultValue = parameter.DefaultValue.TrimEnd('"');
                    }

                    if (state != ParserState.MethodNameHeading) {
                        // finished previous method, moving to next method
                        command = new T();
                        xapiDestinationCollection.Add(command);
                        ResetMethodParsingState();
                    }

                    if (state == ParserState.MethodNameHeading && command is DocXStatus xStatus &&
                        Regex.Match(word.Text, @"\[(?<name>[a-z])\]", RegexOptions.IgnoreCase) is { Success: true } match2) {
                        IntParameter indexParameter = new() {
                            IndexOfParameterInName = command.Name.Count,
                            Required               = true,
                            Name                   = match2.Groups["name"].Value // can be duplicated even in one method
                        };
                        xStatus.ArrayIndexParameters.Add(indexParameter);
                        requiredParameters.Add(indexParameter.Name);
                    }

                    if (state == ParserState.MethodNameHeading && command is DocXConfiguration &&
                        Regex.Match(word.Text, @"(?<name>[a-z]+)\[(?<variableOrRange>\w+|\d+|-?\d+\.\.-?\d)\]", RegexOptions.IgnoreCase) is { Success: true } match4) {
                        // IntParameter indexParameter = new() {
                        //     indexOfParameterInName = command.name.Count + 1,
                        //     required               = true,
                        //     name                   = "n"
                        // };
                        requiredParameters.Add(match4.Groups["name"].Value);
                        command.Name.Add(match4.Groups["name"].Value);
                        command.Name.Add('[' + match4.Groups["variableOrRange"].Value + ']');
                        break;
                    }

                    state = ParserState.MethodNameHeading;
                    command.Name.Add(word.Text);
                    break;
                case CharacterStyle.ProductName:
                    switch (state) {
                        case ParserState.MethodNameHeading when word.Text == "Applies":
                            state = ParserState.AppliesTo;
                            break;
                        case ParserState.AppliesTo when word.Text == "to:":
                            state = ParserState.AppliesToProducts;
                            break;
                        case ParserState.AppliesTo or ParserState.AppliesToProducts:
                            state = ParserState.AppliesToProducts;
                            string productName = (partialProductName ?? string.Empty) + word.Text;
                            if (ParseProduct(productName) is { } product) {
                                command.AppliesTo.Add(product);
                                partialProductName = null;
                            } else if (word.Text is not ("All" or "products")) {
                                partialProductName = productName;
                                // throw new ParsingException(word, state, characterStyle, page, "product name was not 'All', 'products', or a recognized product name");
                            }

                            break;
                        case ParserState.Valuespace or ParserState.UsageParameterValueSpaceAppliesTo:
                            state = ParserState.UsageParameterValueSpaceAppliesTo;
                            if (parameter is not null) {
                                parameter.ValueSpaceDescription = AppendWord(parameter.ValueSpaceDescription, word, previousWordBaseline);
                            } else {
                                // This entire parameter, not just some of its values, only applies to specific products (xCommand Audio Volume Decrease Device:)
                                parameterDescription = AppendWord(parameterDescription, word, previousWordBaseline);
                                // throw new ParsingException(word, state, characterStyle, page, "no parameter to append value space description to");
                            }

                            break;
                        case ParserState.ValuespaceTermDefinition:
                            if (word.Text != "[" && word.Text != "]") {
                                // skip delimiters
                            } else if (ParseProduct(word.Text) is { } product2 && (parameter as IntParameter)?.Ranges.LastOrDefault() is { } lastRange) {
                                lastRange.AppliesTo.Add(product2);
                            }

                            break;
                        case ParserState.UsageDefaultValue when parameter is not null:
                            parameter.DefaultValue = AppendWord(parameter.DefaultValue, word, previousWordBaseline);
                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.UsageHeading:
                    if (word.Text == "USAGE:") {
                        state = ParserState.UsageExample;
                        if (command is DocXCommand xCommand && xCommand.Description.Contains("multiline")) {
                            xCommand.Parameters.Add(new StringParameter { Name = "body", Required = true });
                        }
                    } else {
                        throw new ParsingException(word, state, characterStyle, page, "unexpected word for character style");
                    }

                    break;
                case CharacterStyle.UsageExample:
                    if (state == ParserState.UsageExample) {
                        switch (command) {
                            case DocXConfiguration xConfiguration when Regex.Match(word.Text, @"^(?<prefix>\w*)\[(?<name>\w+)\]$") is { Success: true } match: {
                                string name = match.Groups["name"].Value;
                                if (int.TryParse(name, out int nameNumber)) {
                                    name = "n";
                                }
                                string? namePrefix = match.Groups["prefix"].Value.EmptyToNull();
                                if (namePrefix != null) {
                                    parameterUsageIndex++;
                                }
                                IntParameter indexParameter = new() {
                                    IndexOfParameterInName = parameterUsageIndex,
                                    Required               = true,
                                    Name                   = name,
                                    NamePrefix             = namePrefix
                                };
                                xConfiguration.Parameters.Add(indexParameter);
                                requiredParameters.Add(indexParameter.Name);
                                break;
                            }
                            case DocXConfiguration xConfiguration when Regex.Match(word.Text, @"^(?<prefix>\w*)\[(?<min>-?\d+)\.\.(?<max>-?\d+)\]$") is { Success: true } match: {
                                string? namePrefix = match.Groups["prefix"].Value.EmptyToNull();
                                if (namePrefix != null) {
                                    parameterUsageIndex++;
                                }
                                IntParameter channelParameter = new() {
                                    IndexOfParameterInName = parameterUsageIndex,
                                    Required               = true,
                                    Name                   = namePrefix ?? previousWord!.Text,
                                    Ranges                 = { new IntRange { Minimum = int.Parse(match.Groups["min"].Value), Maximum = int.Parse(match.Groups["max"].Value) } }
                                };
                                xConfiguration.Parameters.Add(channelParameter);
                                requiredParameters.Add(channelParameter.Name);

                                break;
                            }
                        }

                        parameterUsageIndex++;
                    } else if (state == ParserState.Description && command is DocXStatus) {
                        // the PDF authors forgot to put the "Value space of the result returned:" text in the description, so we parsed the entire value space definition as the description instead, and are now surprised by the font of the Example
                        // add the value space definition in Fixes.cs
                        state = ParserState.UsageExample;
                    } else {
                        throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.ParameterName:
                    switch (state) {
                        case ParserState.UsageExample:
                            if (!word.Text.EndsWith(']')) {
                                requiredParameters.Add(word.Text.Trim('"'));
                            }

                            // otherwise it's an optional parameter like [Channel: Channel]
                            parameterUsageIndex++;
                            break;
                        case ParserState.UsageParameterName or ParserState.ValuespaceTermDefinition when command is DocXConfiguration xConfiguration &&
                            xConfiguration.Parameters.FirstOrDefault(p => word.Text == p.Name + ':' && (p as IntParameter)?.IndexOfParameterInName is not null) is { } positionalParam:
                            parameter = positionalParam;
                            state     = ParserState.UsageParameterDescription;
                            break;
                        case ParserState.UsageParameterName or ParserState.ValuespaceTermDefinition or ParserState.UsageDefaultValue or ParserState.Valuespace or
                            ParserState.UsageParameterValueSpaceAppliesTo or ParserState.UsageParameterDescription:
                            // ReSharper disable once MergeIntoPattern - broken null checking if you apply this suggestion
                            if (parameter is StringParameter param && param.DefaultValue is not null) {
                                param.DefaultValue = param.DefaultValue.TrimEnd('"');
                            }

                            ResetParameterParsingState();
                            parameterName = word.Text.TrimEnd(':');
                            state         = ParserState.Valuespace;
                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.ValuespaceOrDisclaimer:
                    switch (state) {
                        case ParserState.AppliesToProducts or ParserState.RequiresUserRole:
                            // ignore the "Not available for the Webex Devices Cloud xAPI service on personal mode devices" disclaimer
                            state = ParserState.RequiresUserRole;
                            break;
                        case ParserState.Valuespace when command is DocXStatus:
                            statusValueSpace = AppendWord(statusValueSpace, word, previousWordBaseline);
                            break;
                        case ParserState.Valuespace or ParserState.UsageParameterValueSpaceAppliesTo:
                            Regex numericRangePattern = new(@"^(?<openparen>\()?(?<min>-?\d+)\.\.(?<max>-?\d+)(?<-openparen>\))?(?(openparen)(?!))$");
                            switch (word.Text) {
                                case "String" when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new StringParameter {
                                        Name        = parameterName,
                                        Required    = requiredParameters.Contains(parameterName),
                                        Description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty
                                    };

                                    (command as DocXConfiguration)?.Parameters.Add(parameter);
                                    parameterName = null;
                                    break;
                                case "Integer" when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new IntParameter {
                                        Name        = parameterName,
                                        Required    = requiredParameters.Contains(parameterName),
                                        Description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty
                                    };

                                    (command as DocXConfiguration)?.Parameters.Add(parameter);
                                    parameterName = null;
                                    break;
                                case { } valueSpace when parameter is null && numericRangePattern.Match(valueSpace) is { Success: true } match:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new IntParameter {
                                        Name        = parameterName,
                                        Required    = requiredParameters.Contains(parameterName),
                                        Description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty,
                                        Ranges = {
                                            new IntRange {
                                                Minimum = int.Parse(match.Groups["min"].Value),
                                                Maximum = int.Parse(match.Groups["max"].Value)
                                            }
                                        }
                                    };

                                    (command as DocXConfiguration)?.Parameters.Add(parameter);
                                    parameterName = null;

                                    break;
                                case { } enumList when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    if (enumList.EndsWith(',')) {
                                        enumListDelimiter = ",";
                                    }

                                    parameter = new EnumParameter {
                                        Name           = parameterName,
                                        Required       = requiredParameters.Contains(parameterName),
                                        Description    = parameterDescription is not null ? parameterDescription + '\n' : string.Empty,
                                        PossibleValues = ParseEnumValueSpacePossibleValues(enumList, enumListDelimiter)
                                    };

                                    (command as DocXConfiguration)?.Parameters.Add(parameter);
                                    parameterName = null;
                                    break;
                                case { } valueSpace when parameter is StringParameter param && numericRangePattern.Match(valueSpace) is { Success: true } match:
                                    // It's an integer encoded as a string!
                                    param.MinimumLength = match.Groups["min"].Length;
                                    param.MaximumLength = match.Groups["max"].Length;
                                    state               = ParserState.UsageParameterValueSpaceAppliesTo;
                                    break;
                                case { } valueSpace when parameter is StringParameter param && Regex.Match(valueSpace, @"^\((?<min>-?\d+),(?<max>-?\d+)\)$") is { Success: true } match:
                                    try {
                                        param.MinimumLength = int.Parse(match.Groups["min"].Value);
                                        param.MaximumLength = int.Parse(match.Groups["max"].Value);
                                    } catch (FormatException) {
                                        throw new ParsingException(word, state, characterStyle, page,
                                            $"Failed to parse uncommon comma-style string parameter length range \"{valueSpace}\" as ({match.Groups["min"].Value}, {match.Groups["max"].Value})");
                                    }

                                    state = ParserState.UsageParameterValueSpaceAppliesTo;
                                    break;
                                case { } valueSpace when parameter is StringParameter param && valueSpace.StartsWith('(') && valueSpace.EndsWith(','):
                                    try {
                                        param.MinimumLength = int.Parse(valueSpace[1..^1]);
                                    } catch (FormatException) {
                                        throw new ParsingException(word, state, characterStyle, page,
                                            $"Failed to parse string parameter length lower bound \"{valueSpace}\" as integer {valueSpace[1..^1]}");
                                    }

                                    break;
                                case { } valueSpace when parameter is StringParameter param && valueSpace.EndsWith(')'):
                                    try {
                                        param.MaximumLength = int.Parse(valueSpace[..^1]);
                                    } catch (FormatException) {
                                        throw new ParsingException(word, state, characterStyle, page,
                                            $"Failed to parse string parameter length upper bound \"{valueSpace}\" as integer {valueSpace[..^1]}");
                                    }

                                    state = ParserState.UsageParameterValueSpaceAppliesTo;
                                    break;
                                case { } valueSpace when parameter is IntParameter param && numericRangePattern.Match(valueSpace) is { Success: true } match:
                                    param.Ranges.Add(new IntRange { Minimum = int.Parse(match.Groups["min"].Value), Maximum = int.Parse(match.Groups["max"].Value) });
                                    // state = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO; //sometimes might be followed by more text
                                    break;
                                case { } enumList when parameter is EnumParameter param:
                                    //second line of wrapped enum values
                                    if (enumListDelimiter == "," && (enumList.EndsWith('_') || enumList.EndsWith('/'))) {
                                        partialEnumValue += enumList;
                                    } else {
                                        IEnumerable<EnumValue> additionalValues = ParseEnumValueSpacePossibleValues((partialEnumValue ?? "") + enumList, enumListDelimiter);
                                        foreach (EnumValue additionalValue in additionalValues) {
                                            param.PossibleValues.Add(additionalValue);
                                        }

                                        partialEnumValue = null;
                                    }

                                    break;
                                default:
                                    //ignore additional text after the value space that clarifies when it applies
                                    break;
                            }

                            if (parameter != null) {
                                parameter.ValueSpaceDescription = parameter.ValueSpaceDescription is null ? word.Text : AppendWord(parameter.ValueSpaceDescription, word, previousWordBaseline);
                            }

                            break;
                        case ParserState.UsageDefaultValue when parameter is not null:
                            parameter.DefaultValue = AppendWord(parameter.DefaultValue, word, previousWordBaseline);
                            break;
                        case ParserState.DescriptionValueSpaceHeading:

                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.ValuespaceTerm:
                    if ((parameter as IEnumValues ?? (command as DocXStatus)?.ReturnValueSpace as IEnumValues) is { } enumValues) {
                        enumValue = enumValues.PossibleValues.FirstOrDefault(value => value.Name == word.Text.TrimEnd(':'));
                        state     = ParserState.ValuespaceTermDefinition;
                    } else if (parameter is not null) {
                        parameter.Description = AppendWord(parameter.Description, word, previousWordBaseline);
                        state                 = ParserState.UsageParameterDescription;
                    } else {
                        // throw new ParsingException(word, state, characterStyle, page, $"found parameter enum term character style for non-enum parameter {parameter?.name ?? "(null param)"} of type {parameter?.type.ToString() ?? "null"}");
                    }

                    break;
                case CharacterStyle.Body:
                    switch (state) {
                        case ParserState.Start:
                            state = ParserState.VersionAndProductsCoveredPreamble;
                            break;
                        case ParserState.VersionAndProductsCoveredPreamble:
                            //skip
                            break;
                        case ParserState.MethodNameHeading when word.Text == "Applies":
                            state = ParserState.AppliesTo;
                            break;
                        case ParserState.AppliesTo when word.Text == "to:":
                            state = ParserState.AppliesToProducts;
                            break;
                        case ParserState.AppliesTo when word.Text == "Requires":
                            state = ParserState.RequiresUserRole;
                            break;
                        case ParserState.AppliesToProducts when word.Text == "Requires":
                            state = ParserState.RequiresUserRole;
                            break;
                        case ParserState.AppliesToProducts:
                            command.Description = AppendWord(command.Description, word, previousWordBaseline);
                            state               = ParserState.Description;
                            break;
                        case ParserState.RequiresUserRole when word.Text is "role:":
                            state = ParserState.RequiresUserRoleRoles;
                            break;
                        case ParserState.RequiresUserRoleRoles when !IsDifferentParagraph(word, previousWordBaseline):
                            foreach (string roleName in word.Text.TrimEnd(',').Split(',')) {
                                if (ParseEnum<UserRole>(roleName) is { } role) {
                                    command.RequiresUserRole.Add(role);
                                } else {
                                    throw new ParsingException(word, state, characterStyle, page, "role was not a recognized user role");
                                }
                            }

                            break;
                        case ParserState.UsageExample:
                            if (word.Text == "where") {
                                state = ParserState.UsageParameterName;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "unexpected word for state and character style");
                            }

                            break;
                        case ParserState.Valuespace or ParserState.ValuespaceDescription or ParserState.ValuespaceTermDefinition
                            when IsDifferentParagraph(word, previousWordBaseline) && word.Text == "Example:" && command is DocXStatus:
                            state = ParserState.UsageExample;
                            break;
                        case ParserState.UsageParameterDescription or ParserState.UsageParameterValueSpaceAppliesTo or ParserState.Valuespace or ParserState
                                .ValuespaceTermDefinition
                            when IsDifferentParagraph(word, previousWordBaseline) && command.GetType() == typeof(DocXConfiguration) && word.Text == "Default":

                            state     = ParserState.UsageDefaultValueHeading;
                            enumValue = null;
                            break;
                        case ParserState.UsageParameterDescription or ParserState.UsageParameterValueSpaceAppliesTo or ParserState.Valuespace or ParserState
                                .ValuespaceTermDefinition
                            when IsDifferentParagraph(word, previousWordBaseline) && command.GetType() == typeof(DocXConfiguration) && word.Text == "Range:" &&
                            parameter is IntParameter:

                            state = ParserState.Valuespace;
                            break;
                        case ParserState.UsageParameterDescription or ParserState.UsageParameterValueSpaceAppliesTo or ParserState.Valuespace:
                            if (parameter is not null) {
                                if (parameter is IntParameter intParameter && Regex.Match(word.Text, @"^(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match) {
                                    intParameter.Ranges.Add(new IntRange { Minimum = int.Parse(match.Groups["min"].Value), Maximum = int.Parse(match.Groups["max"].Value) });
                                    state = ParserState.ValuespaceTermDefinition;
                                } else if (parameter is IntParameter intParameter2 && int.TryParse(word.Text, out int match3)) {
                                    intParameter2.Ranges.Add(new IntRange { Minimum = match3, Maximum = match3 });
                                    state = ParserState.ValuespaceTermDefinition;
                                } else {
                                    parameter.Description = AppendWord(parameter.Description, word, previousWordBaseline);
                                    state                 = ParserState.UsageParameterDescription;
                                }
                            } else if (word.Text == ":" && state == ParserState.Valuespace) {
                                //the colon after the parameter name, usually it's part of the word with PARAMETER_NAME character style but sometimes it's tokenized as a separate word
                                //skip
                            } else if (command is DocXStatus status2) {
                                status2.ReturnValueSpace.Description = AppendWord(status2.ReturnValueSpace.Description, word, previousWordBaseline);
                                state                                = ParserState.ValuespaceDescription;
                            } else if (command is DocXConfiguration config && word.Text == "Unique" /*&& ((List<string>) [
                                           "xConfiguration Audio Input MicrophoneMode",
                                           "xConfiguration Audio Microphones BeamMix Inputs",
                                           "xConfiguration Audio Microphones NearTalkerSector Mode",
                                           "xConfiguration Audio Microphones PhantomPower"
                                       ]).Any(method => config.name.SequenceEqual(method.Split(' ')))*/) {
                                parameter = new IntParameter { Name = "n", Description = "DELETE ME" };
                                state     = ParserState.ValuespaceTermDefinition;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "no current parameter to append description to");
                            }

                            break;
                        case ParserState.ValuespaceTermDefinition:
                            if (enumValue is not null) {
                                enumValue.Description = AppendWord(enumValue.Description, word, previousWordBaseline);
                            } else if ((parameter as IntParameter)?.Ranges.LastOrDefault() is { } lastRange) {
                                lastRange.Description = AppendWord(lastRange.Description, word, previousWordBaseline);
                            }

                            break;
                        case ParserState.UsageDefaultValueHeading:
                            if (word.Text == "value:") {
                                state = ParserState.UsageDefaultValue;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "unexpected word for state and character style");
                            }

                            break;
                        case ParserState.UsageDefaultValue when parameter is not null:
                            switch (parameter) {
                                case IntParameter param:
                                    param.DefaultValue = word.Text;
                                    break;
                                case EnumParameter param:
                                    param.DefaultValue = word.Text;
                                    break;
                                case StringParameter param:
                                    param.DefaultValue = param.DefaultValue is null ? word.Text.TrimStart('"') : word.Text;
                                    break;
                            }

                            break;
                        case ParserState.RequiresUserRoleRoles or ParserState.Description:
                            if (word.Text == "Value" && IsDifferentParagraph(word, previousWordBaseline) && command is DocXStatus) {
                                state = ParserState.DescriptionValueSpaceHeading;
                            } else {
                                state               = ParserState.Description;
                                command.Description = AppendWord(command.Description, word, previousWordBaseline);
                            }

                            break;
                        case ParserState.DescriptionValueSpaceHeading when command is DocXStatus && !IsDifferentParagraph(word, previousWordBaseline):
                            if (word.Text == "returned:") {
                                state = ParserState.Valuespace;
                            } else if (!XstatusDescriptionValueSpaceHeadingWords.Contains(word.Text)) {
                                throw new ParsingException(word, state, characterStyle, page, "xStatus description contained a paragraph that started looking like it was the \"Value space of " +
                                    $"the result returned:\" heading, but it wasn't because it contained the word {word.Text}. Please implement a buffer for this situation to put this paragraph in " +
                                    "the description.");
                            } else {
                                //skip word, we don't care about the "Value space of the result returned:" text
                            }

                            break;
                        default:
                            break;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(characterStyle.ToString());
            }

            previousWordBaseline = word.Letters[0].StartBaseLine.Y;
            previousWord         = word;
        }
    }

    private static ISet<EnumValue> GuessEnumRange(IList<string> split) {
        split = new List<string>(split); // in case it was fixed-size from string[]
        IEnumerable<string> allValues = split;

        int     ellipsisIndex = split.IndexOf("..");
        string  lowerBound    = split[ellipsisIndex - 1];
        string  upperBound    = split[ellipsisIndex + 1];
        string? boundPrefix   = null, boundSuffix = null;
        int     boundIndex;

        for (boundIndex = 0; boundIndex < Math.Min(lowerBound.Length, upperBound.Length); boundIndex++) {
            if (lowerBound[boundIndex] != upperBound[boundIndex]) {
                boundPrefix = lowerBound[..boundIndex];

                for (boundIndex = 1; Math.Min(lowerBound.Length, upperBound.Length) - boundIndex > boundPrefix.Length - 1; boundIndex++) {
                    if (lowerBound[^boundIndex] != upperBound[^boundIndex]) {
                        boundSuffix = lowerBound[^(boundIndex - 1)..];
                        break;
                    }
                }

                break;
            }
        }

        if (boundPrefix != null && boundSuffix != null) {
            int lower = int.Parse(lowerBound[boundPrefix.Length..^boundSuffix.Length]);
            int upper = int.Parse(upperBound[boundPrefix.Length..^boundSuffix.Length]);

            split.RemoveAt(ellipsisIndex);
            IEnumerable<string> intermediateValues = Enumerable.Range(lower + 1, upper - lower - 1).Select(i => $"{boundPrefix}{i:N0}{boundSuffix}");
            allValues = split.Insert(intermediateValues, ellipsisIndex);
        }

        return allValues.Select(s => new EnumValue(s)).ToHashSet();
    }

    private static ISet<EnumValue> ParseEnumValueSpacePossibleValues(string enumList, string delimiter = "/") =>
        ParseEnumValueSpacePossibleValues(enumList.TrimEnd(')').Split(delimiter, StringSplitOptions.RemoveEmptyEntries));

    private static ISet<EnumValue> ParseEnumValueSpacePossibleValues(IEnumerable<string> enumList) => enumList.Select(value => new EnumValue(value)).ToHashSet();

    private static string AppendWord(string? head, Word tail, double? previousWordBaseline) {
        double baselineDifference = GetBaselineDifference(tail, previousWordBaseline);
        bool   isDifferentLine    = baselineDifference > 3;
        string wordSeparator;

        if (string.IsNullOrWhiteSpace(head)) {
            wordSeparator = string.Empty;
        } else if (isDifferentLine && (head.EndsWith('-') || head.EndsWith('/'))) {
            head          = head.TrimEnd('-', '/');
            wordSeparator = string.Empty;
        } else if (IsDifferentParagraph(baselineDifference)) {
            wordSeparator = "\n";
        } else {
            wordSeparator = " ";
        }

        return (head ?? string.Empty) + wordSeparator + tail.Text;
    }

    private static double GetBaselineDifference(Word tail, double? previousWordBaseline) {
        return previousWordBaseline is not null ? Math.Abs(tail.Letters[0].StartBaseLine.Y - (double) previousWordBaseline) : 0;
    }

    private static bool IsDifferentParagraph(double baselineDifference) => baselineDifference > 10;
    private static bool IsDifferentParagraph(Word word, double? previousWordBaseline) => IsDifferentParagraph(GetBaselineDifference(word, previousWordBaseline));

    private static IEnumerable<(Word word, Page page)> getWordsOnPages(PdfDocument pdf, Range pageIndices) {
        int[] pageNumbers = Enumerable.Range(0, pdf.NumberOfPages).ToArray()[pageIndices];
        foreach (int pageNumber in pageNumbers) {
            foreach (bool readLeftSide in new[] { true, false }) {
                Page           page = pdf.GetPage(pageNumber);
                IWordExtractor wordExtractor;
                wordExtractor = FixedDefaultWordExtractor.Instance;
                IReadOnlyList<Letter> lettersWithUnfuckedQuotationMarks = page.Letters
                    .Where(letter => isTextOnHalfOfPage(letter, page, readLeftSide))
                    .Select(letter => new Letter(
                        letter.Value,
                        letter.GlyphRectangle,
                        // when Cisco made the monospaced quotation marks bigger, loss of floating-point precision lowered the baseline enough to mess up the letter order relied upon by the DefaultWordExtractor
                        new PdfPoint(letter.StartBaseLine.X, Math.Round(letter.StartBaseLine.Y, 3)),
                        new PdfPoint(letter.EndBaseLine.X, Math.Round(letter.EndBaseLine.Y, 3)),
                        letter.Width,
                        letter.FontSize,
                        letter.Font,
                        letter.RenderingMode,
                        letter.StrokeColor,
                        letter.FillColor,
                        letter is { Value: "\"", PointSize: 9.6, FontName: var fontName } && fontName.EndsWith("CourierNewPSMT") ? 8.8 : letter.PointSize,
                        letter.TextSequence)
                    ).ToImmutableList();
                IEnumerable<Word> words = wordExtractor.GetWords(lettersWithUnfuckedQuotationMarks);

                foreach (Word word in words) {
                    yield return (word, page);
                }
            }
        }
    }

    private static Range getPagesForSection(PdfDocument doc, string previousBookmarkName, string nextBookmarkName) {
        doc.TryGetBookmarks(out Bookmarks bookmarks);
        IEnumerable<DocumentBookmarkNode> bookmarksInSection = bookmarks.GetNodes()
            .Where(node => node.Level == 0)
            .OfType<DocumentBookmarkNode>()
            .OrderBy(node => node.PageNumber).SkipUntil(node => node.Title == previousBookmarkName).TakeUntil(node => node.Title == nextBookmarkName)
            .ToList();
        return bookmarksInSection.First().PageNumber..bookmarksInSection.Last().PageNumber;
    }

    internal static CharacterStyle GetCharacterStyle(Word word) {
        return getFirstNonQuotationMarkLetter(word.Letters) switch {
            { PointSize: 16.0 }                                                                                                  => CharacterStyle.MethodFamilyHeading,
            { PointSize: 10.0 }                                                                                                  => CharacterStyle.MethodNameHeading,
            { FontName: var font, Color: var color } when font.EndsWith("CiscoSansTT-Oblique") && color.Equals(ProductNameColor) => CharacterStyle.ProductName,
            { PointSize: 8.0, FontName: var font } when font.EndsWith("CiscoSansTT")                                             => CharacterStyle.UsageHeading,
            { PointSize: 8.8 or 9.6, FontName: var font } when font.EndsWith("CourierNewPSMT")                                   => CharacterStyle.UsageExample,
            { PointSize: 8.8, FontName: var font } when font.EndsWith("CourierNewPS-ItalicMT")                                   => CharacterStyle.ParameterName,
            { PointSize: 8.0, FontName: var font } when font.EndsWith("CiscoSansTTLight-Oblique")                                => CharacterStyle.ValuespaceOrDisclaimer,
            { PointSize: 8.0, FontName: var font } when font.EndsWith("CiscoSansTT-Oblique")                                     => CharacterStyle.ValuespaceTerm,
            _                                                                                                                    => CharacterStyle.Body
        };
    }

    private static T? ParseEnum<T>(string text) where T: struct, Enum => Enum.TryParse(text, true, out T result) ? result : null;

    private static Product? ParseProduct(string text) => ParseEnum<Product>(text.Replace('/', '_'));

    internal static Letter getFirstNonQuotationMarkLetter(IReadOnlyList<Letter> letters) {
        return letters.SkipWhile(letter => letter.Value == "\"").FirstOrDefault(letters[0]);
    }

    internal static bool isTextOnHalfOfPage(Word word, Page page, bool isOnLeft) => isTextOnHalfOfPage(word.Letters[0], page, isOnLeft);

    internal static bool isTextOnHalfOfPage(Letter letter, Page page, bool isOnLeft) {

        const int pointsPerInch = 72;

        const double leftMargin   = 5.0 / 8.0 * pointsPerInch;
        const double topMargin    = 1.0 * pointsPerInch;
        const double bottomMargin = pointsPerInch * 0.5;

        return letter.Location.Y > bottomMargin
            && letter.Location.Y < page.Height - topMargin
            && (letter.Location.X < (page.Width - leftMargin) / 2 + leftMargin) ^ !isOnLeft
            && letter.Location.X > leftMargin;
    }

}

internal class ParsingException: Exception {

    public Word Word { get; }
    public ParserState State { get; }
    public CharacterStyle CharacterStyle { get; }
    public Page Page { get; }

    public ParsingException(Word word, ParserState state, CharacterStyle characterStyle, Page page, string message): base(message) {
        Word           = word;
        State          = state;
        CharacterStyle = characterStyle;
        Page           = page;
    }

}

internal enum CharacterStyle {

    MethodFamilyHeading,
    MethodNameHeading,
    ProductName,
    UsageHeading,
    UsageExample,
    ParameterName,
    ValuespaceOrDisclaimer,
    ValuespaceTerm,
    Body

}

internal enum ParserState {

    Start,
    VersionAndProductsCoveredPreamble,
    MethodNameHeading,
    AppliesTo,
    AppliesToProducts,
    RequiresUserRole,
    RequiresUserRoleRoles,
    Description,
    UsageExample,                      //the xCommand or similar example invocation, directly below the "USAGE:" heading
    UsageParameterName,                //the name of the parameter underneath the "where"
    UsageParameterDescription,         //the regular description of the parameter, underneath the valuespace summary and above the value space descriptions
    Valuespace,                        //the italic text for each parameter that says whether it's an Integer, String, or the slash-separated enum values
    ValuespaceTermDefinition,          //the text to the right of the bold enum value name that describes the parameter value
    UsageParameterValueSpaceAppliesTo, //says which products a value space applies to, as italic text to the right of the valuespace
    UsageDefaultValueHeading,          //the xConfiguration "Default value:" heading
    UsageDefaultValue,                 //the xConfiguration default valuespace
    DescriptionValueSpaceHeading,      //the xStatus "Value space of the result returned:" heading
    ValuespaceDescription              //the xStatus explanation of the valuespace below the italic text, which can include bold terms and definitions or just free text

}