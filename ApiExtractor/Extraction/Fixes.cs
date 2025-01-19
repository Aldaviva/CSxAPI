using System;
using System.Collections.Generic;
using System.Linq;

namespace ApiExtractor.Extraction;

public class Fixes(ExtractedDocumentation documentation) {

    public void fix() {
        // Value space descriptions that depend heavily on the endpoint model are hard to parse, so hard-code the value spaces
        setConfigurationValueSpace("xConfiguration Video Input AirPlay Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] CameraControl Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] CEC Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] HDCP Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] InputSourceType", "PC", "camera", "document_camera", "mediaplayer", "whiteboard", "other");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] PreferredResolution", "1920_1080_60", "2560_1440_60", "3840_2160_30", "3840_2160_60");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] Visibility", "Always", "IfSignal", "Never");
        setConfigurationValueSpace("xConfiguration Video Output Connector [n] CEC Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Output Connector [n] HDCPPolicy", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Output Connector [n] Resolution", "Auto", "1920_1080_50", "1920_1080_60", "1920_1200_50", "1920_1200_60", "2560_1440_60", "3840_2160_30",
            "3840_2160_60");

        // Undocumented enum, so deserialize as a string
        // xStatus Conference Call [n] Capabilities FarendMessage Mode
        // xStatus Conference Call [n] Capabilities IxChannel Status
        foreach (DocXStatus naStatus in documentation.statuses.Where(status => status.description == "Not applicable in this release.")) {
            naStatus.returnValueSpace = new StringValueSpace();
        }

        // Multiple path parameters with the same name
        // xStatus MediaChannels Call [n] Channel [n] Audio Mute
        foreach (DocXStatus multiParameterStatus in documentation.statuses.Where(status => status.arrayIndexParameters.Count >= 2)) {
            int nIndex = 1;
            foreach (IntParameter parameter in multiParameterStatus.arrayIndexParameters.Where(parameter => parameter.name == "n")) {
                if (nIndex > 1) {
                    parameter.name += nIndex;
                }

                nIndex++;
            }
        }

        // Event body is a number, not an object
        // xEvent Standby SecondsUntilStandby
        // xEvent RoomReset SecondsUntilReset
        foreach (DocXEvent xEvent in documentation.events.Where(xEvent => xEvent.children is [{ name: [.., "NameNotUsed"] }])) {
            xEvent.children.Single().name[^1] = "Value";
        }

        // Zoom commands and configuration
        foreach (DocXConfiguration xConfiguration in documentation.configurations.Where(xConfiguration => xConfiguration.name[1] == "Zoom").ToList()) {
            documentation.configurations.Remove(xConfiguration);
        }
        foreach (DocXCommand xCommand in documentation.commands.Where(xCommand => xCommand.name[1] == "Zoom").ToList()) {
            documentation.commands.Remove(xCommand);
        }

        // This status has a malformed description in the PDF – it is missing the "Value space of the result returned:" string
        // Therefore, we cannot parse the value space normally.
        // To work around this, manually add the value space definition here.
        documentation.statuses.First(status => status.name.SequenceEqual("xStatus Video Output Connector [n] ConnectedDevice SupportedFormat Res_1920_1200_50".Split(' '))).returnValueSpace =
            new EnumValueSpace { possibleValues = new HashSet<EnumValue> { new("False") { description = "The format is not supported." }, new("True") { description = "The format is supported." } } };

        // Starting in RoomOS 11.14, the codec can control external serial devices using serial-USB adapters.
        // The configurations to set the baud rate and other settings are parameterized with a port number, but it is always 1, which is not a valid C# parameter name.
        // To work around this, rename the parameter from 1 to N, like a normal configuration.
        /*foreach (DocXConfiguration xConfiguration in documentation.configurations) {
            IEnumerable<Parameter> paramWithNumericName =
                xConfiguration.parameters.Where(param => param is IntParameter { indexOfParameterInName: not null } intParam && int.TryParse(intParam.name, out int _));
            foreach (Parameter param in paramWithNumericName) {
                param.name = "n";
            }
        }*/

        // Some configurations are mistakenly documented to take a [n] parameter, even though they don't, such as xConfiguration Audio Input MicrophoneMode
        foreach (DocXConfiguration config in documentation.configurations) {
            if (config.parameters.FirstOrDefault(parameter => parameter.description == "DELETE ME") is { } toDelete) {
                config.parameters.Remove(toDelete);
            }
        }

        // xCommand Bookings List is mistakenly documented to have two duplicate "offset" parameters
        DocXCommand bookingsList = documentation.commands.First(command => command.name.SequenceEqual("xCommand Bookings List".Split(' ')));
        foreach (Parameter extraParam in bookingsList.parameters.Where(p => p.name == "Offset").Skip(1).ToList()) {
            bookingsList.parameters.Remove(extraParam);
        }

        // Many Ethernet audio configurations are mistakenly documented twice, once for RoomBar and once for everything else.
        // The only thing that differs is the range of the positional Channel parameter, but this should not be documented as different methods.
        // It should be one method with an [n] parameter, which can have different ranges depending on the product, like xConfiguration Audio Output Line [n] Level does (1..6 for CodecPro, 1..1 for Room70).
        foreach (DocXConfiguration duplicateConfig in (IEnumerable<DocXConfiguration>) documentation.configurations.Where(cfg =>
                     cfg.name is ["xConfiguration", "Audio", "Input", "Ethernet", _, "Channel", _, "Level" or "Mode" or "Pan" or "Zone"] && cfg.appliesTo.SetEquals([Product.RoomBar])).ToList()) {
            documentation.configurations.Remove(duplicateConfig);
        }

    }

    private void setConfigurationValueSpace(string path, params string[] values) {
        if (findCommand<DocXConfiguration>(path) is { } configuration && configuration.parameters.LastOrDefault() is EnumParameter parameter) {
            parameter.possibleValues.Clear();
            foreach (string newValue in values) {
                parameter.possibleValues.Add(new EnumValue(newValue));
            }
        } else {
            Console.WriteLine($"Fixes: could not find {path}, so not applying this fix");
        }
    }

    private T? findCommand<T>(string path) where T: IPathNamed {
        IList<string> nameQuery = path.Split(' ');
        IList<T> collection = typeof(T) switch {
            var t when t == typeof(DocXCommand)       => (List<T>) documentation.commands,
            var t when t == typeof(DocXConfiguration) => (List<T>) documentation.configurations,
            var t when t == typeof(DocXStatus)        => (List<T>) documentation.statuses,
            var t when t == typeof(DocXEvent)         => (List<T>) documentation.events
            // _                                         => null
        };

        return collection.FirstOrDefault(command => command.name.SequenceEqual(nameQuery));
    }

}