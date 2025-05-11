using System;
using System.Collections.Generic;
using System.Linq;

namespace ApiExtractor.Extraction;

public class Fixes(ExtractedDocumentation documentation) {

    /*
     * These fixes are applied after parsing the PDF but before generating the client code.
     */
    public void Fix() {
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
        foreach (DocXStatus naStatus in documentation.Statuses.Where(status => status.Description == "Not applicable in this release.")) {
            naStatus.ReturnValueSpace = new StringValueSpace();
        }

        // Multiple path parameters with the same name
        // xStatus MediaChannels Call [n] Channel [n] Audio Mute
        foreach (DocXStatus multiParameterStatus in documentation.Statuses.Where(status => status.ArrayIndexParameters.Count >= 2)) {
            int nIndex = 1;
            foreach (IntParameter parameter in multiParameterStatus.ArrayIndexParameters.Where(parameter => parameter.Name == "n")) {
                if (nIndex > 1) {
                    parameter.Name += nIndex;
                }

                nIndex++;
            }
        }

        // Event body is a number, not an object
        // xEvent Standby SecondsUntilStandby
        // xEvent RoomReset SecondsUntilReset
        foreach (DocXEvent xEvent in documentation.Events.Where(xEvent => xEvent.Children is [{ Name: [.., "NameNotUsed"] }])) {
            xEvent.Children.Single().Name[^1] = "Value";
        }

        // Zoom commands and configuration
        foreach (DocXConfiguration xConfiguration in documentation.Configurations.Where(xConfiguration => xConfiguration.Name[1] == "Zoom").ToList()) {
            documentation.Configurations.Remove(xConfiguration);
        }
        foreach (DocXCommand xCommand in documentation.Commands.Where(xCommand => xCommand.Name[1] == "Zoom").ToList()) {
            documentation.Commands.Remove(xCommand);
        }

        // This status has a malformed description in the PDF – it is missing the "Value space of the result returned:" string
        // Therefore, we cannot parse the value space normally.
        // To work around this, manually add the value space definition here.
        FindCommand<DocXStatus>("xStatus Video Output Connector [n] ConnectedDevice SupportedFormat Res_1920_1200_50")!.ReturnValueSpace =
            new EnumValueSpace { PossibleValues = new HashSet<EnumValue> { new("False") { Description = "The format is not supported." }, new("True") { Description = "The format is supported." } } };

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
        foreach (DocXConfiguration config in documentation.Configurations) {
            if (config.Parameters.FirstOrDefault(parameter => parameter.Description == "DELETE ME") is { } toDelete) {
                config.Parameters.Remove(toDelete);
            }
        }

        // xCommand Bookings List is mistakenly documented to have two duplicate "offset" parameters
        DocXCommand bookingsList = FindCommand<DocXCommand>("xCommand Bookings List")!;
        foreach (Parameter extraParam in bookingsList.Parameters.Where(p => p.Name == "Offset").Skip(1).ToList()) {
            bookingsList.Parameters.Remove(extraParam);
        }

        // Many Ethernet audio configurations are mistakenly documented twice, once for RoomBar and once for everything else.
        // The only thing that differs is the range of the positional Channel parameter, but this should not be documented as different methods.
        // It should be one method with an [n] parameter, which can have different ranges depending on the product, like xConfiguration Audio Output Line [n] Level does (1..6 for CodecPro, 1..1 for Room70).
        foreach (DocXConfiguration duplicateConfig in (IEnumerable<DocXConfiguration>) documentation.Configurations.Where(cfg =>
                     cfg.Name is ["xConfiguration", "Audio", "Input", "Ethernet", _, "Channel", _, "Level" or "Mode" or "Pan" or "Zone"] && cfg.AppliesTo.SetEquals([Product.RoomBar])).ToList()) {
            documentation.Configurations.Remove(duplicateConfig);
        }

        // The last word in "xConfiguration Video Output Connector [n] HDCPForce1_4" is the number 4, so the auto-generated parameter name for setting this configuration is illegal because it starts with a number.
        FindCommand<DocXConfiguration>("xConfiguration Video Output Connector [n] HDCPForce1_4")!.Parameters[1].Name = "hdcpForce1_4";

        DocXConfiguration bleLevel = FindCommand<DocXConfiguration>("xConfiguration Bluetooth LEAdvertisementOutputLevel")!;

    }

    private void setConfigurationValueSpace(string path, params string[] values) {
        if (FindCommand<DocXConfiguration>(path) is { } configuration && configuration.Parameters.LastOrDefault() is EnumParameter parameter) {
            parameter.PossibleValues.Clear();
            foreach (string newValue in values) {
                parameter.PossibleValues.Add(new EnumValue(newValue));
            }
        } else {
            Console.WriteLine($"Fixes: could not find {path}, so not applying this fix");
        }
    }

    private T? FindCommand<T>(string path) where T: IPathNamed {
        string[] nameQuery = path.Split(' ');
        ICollection<T> collection = typeof(T) switch {
            var t when t == typeof(DocXCommand)       => (ICollection<T>) documentation.Commands,
            var t when t == typeof(DocXConfiguration) => (ICollection<T>) documentation.Configurations,
            var t when t == typeof(DocXStatus)        => (ICollection<T>) documentation.Statuses,
            var t when t == typeof(DocXEvent)         => (ICollection<T>) documentation.Events
        };

        return collection.FirstOrDefault(command => command.Name.SequenceEqual(nameQuery));
    }

}