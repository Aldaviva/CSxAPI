using System.Text.RegularExpressions;
using CSxAPI.API.Data;

namespace CSxAPI;

public static class Extensions {

    private static readonly Regex Underscore = new(@"_");

    private static readonly IDictionary<ConfigurationTimeZone, TimeZoneInfo> TimeZoneInfoCache = new Dictionary<ConfigurationTimeZone, TimeZoneInfo>();

    public static IDictionary<TKey, TValue> Compact<TKey, TValue>(this IDictionary<TKey, TValue?> dictionary) where TKey: notnull where TValue: class {
        return dictionary.Where(entry => entry.Value != null)
            .ToDictionary(entry => entry.Key, entry => entry.Value!);
    }

    /// <exception cref="TimeZoneNotFoundException">if the Cisco time zone does not map to a .NET time zone</exception>
    public static TimeZoneInfo ToTimeZoneInfo(this ConfigurationTimeZone xapiTimeZone) {
        if (!TimeZoneInfoCache.TryGetValue(xapiTimeZone, out TimeZoneInfo? timeZone)) {
            timeZone = xapiTimeZone switch {
                ConfigurationTimeZone.Antarctica_Troll => TimeZoneInfo.CreateCustomTimeZone("Antarctica/Troll", TimeSpan.Zero, "(UTC+00:00) Troll", "Greenwich Mean Time",
                    "Central European Summer Time",
                    new[] {
                        TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(2),
                            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 1, 0, 0), 3, 3, DayOfWeek.Sunday),
                            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday))
                    }),
                ConfigurationTimeZone.America_Ciudad_Juarez => TimeZoneInfo.CreateCustomTimeZone("America/Ciudad_Juarez", TimeSpan.FromHours(-7), "(UTC-07:00) Ciudad Juárez", "Mountain Standard Time",
                    "Mountain Daylight Time", new[] {
                        TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday))
                    }),
                _ => TimeZoneInfo.FindSystemTimeZoneById(xapiTimeZone switch {
                        ConfigurationTimeZone.Africa_Porto_Novo                => "Africa/Porto-Novo",
                        ConfigurationTimeZone.America_Argentina_Buenos_Aires   => "America/Argentina/Buenos_Aires",
                        ConfigurationTimeZone.America_Argentina_Catamarca      => "America/Argentina/Catamarca",
                        ConfigurationTimeZone.America_Argentina_ComodRivadavia => "America/Argentina/ComodRivadavia",
                        ConfigurationTimeZone.America_Argentina_Cordoba        => "America/Argentina/Cordoba",
                        ConfigurationTimeZone.America_Argentina_Jujuy          => "America/Argentina/Jujuy",
                        ConfigurationTimeZone.America_Argentina_La_Rioja       => "America/Argentina/La_Rioja",
                        ConfigurationTimeZone.America_Argentina_Mendoza        => "America/Argentina/Mendoza",
                        ConfigurationTimeZone.America_Argentina_Rio_Gallegos   => "America/Argentina/Rio_Gallegos",
                        ConfigurationTimeZone.America_Argentina_Salta          => "America/Argentina/Salta",
                        ConfigurationTimeZone.America_Argentina_San_Juan       => "America/Argentina/San_Juan",
                        ConfigurationTimeZone.America_Argentina_San_Luis       => "America/Argentina/San_Luis",
                        ConfigurationTimeZone.America_Argentina_Tucuman        => "America/Argentina/Tucuman",
                        ConfigurationTimeZone.America_Argentina_Ushuaia        => "America/Argentina/Ushuaia",
                        ConfigurationTimeZone.America_Blanc_Sablon             => "America/Blanc-Sablon",
                        ConfigurationTimeZone.America_Indiana_Indianapolis     => "America/Indiana/Indianapolis",
                        ConfigurationTimeZone.America_Indiana_Knox             => "America/Indiana/Knox",
                        ConfigurationTimeZone.America_Indiana_Marengo          => "America/Indiana/Marengo",
                        ConfigurationTimeZone.America_Indiana_Petersburg       => "America/Indiana/Petersburg",
                        ConfigurationTimeZone.America_Indiana_Tell_City        => "America/Indiana/Tell_City",
                        ConfigurationTimeZone.America_Indiana_Vevay            => "America/Indiana/Vevay",
                        ConfigurationTimeZone.America_Indiana_Vincennes        => "America/Indiana/Vincennes",
                        ConfigurationTimeZone.America_Indiana_Winamac          => "America/Indiana/Winamac",
                        ConfigurationTimeZone.America_Kentucky_Louisville      => "America/Kentucky/Louisville",
                        ConfigurationTimeZone.America_Kentucky_Monticello      => "America/Kentucky/Monticello",
                        ConfigurationTimeZone.America_North_Dakota_Beulah      => "America/North_Dakota/Beulah",
                        ConfigurationTimeZone.America_North_Dakota_Center      => "America/North_Dakota/Center",
                        ConfigurationTimeZone.America_North_Dakota_New_Salem   => "America/North_Dakota/New_Salem",
                        ConfigurationTimeZone.America_Port_au_Prince           => "America/Port-au-Prince",
                        ConfigurationTimeZone.Asia_Kashgar                     => "Asia/Dhaka",
                        ConfigurationTimeZone.Asia_Urumqi                      => "Asia/Dhaka",
                        ConfigurationTimeZone.Asia_Ust_Nera                    => "Asia/Ust-Nera",
                        ConfigurationTimeZone.CET                              => "Europe/Paris",
                        ConfigurationTimeZone.EET                              => "Europe/Sofia",
                        ConfigurationTimeZone.Etc_GMT_Minus_0                  => "Etc/GMT-0",
                        ConfigurationTimeZone.Etc_GMT_Minus_1                  => "Etc/GMT-1",
                        ConfigurationTimeZone.Etc_GMT_Minus_10                 => "Etc/GMT-10",
                        ConfigurationTimeZone.Etc_GMT_Minus_11                 => "Etc/GMT-11",
                        ConfigurationTimeZone.Etc_GMT_Minus_12                 => "Etc/GMT-12",
                        ConfigurationTimeZone.Etc_GMT_Minus_13                 => "Etc/GMT-13",
                        ConfigurationTimeZone.Etc_GMT_Minus_14                 => "Etc/GMT-14",
                        ConfigurationTimeZone.Etc_GMT_Minus_2                  => "Etc/GMT-2",
                        ConfigurationTimeZone.Etc_GMT_Minus_3                  => "Etc/GMT-3",
                        ConfigurationTimeZone.Etc_GMT_Minus_4                  => "Etc/GMT-4",
                        ConfigurationTimeZone.Etc_GMT_Minus_5                  => "Etc/GMT-5",
                        ConfigurationTimeZone.Etc_GMT_Minus_6                  => "Etc/GMT-6",
                        ConfigurationTimeZone.Etc_GMT_Minus_7                  => "Etc/GMT-7",
                        ConfigurationTimeZone.Etc_GMT_Minus_8                  => "Etc/GMT-8",
                        ConfigurationTimeZone.Etc_GMT_Minus_9                  => "Etc/GMT-9",
                        ConfigurationTimeZone.Etc_GMT_Plus_0                   => "Etc/GMT+0",
                        ConfigurationTimeZone.Etc_GMT_Plus_1                   => "Etc/GMT+1",
                        ConfigurationTimeZone.Etc_GMT_Plus_10                  => "Etc/GMT+10",
                        ConfigurationTimeZone.Etc_GMT_Plus_11                  => "Etc/GMT+11",
                        ConfigurationTimeZone.Etc_GMT_Plus_12                  => "Etc/GMT+12",
                        ConfigurationTimeZone.Etc_GMT_Plus_2                   => "Etc/GMT+2",
                        ConfigurationTimeZone.Etc_GMT_Plus_3                   => "Etc/GMT+3",
                        ConfigurationTimeZone.Etc_GMT_Plus_4                   => "Etc/GMT+4",
                        ConfigurationTimeZone.Etc_GMT_Plus_5                   => "Etc/GMT+5",
                        ConfigurationTimeZone.Etc_GMT_Plus_6                   => "Etc/GMT+6",
                        ConfigurationTimeZone.Etc_GMT_Plus_7                   => "Etc/GMT+7",
                        ConfigurationTimeZone.Etc_GMT_Plus_8                   => "Etc/GMT+8",
                        ConfigurationTimeZone.Etc_GMT_Plus_9                   => "Etc/GMT+9",
                        ConfigurationTimeZone.Europe_Kyiv                      => "Europe/Kiev",
                        ConfigurationTimeZone.GB_Eire                          => "GB-Eire",
                        ConfigurationTimeZone.GMT_Minus_0                      => "GMT-0",
                        ConfigurationTimeZone.GMT_Plus_0                       => "GMT+0",
                        ConfigurationTimeZone.MET                              => "Europe/Paris",
                        ConfigurationTimeZone.NZ_CHAT                          => "NZ-CHAT",
                        ConfigurationTimeZone.Pacific_Kanton                   => "Pacific/Enderbury",
                        ConfigurationTimeZone.US_East_Indiana                  => "US/East-Indiana",
                        ConfigurationTimeZone.US_Indiana_Starke                => "US/Indiana-Starke",
                        ConfigurationTimeZone.W_SU                             => "W-SU",
                        ConfigurationTimeZone.WET                              => "Europe/Lisbon",
                        _                                                      => Underscore.Replace(xapiTimeZone.ToString(), "/", 1)
                    }
                )
            };

            TimeZoneInfoCache[xapiTimeZone] = timeZone;
        }

        return timeZone;
    }

    public static ConfigurationTimeZone ToXAPITimeZone(this TimeZoneInfo dotnetTimeZone) {
        return dotnetTimeZone.Id switch {
            "Africa/Porto-Novo"                => ConfigurationTimeZone.Africa_Porto_Novo,
            "America/Argentina/Buenos_Aires"   => ConfigurationTimeZone.America_Argentina_Buenos_Aires,
            "America/Argentina/Catamarca"      => ConfigurationTimeZone.America_Argentina_Catamarca,
            "America/Argentina/ComodRivadavia" => ConfigurationTimeZone.America_Argentina_ComodRivadavia,
            "America/Argentina/Cordoba"        => ConfigurationTimeZone.America_Argentina_Cordoba,
            "America/Argentina/Jujuy"          => ConfigurationTimeZone.America_Argentina_Jujuy,
            "America/Argentina/La_Rioja"       => ConfigurationTimeZone.America_Argentina_La_Rioja,
            "America/Argentina/Mendoza"        => ConfigurationTimeZone.America_Argentina_Mendoza,
            "America/Argentina/Rio_Gallegos"   => ConfigurationTimeZone.America_Argentina_Rio_Gallegos,
            "America/Argentina/Salta"          => ConfigurationTimeZone.America_Argentina_Salta,
            "America/Argentina/San_Juan"       => ConfigurationTimeZone.America_Argentina_San_Juan,
            "America/Argentina/San_Luis"       => ConfigurationTimeZone.America_Argentina_San_Luis,
            "America/Argentina/Tucuman"        => ConfigurationTimeZone.America_Argentina_Tucuman,
            "America/Argentina/Ushuaia"        => ConfigurationTimeZone.America_Argentina_Ushuaia,
            "America/Blanc-Sablon"             => ConfigurationTimeZone.America_Blanc_Sablon,
            "America/Indiana/Indianapolis"     => ConfigurationTimeZone.America_Indiana_Indianapolis,
            "America/Indiana/Knox"             => ConfigurationTimeZone.America_Indiana_Knox,
            "America/Indiana/Marengo"          => ConfigurationTimeZone.America_Indiana_Marengo,
            "America/Indiana/Petersburg"       => ConfigurationTimeZone.America_Indiana_Petersburg,
            "America/Indiana/Tell_City"        => ConfigurationTimeZone.America_Indiana_Tell_City,
            "America/Indiana/Vevay"            => ConfigurationTimeZone.America_Indiana_Vevay,
            "America/Indiana/Vincennes"        => ConfigurationTimeZone.America_Indiana_Vincennes,
            "America/Indiana/Winamac"          => ConfigurationTimeZone.America_Indiana_Winamac,
            "America/Kentucky/Louisville"      => ConfigurationTimeZone.America_Kentucky_Louisville,
            "America/Kentucky/Monticello"      => ConfigurationTimeZone.America_Kentucky_Monticello,
            "America/North_Dakota/Beulah"      => ConfigurationTimeZone.America_North_Dakota_Beulah,
            "America/North_Dakota/Center"      => ConfigurationTimeZone.America_North_Dakota_Center,
            "America/North_Dakota/New_Salem"   => ConfigurationTimeZone.America_North_Dakota_New_Salem,
            "America/Port-au-Prince"           => ConfigurationTimeZone.America_Port_au_Prince,
            "Asia/Ust-Nera"                    => ConfigurationTimeZone.Asia_Ust_Nera,
            "Etc/GMT+0"                        => ConfigurationTimeZone.Etc_GMT_Plus_0,
            "Etc/GMT+1"                        => ConfigurationTimeZone.Etc_GMT_Plus_1,
            "Etc/GMT+10"                       => ConfigurationTimeZone.Etc_GMT_Plus_10,
            "Etc/GMT+11"                       => ConfigurationTimeZone.Etc_GMT_Plus_11,
            "Etc/GMT+12"                       => ConfigurationTimeZone.Etc_GMT_Plus_12,
            "Etc/GMT+2"                        => ConfigurationTimeZone.Etc_GMT_Plus_2,
            "Etc/GMT+3"                        => ConfigurationTimeZone.Etc_GMT_Plus_3,
            "Etc/GMT+4"                        => ConfigurationTimeZone.Etc_GMT_Plus_4,
            "Etc/GMT+5"                        => ConfigurationTimeZone.Etc_GMT_Plus_5,
            "Etc/GMT+6"                        => ConfigurationTimeZone.Etc_GMT_Plus_6,
            "Etc/GMT+7"                        => ConfigurationTimeZone.Etc_GMT_Plus_7,
            "Etc/GMT+8"                        => ConfigurationTimeZone.Etc_GMT_Plus_8,
            "Etc/GMT+9"                        => ConfigurationTimeZone.Etc_GMT_Plus_9,
            "Etc/GMT-0"                        => ConfigurationTimeZone.Etc_GMT_Minus_0,
            "Etc/GMT-1"                        => ConfigurationTimeZone.Etc_GMT_Minus_1,
            "Etc/GMT-10"                       => ConfigurationTimeZone.Etc_GMT_Minus_10,
            "Etc/GMT-11"                       => ConfigurationTimeZone.Etc_GMT_Minus_11,
            "Etc/GMT-12"                       => ConfigurationTimeZone.Etc_GMT_Minus_12,
            "Etc/GMT-13"                       => ConfigurationTimeZone.Etc_GMT_Minus_13,
            "Etc/GMT-14"                       => ConfigurationTimeZone.Etc_GMT_Minus_14,
            "Etc/GMT-2"                        => ConfigurationTimeZone.Etc_GMT_Minus_2,
            "Etc/GMT-3"                        => ConfigurationTimeZone.Etc_GMT_Minus_3,
            "Etc/GMT-4"                        => ConfigurationTimeZone.Etc_GMT_Minus_4,
            "Etc/GMT-5"                        => ConfigurationTimeZone.Etc_GMT_Minus_5,
            "Etc/GMT-6"                        => ConfigurationTimeZone.Etc_GMT_Minus_6,
            "Etc/GMT-7"                        => ConfigurationTimeZone.Etc_GMT_Minus_7,
            "Etc/GMT-8"                        => ConfigurationTimeZone.Etc_GMT_Minus_8,
            "Etc/GMT-9"                        => ConfigurationTimeZone.Etc_GMT_Minus_9,
            "GB-Eire"                          => ConfigurationTimeZone.GB_Eire,
            "GMT+0"                            => ConfigurationTimeZone.GMT_Plus_0,
            "GMT-0"                            => ConfigurationTimeZone.GMT_Minus_0,
            "NZ-CHAT"                          => ConfigurationTimeZone.NZ_CHAT,
            "US/East-Indiana"                  => ConfigurationTimeZone.US_East_Indiana,
            "US/Indiana-Starke"                => ConfigurationTimeZone.US_Indiana_Starke,
            "W-SU"                             => ConfigurationTimeZone.W_SU,
            _ => Enum.TryParse(Regex.Replace(dotnetTimeZone.Id, @"[^a-z0-9_]", match => match.Value switch {
                "."                                        => "_",
                "/"                                        => "_",
                "+"                                        => "_Plus_",
                "-" when dotnetTimeZone.Id.Contains("GMT") => "_Minus_",
                "-"                                        => "_",
                _                                          => ""
            }, RegexOptions.IgnoreCase), out ConfigurationTimeZone parsed) ? parsed
                : throw new TimeZoneNotFoundException($"Could not convert .NET time zone with ID {dotnetTimeZone.Id} into an xAPI time zone")
        };
    }

}