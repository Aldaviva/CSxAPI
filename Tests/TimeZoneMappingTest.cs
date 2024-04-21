using CSxAPI;
using CSxAPI.API.Data;

namespace Tests;

public class TimeZoneMappingTest {

    [Theory]
    [MemberData(nameof(AllCiscoTimeZones))]
    public void HandlesEveryCiscoTimeZone(ConfigurationTimeZone ciscoTimeZone) {
        TimeZoneInfo actual = ciscoTimeZone.ToTimeZoneInfo();
        actual.Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(AllOneToOneCiscoTimeZones))]
    public void OneToOneCiscoTimeZones(ConfigurationTimeZone ciscoTimeZone) {
        ConfigurationTimeZone inverse = ciscoTimeZone.ToTimeZoneInfo().ToXAPITimeZone();
        inverse.Should().Be(ciscoTimeZone, "symmetric conversion");
    }

    public static TheoryData<ConfigurationTimeZone> AllCiscoTimeZones => new(Enum.GetValues<ConfigurationTimeZone>());

    public static TheoryData<ConfigurationTimeZone> AllOneToOneCiscoTimeZones => new(Enum.GetValues<ConfigurationTimeZone>().Except([
        ConfigurationTimeZone.Asia_Kashgar,
        ConfigurationTimeZone.Asia_Urumqi,
        ConfigurationTimeZone.CET,
        ConfigurationTimeZone.EET,
        ConfigurationTimeZone.Europe_Kyiv,
        ConfigurationTimeZone.MET,
        ConfigurationTimeZone.Pacific_Kanton,
        ConfigurationTimeZone.WET
    ]));

    [Theory]
    [InlineData("America/Los_Angeles", ConfigurationTimeZone.America_Los_Angeles)]
    [InlineData("America/New_York", ConfigurationTimeZone.America_New_York)]
    [InlineData("America/Chicago", ConfigurationTimeZone.America_Chicago)]
    [InlineData("America/Denver", ConfigurationTimeZone.America_Denver)]
    [InlineData("UTC", ConfigurationTimeZone.UTC)]
    [InlineData("Asia/Kolkata", ConfigurationTimeZone.Asia_Kolkata)]
    [InlineData("Pacific/Auckland", ConfigurationTimeZone.Pacific_Auckland)]
    [InlineData("Europe/London", ConfigurationTimeZone.Europe_London)]
    [InlineData("Europe/Berlin", ConfigurationTimeZone.Europe_Berlin)]
    [InlineData("Europe/Kiev", ConfigurationTimeZone.Europe_Kiev)]
    public void HandlesSomeDotNetTimeZones(string dotNetTimeZoneId, ConfigurationTimeZone expected) {
        TimeZoneInfo.FindSystemTimeZoneById(dotNetTimeZoneId).ToXAPITimeZone().Should().Be(expected);
    }

    [Fact]
    public void HandlesAllDotNetTimeZones() {
        Action thrower = () => TimeZoneInfo.FindSystemTimeZoneById("Mid-Atlantic Standard Time").ToXAPITimeZone();
        thrower.Should().Throw<TimeZoneNotFoundException>();
    }

    [Theory]
    [InlineData(ConfigurationTimeZone.America_Ciudad_Juarez, "America/Ciudad_Juarez", ConfigurationTimeZone.America_Ciudad_Juarez)]
    [InlineData(ConfigurationTimeZone.Asia_Kashgar, "Asia/Dhaka", ConfigurationTimeZone.Asia_Dhaka)]
    [InlineData(ConfigurationTimeZone.Asia_Urumqi, "Asia/Dhaka", ConfigurationTimeZone.Asia_Dhaka)]
    [InlineData(ConfigurationTimeZone.Europe_Kyiv, "Europe/Kiev", ConfigurationTimeZone.Europe_Kiev)]
    [InlineData(ConfigurationTimeZone.Pacific_Kanton, "Pacific/Enderbury", ConfigurationTimeZone.Pacific_Enderbury)]
    [InlineData(ConfigurationTimeZone.CET, "Europe/Paris", ConfigurationTimeZone.Europe_Paris)]
    [InlineData(ConfigurationTimeZone.EET, "Europe/Sofia", ConfigurationTimeZone.Europe_Sofia)]
    [InlineData(ConfigurationTimeZone.MET, "Europe/Paris", ConfigurationTimeZone.Europe_Paris)]
    [InlineData(ConfigurationTimeZone.WET, "Europe/Lisbon", ConfigurationTimeZone.Europe_Lisbon)]
    [InlineData(ConfigurationTimeZone.Antarctica_Troll, "Antarctica/Troll", ConfigurationTimeZone.Antarctica_Troll)]
    public void RoundTripProblematicTimeZones(ConfigurationTimeZone xapiTimeZone, string dotNetTimeZoneId, ConfigurationTimeZone? bclToXapiZone = null) {
        TimeZoneInfo actualBclZone = xapiTimeZone.ToTimeZoneInfo();
        actualBclZone.Id.Should().Be(dotNetTimeZoneId, "conversion from Cisco to .NET");
        actualBclZone.ToXAPITimeZone().Should().Be(bclToXapiZone ?? xapiTimeZone, "conversion from .NET to Cisco");
    }

}