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

        // In 11.27, time zones started being documented in the PDF as a slash-separated list instead of a comma-separated list. This makes it consistent with all other valuespaces in the
        // documentation, but it's also ambiguous and unparseable because the values themselves also have slashes inside them, so you can't tell if a slash is part of the value name or a separator
        // between two values. This data is also hard to extract from the firmware because it's not in a text file, it only appears in giant binaries and ICU data files that can't be loaded by ICU libraries for Java or .NET, and would require a C++ program to dump (and I believe RoomOS post-processes the ICU list so even that might be insufficient).
        // Plan D is to check in the correct list to the bottom of this file, and diff its tokens (zone names split on slashes) to make it automatically break if the list changes, and easy to tell
        // what change to make to the list below.
        ISet<EnumValue>     allZones              = ((EnumParameter) FindCommand<DocXConfiguration>("xConfiguration Time Zone")!.Parameters[0]).PossibleValues;
        IEnumerable<string> actualTokens          = allZones.SelectMany(chunk => chunk.Name.Split('/', StringSplitOptions.RemoveEmptyEntries));
        int                 expectedTimeZoneIndex = -1;
        int                 expectedTokenIndex    = 0;
        string[]            expectedTimeZoneSplit = [];
        bool                allTimeZonesExpected  = true;
        foreach (string actualToken in actualTokens) {
            if (++expectedTokenIndex >= expectedTimeZoneSplit.Length) {
                expectedTokenIndex = 0;
                expectedTimeZoneIndex++;
                if (ExpectedTimeZones.Count <= expectedTokenIndex) {
                    Console.WriteLine($"More actual time zones than expected at {actualToken}");
                    allTimeZonesExpected = false;
                    break;
                }
                expectedTimeZoneSplit = ExpectedTimeZones[expectedTimeZoneIndex].Split('/');
            }
            string expected = expectedTimeZoneSplit[expectedTokenIndex];
            if (expected != actualToken) {
                Console.WriteLine($"Unexpected time zone ID token at entry {expectedTimeZoneIndex}: expected {ExpectedTimeZones[expectedTimeZoneIndex]}, actually {actualToken}");
                allTimeZonesExpected = false;
                break;
            }
        }
        if (allTimeZonesExpected) {
            allZones.Clear();
            foreach (string timeZone in ExpectedTimeZones) {
                allZones.Add(new EnumValue(timeZone));
            }
        } else {
            throw new Exception($"Time zone list needs to be updated in {nameof(Fixes)}.{nameof(ExpectedTimeZones)}");
        }
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

    private static readonly IList<string> ExpectedTimeZones = [
        "Africa/Abidjan",
        "Africa/Accra",
        "Africa/Addis_Ababa",
        "Africa/Algiers",
        "Africa/Asmara",
        "Africa/Asmera",
        "Africa/Bamako",
        "Africa/Bangui",
        "Africa/Banjul",
        "Africa/Bissau",
        "Africa/Blantyre",
        "Africa/Brazzaville",
        "Africa/Bujumbura",
        "Africa/Cairo",
        "Africa/Casablanca",
        "Africa/Ceuta",
        "Africa/Conakry",
        "Africa/Dakar",
        "Africa/Dar_es_Salaam",
        "Africa/Djibouti",
        "Africa/Douala",
        "Africa/El_Aaiun",
        "Africa/Freetown",
        "Africa/Gaborone",
        "Africa/Harare",
        "Africa/Johannesburg",
        "Africa/Juba",
        "Africa/Kampala",
        "Africa/Khartoum",
        "Africa/Kigali",
        "Africa/Kinshasa",
        "Africa/Lagos",
        "Africa/Libreville",
        "Africa/Lome",
        "Africa/Luanda",
        "Africa/Lubumbashi",
        "Africa/Lusaka",
        "Africa/Malabo",
        "Africa/Maputo",
        "Africa/Maseru",
        "Africa/Mbabane",
        "Africa/Mogadishu",
        "Africa/Monrovia",
        "Africa/Nairobi",
        "Africa/Ndjamena",
        "Africa/Niamey",
        "Africa/Nouakchott",
        "Africa/Ouagadougou",
        "Africa/Porto-Novo",
        "Africa/Sao_Tome",
        "Africa/Timbuktu",
        "Africa/Tripoli",
        "Africa/Tunis",
        "Africa/Windhoek",
        "America/Adak",
        "America/Anchorage",
        "America/Anguilla",
        "America/Antigua",
        "America/Araguaina",
        "America/Argentina/Buenos_Aires",
        "America/Argentina/Catamarca",
        "America/Argentina/ComodRivadavia",
        "America/Argentina/Cordoba",
        "America/Argentina/Jujuy",
        "America/Argentina/La_Rioja",
        "America/Argentina/Mendoza",
        "America/Argentina/Rio_Gallegos",
        "America/Argentina/Salta",
        "America/Argentina/San_Juan",
        "America/Argentina/San_Luis",
        "America/Argentina/Tucuman",
        "America/Argentina/Ushuaia",
        "America/Aruba",
        "America/Asuncion",
        "America/Atikokan",
        "America/Atka",
        "America/Bahia",
        "America/Bahia_Banderas",
        "America/Barbados",
        "America/Belem",
        "America/Belize",
        "America/Blanc-Sablon",
        "America/Boa_Vista",
        "America/Bogota",
        "America/Boise",
        "America/Buenos_Aires",
        "America/Cambridge_Bay",
        "America/Campo_Grande",
        "America/Cancun",
        "America/Caracas",
        "America/Catamarca",
        "America/Cayenne",
        "America/Cayman",
        "America/Chicago",
        "America/Chihuahua",
        "America/Ciudad_Juarez",
        "America/Coral_Harbour",
        "America/Cordoba",
        "America/Costa_Rica",
        // "America/Coyhaique", // added in 11.28, not 11.27
        "America/Creston",
        "America/Cuiaba",
        "America/Curacao",
        "America/Danmarkshavn",
        "America/Dawson",
        "America/Dawson_Creek",
        "America/Denver",
        "America/Detroit",
        "America/Dominica",
        "America/Edmonton",
        "America/Eirunepe",
        "America/El_Salvador",
        "America/Ensenada",
        "America/Fort_Nelson",
        "America/Fort_Wayne",
        "America/Fortaleza",
        "America/Glace_Bay",
        "America/Godthab",
        "America/Goose_Bay",
        "America/Grand_Turk",
        "America/Grenada",
        "America/Guadeloupe",
        "America/Guatemala",
        "America/Guayaquil",
        "America/Guyana",
        "America/Halifax",
        "America/Havana",
        "America/Hermosillo",
        "America/Indiana/Indianapolis",
        "America/Indiana/Knox",
        "America/Indiana/Marengo",
        "America/Indiana/Petersburg",
        "America/Indiana/Tell_City",
        "America/Indiana/Vevay",
        "America/Indiana/Vincennes",
        "America/Indiana/Winamac",
        "America/Indianapolis",
        "America/Inuvik",
        "America/Iqaluit",
        "America/Jamaica",
        "America/Jujuy",
        "America/Juneau",
        "America/Kentucky/Louisville",
        "America/Kentucky/Monticello",
        "America/Knox_IN",
        "America/Kralendijk",
        "America/La_Paz",
        "America/Lima",
        "America/Los_Angeles",
        "America/Louisville",
        "America/Lower_Princes",
        "America/Maceio",
        "America/Managua",
        "America/Manaus",
        "America/Marigot",
        "America/Martinique",
        "America/Matamoros",
        "America/Mazatlan",
        "America/Mendoza",
        "America/Menominee",
        "America/Merida",
        "America/Metlakatla",
        "America/Mexico_City",
        "America/Miquelon",
        "America/Moncton",
        "America/Monterrey",
        "America/Montevideo",
        "America/Montreal",
        "America/Montserrat",
        "America/Nassau",
        "America/New_York",
        "America/Nipigon",
        "America/Nome",
        "America/Noronha",
        "America/North_Dakota/Beulah",
        "America/North_Dakota/Center",
        "America/North_Dakota/New_Salem",
        "America/Nuuk",
        "America/Ojinaga",
        "America/Panama",
        "America/Pangnirtung",
        "America/Paramaribo",
        "America/Phoenix",
        "America/Port-au-Prince",
        "America/Port_of_Spain",
        "America/Porto_Acre",
        "America/Porto_Velho",
        "America/Puerto_Rico",
        "America/Punta_Arenas",
        "America/Rainy_River",
        "America/Rankin_Inlet",
        "America/Recife",
        "America/Regina",
        "America/Resolute",
        "America/Rio_Branco",
        "America/Rosario",
        "America/Santa_Isabel",
        "America/Santarem",
        "America/Santiago",
        "America/Santo_Domingo",
        "America/Sao_Paulo",
        "America/Scoresbysund",
        "America/Shiprock",
        "America/Sitka",
        "America/St_Barthelemy",
        "America/St_Johns",
        "America/St_Kitts",
        "America/St_Lucia",
        "America/St_Thomas",
        "America/St_Vincent",
        "America/Swift_Current",
        "America/Tegucigalpa",
        "America/Thule",
        "America/Thunder_Bay",
        "America/Tijuana",
        "America/Toronto",
        "America/Tortola",
        "America/Vancouver",
        "America/Virgin",
        "America/Whitehorse",
        "America/Winnipeg",
        "America/Yakutat",
        "America/Yellowknife",
        "Antarctica/Casey",
        "Antarctica/Davis",
        "Antarctica/DumontDUrville",
        "Antarctica/Macquarie",
        "Antarctica/Mawson",
        "Antarctica/McMurdo",
        "Antarctica/Palmer",
        "Antarctica/Rothera",
        "Antarctica/South_Pole",
        "Antarctica/Syowa",
        "Antarctica/Troll",
        "Antarctica/Vostok",
        "Arctic/Longyearbyen",
        "Asia/Aden",
        "Asia/Almaty",
        "Asia/Amman",
        "Asia/Anadyr",
        "Asia/Aqtau",
        "Asia/Aqtobe",
        "Asia/Ashgabat",
        "Asia/Ashkhabad",
        "Asia/Atyrau",
        "Asia/Baghdad",
        "Asia/Bahrain",
        "Asia/Baku",
        "Asia/Bangkok",
        "Asia/Barnaul",
        "Asia/Beirut",
        "Asia/Bishkek",
        "Asia/Brunei",
        "Asia/Calcutta",
        "Asia/Chita",
        "Asia/Choibalsan",
        "Asia/Chongqing",
        "Asia/Chungking",
        "Asia/Colombo",
        "Asia/Dacca",
        "Asia/Damascus",
        "Asia/Dhaka",
        "Asia/Dili",
        "Asia/Dubai",
        "Asia/Dushanbe",
        "Asia/Famagusta",
        "Asia/Gaza",
        "Asia/Harbin",
        "Asia/Hebron",
        "Asia/Ho_Chi_Minh",
        "Asia/Hong_Kong",
        "Asia/Hovd",
        "Asia/Irkutsk",
        "Asia/Istanbul",
        "Asia/Jakarta",
        "Asia/Jayapura",
        "Asia/Jerusalem",
        "Asia/Kabul",
        "Asia/Kamchatka",
        "Asia/Karachi",
        "Asia/Kashgar",
        "Asia/Kathmandu",
        "Asia/Katmandu",
        "Asia/Khandyga",
        "Asia/Kolkata",
        "Asia/Krasnoyarsk",
        "Asia/Kuala_Lumpur",
        "Asia/Kuching",
        "Asia/Kuwait",
        "Asia/Macao",
        "Asia/Macau",
        "Asia/Magadan",
        "Asia/Makassar",
        "Asia/Manila",
        "Asia/Muscat",
        "Asia/Nicosia",
        "Asia/Novokuznetsk",
        "Asia/Novosibirsk",
        "Asia/Omsk",
        "Asia/Oral",
        "Asia/Phnom_Penh",
        "Asia/Pontianak",
        "Asia/Pyongyang",
        "Asia/Qatar",
        "Asia/Qostanay",
        "Asia/Qyzylorda",
        "Asia/Rangoon",
        "Asia/Riyadh",
        "Asia/Saigon",
        "Asia/Sakhalin",
        "Asia/Samarkand",
        "Asia/Seoul",
        "Asia/Shanghai",
        "Asia/Singapore",
        "Asia/Srednekolymsk",
        "Asia/Taipei",
        "Asia/Tashkent",
        "Asia/Tbilisi",
        "Asia/Tehran",
        "Asia/Tel_Aviv",
        "Asia/Thimbu",
        "Asia/Thimphu",
        "Asia/Tokyo",
        "Asia/Tomsk",
        "Asia/Ujung_Pandang",
        "Asia/Ulaanbaatar",
        "Asia/Ulan_Bator",
        "Asia/Urumqi",
        "Asia/Ust-Nera",
        "Asia/Vientiane",
        "Asia/Vladivostok",
        "Asia/Yakutsk",
        "Asia/Yangon",
        "Asia/Yekaterinburg",
        "Asia/Yerevan",
        "Atlantic/Azores",
        "Atlantic/Bermuda",
        "Atlantic/Canary",
        "Atlantic/Cape_Verde",
        "Atlantic/Faeroe",
        "Atlantic/Faroe",
        "Atlantic/Jan_Mayen",
        "Atlantic/Madeira",
        "Atlantic/Reykjavik",
        "Atlantic/South_Georgia",
        "Atlantic/St_Helena",
        "Atlantic/Stanley",
        "Australia/ACT",
        "Australia/Adelaide",
        "Australia/Brisbane",
        "Australia/Broken_Hill",
        "Australia/Canberra",
        "Australia/Currie",
        "Australia/Darwin",
        "Australia/Eucla",
        "Australia/Hobart",
        "Australia/LHI",
        "Australia/Lindeman",
        "Australia/Lord_Howe",
        "Australia/Melbourne",
        "Australia/NSW",
        "Australia/North",
        "Australia/Perth",
        "Australia/Queensland",
        "Australia/South",
        "Australia/Sydney",
        "Australia/Tasmania",
        "Australia/Victoria",
        "Australia/West",
        "Australia/Yancowinna",
        "Brazil/Acre",
        "Brazil/DeNoronha",
        "Brazil/East",
        "Brazil/West",
        "CET",
        "CST6CDT",
        "Canada/Atlantic",
        "Canada/Central",
        "Canada/Eastern",
        "Canada/Mountain",
        "Canada/Newfoundland",
        "Canada/Pacific",
        "Canada/Saskatchewan",
        "Canada/Yukon",
        "Chile/Continental",
        "Chile/EasterIsland",
        "Cuba",
        "EET",
        "EST",
        "EST5EDT",
        "Egypt",
        "Eire",
        "Etc/GMT",
        "Etc/GMT+0",
        "Etc/GMT+1",
        "Etc/GMT+10",
        "Etc/GMT+11",
        "Etc/GMT+12",
        "Etc/GMT+2",
        "Etc/GMT+3",
        "Etc/GMT+4",
        "Etc/GMT+5",
        "Etc/GMT+6",
        "Etc/GMT+7",
        "Etc/GMT+8",
        "Etc/GMT+9",
        "Etc/GMT-0",
        "Etc/GMT-1",
        "Etc/GMT-10",
        "Etc/GMT-11",
        "Etc/GMT-12",
        "Etc/GMT-13",
        "Etc/GMT-14",
        "Etc/GMT-2",
        "Etc/GMT-3",
        "Etc/GMT-4",
        "Etc/GMT-5",
        "Etc/GMT-6",
        "Etc/GMT-7",
        "Etc/GMT-8",
        "Etc/GMT-9",
        "Etc/GMT0",
        "Etc/Greenwich",
        "Etc/UCT",
        "Etc/UTC",
        "Etc/Universal",
        "Etc/Zulu",
        "Europe/Amsterdam",
        "Europe/Andorra",
        "Europe/Astrakhan",
        "Europe/Athens",
        "Europe/Belfast",
        "Europe/Belgrade",
        "Europe/Berlin",
        "Europe/Bratislava",
        "Europe/Brussels",
        "Europe/Bucharest",
        "Europe/Budapest",
        "Europe/Busingen",
        "Europe/Chisinau",
        "Europe/Copenhagen",
        "Europe/Dublin",
        "Europe/Gibraltar",
        "Europe/Guernsey",
        "Europe/Helsinki",
        "Europe/Isle_of_Man",
        "Europe/Istanbul",
        "Europe/Jersey",
        "Europe/Kaliningrad",
        "Europe/Kiev",
        "Europe/Kirov",
        "Europe/Kyiv",
        "Europe/Lisbon",
        "Europe/Ljubljana",
        "Europe/London",
        "Europe/Luxembourg",
        "Europe/Madrid",
        "Europe/Malta",
        "Europe/Mariehamn",
        "Europe/Minsk",
        "Europe/Monaco",
        "Europe/Moscow",
        "Europe/Nicosia",
        "Europe/Oslo",
        "Europe/Paris",
        "Europe/Podgorica",
        "Europe/Prague",
        "Europe/Riga",
        "Europe/Rome",
        "Europe/Samara",
        "Europe/San_Marino",
        "Europe/Sarajevo",
        "Europe/Saratov",
        "Europe/Simferopol",
        "Europe/Skopje",
        "Europe/Sofia",
        "Europe/Stockholm",
        "Europe/Tallinn",
        "Europe/Tirane",
        "Europe/Tiraspol",
        "Europe/Ulyanovsk",
        "Europe/Uzhgorod",
        "Europe/Vaduz",
        "Europe/Vatican",
        "Europe/Vienna",
        "Europe/Vilnius",
        "Europe/Volgograd",
        "Europe/Warsaw",
        "Europe/Zagreb",
        "Europe/Zaporozhye",
        "Europe/Zurich",
        "GB",
        "GB-Eire",
        "GMT",
        "GMT+0",
        "GMT-0",
        "GMT0",
        "Greenwich",
        "HST",
        "Hongkong",
        "Iceland",
        "Indian/Antananarivo",
        "Indian/Chagos",
        "Indian/Christmas",
        "Indian/Cocos",
        "Indian/Comoro",
        "Indian/Kerguelen",
        "Indian/Mahe",
        "Indian/Maldives",
        "Indian/Mauritius",
        "Indian/Mayotte",
        "Indian/Reunion",
        "Iran",
        "Israel",
        "Jamaica",
        "Japan",
        "Kwajalein",
        "Libya",
        "MET",
        "MST",
        "MST7MDT",
        "Mexico/BajaNorte",
        "Mexico/BajaSur",
        "Mexico/General",
        "NZ",
        "NZ-CHAT",
        "Navajo",
        "PRC",
        "PST8PDT",
        "Pacific/Apia",
        "Pacific/Auckland",
        "Pacific/Bougainville",
        "Pacific/Chatham",
        "Pacific/Chuuk",
        "Pacific/Easter",
        "Pacific/Efate",
        "Pacific/Enderbury",
        "Pacific/Fakaofo",
        "Pacific/Fiji",
        "Pacific/Funafuti",
        "Pacific/Galapagos",
        "Pacific/Gambier",
        "Pacific/Guadalcanal",
        "Pacific/Guam",
        "Pacific/Honolulu",
        "Pacific/Johnston",
        "Pacific/Kanton",
        "Pacific/Kiritimati",
        "Pacific/Kosrae",
        "Pacific/Kwajalein",
        "Pacific/Majuro",
        "Pacific/Marquesas",
        "Pacific/Midway",
        "Pacific/Nauru",
        "Pacific/Niue",
        "Pacific/Norfolk",
        "Pacific/Noumea",
        "Pacific/Pago_Pago",
        "Pacific/Palau",
        "Pacific/Pitcairn",
        "Pacific/Pohnpei",
        "Pacific/Ponape",
        "Pacific/Port_Moresby",
        "Pacific/Rarotonga",
        "Pacific/Saipan",
        "Pacific/Samoa",
        "Pacific/Tahiti",
        "Pacific/Tarawa",
        "Pacific/Tongatapu",
        "Pacific/Truk",
        "Pacific/Wake",
        "Pacific/Wallis",
        "Pacific/Yap",
        "Poland",
        "Portugal",
        "ROC",
        "ROK",
        "Singapore",
        "Turkey",
        "UCT",
        "US/Alaska",
        "US/Aleutian",
        "US/Arizona",
        "US/Central",
        "US/East-Indiana",
        "US/Eastern",
        "US/Hawaii",
        "US/Indiana-Starke",
        "US/Michigan",
        "US/Mountain",
        "US/Pacific",
        "US/Samoa",
        "UTC",
        "Universal",
        "W-SU",
        "WET",
        "Zulu"
    ];

}