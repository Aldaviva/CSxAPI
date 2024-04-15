using CSxAPI;

namespace Tests;

public class SampleUnitTest {

    private readonly XAPI _xapi = A.Fake<XAPI>(); // mocked CSxAPIClient

    [Fact]
    public async Task DialAndHangUp() {
        // Arrange
        A.CallTo(() => _xapi.Command.Dial(A<string>._, null, null, null, null, null, null, null))
            .Returns(new Dictionary<string, object> { ["CallId"] = 3, ["ConferenceId"] = 2 });

        // Act
        IDictionary<string, object> actual = await _xapi.Command.Dial("10990@bjn.vc");
        await _xapi.Command.Call.Disconnect((int?) actual["CallId"]);

        // Assert
        actual["CallId"].Should().Be(3);
        actual["ConferenceId"].Should().Be(2);

        A.CallTo(() => _xapi.Command.Dial("10990@bjn.vc", null, null, null, null, null, null, null))
            .MustHaveHappened();
        A.CallTo(() => _xapi.Command.Call.Disconnect(3)).MustHaveHappened();
    }

}