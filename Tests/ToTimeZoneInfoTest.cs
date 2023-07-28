using CSxAPI;
using CSxAPI.API.Data;

namespace Tests;

public class ToTimeZoneInfoTest {

    [Theory]
    [MemberData(nameof(AllCiscoTimeZones))]
    public void HandlesEveryCiscoTimeZone(ConfigurationTimeZone ciscoTimeZone) {
        TimeZoneInfo actual = ciscoTimeZone.ToTimeZoneInfo();
        actual.Should().NotBeNull();
    }

    public static IEnumerable<object[]> AllCiscoTimeZones => Enum.GetValues<ConfigurationTimeZone>().Select(zone => new object[] { zone });

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
    public void HandlesSomeDotNetTimeZones(string dotNetTimeZoneId, ConfigurationTimeZone expected) {
        TimeZoneInfo.FindSystemTimeZoneById(dotNetTimeZoneId).ToXAPITimeZone().Should().Be(expected);
    }

}