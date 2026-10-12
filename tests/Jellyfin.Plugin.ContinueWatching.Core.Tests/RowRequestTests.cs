using Jellyfin.Plugin.ContinueWatching.Core;

namespace Jellyfin.Plugin.ContinueWatching.Core.Tests;

public class RowRequestTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    [Fact]
    public void WithNoUserGivenTheCallersOwnRowIsBuilt()
    {
        Assert.Equal(Ann, RowRequest.TargetUser(Ann, null, callerIsAdmin: false));
        Assert.Equal(Ann, RowRequest.TargetUser(Ann, Guid.Empty, callerIsAdmin: false));
    }

    [Fact]
    public void AnyoneMayAskForTheirOwnRow() =>
        Assert.Equal(Ann, RowRequest.TargetUser(Ann, Ann, callerIsAdmin: false));

    [Fact]
    public void OnlyAnAdminMayReadSomeoneElsesRow()
    {
        Assert.Null(RowRequest.TargetUser(Ann, Bob, callerIsAdmin: false));
        Assert.Equal(Bob, RowRequest.TargetUser(Ann, Bob, callerIsAdmin: true));
    }

    [Fact]
    public void AnApiKeyHasNoUserSoItMustNameOne()
    {
        // An API key authenticates as an admin with an empty user id.
        Assert.Null(RowRequest.TargetUser(Guid.Empty, null, callerIsAdmin: true));
        Assert.Equal(Bob, RowRequest.TargetUser(Guid.Empty, Bob, callerIsAdmin: true));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(20, 20)]
    [InlineData(1000, 100)]
    public void TheLimitStaysInRange(int asked, int used) => Assert.Equal(used, RowRequest.ClampLimit(asked));

    private enum Field
    {
        Overview,
        UserData,
        MediaStreams
    }

    [Fact]
    public void ListsParseLikeJellyfinsOwnEndpoints()
    {
        Assert.Equal([Field.Overview, Field.UserData], RowRequest.ParseList<Field>("Overview, userdata"));
        Assert.Empty(RowRequest.ParseList<Field>(null));
        Assert.Empty(RowRequest.ParseList<Field>(""));
    }

    [Fact]
    public void UnknownNamesAndNumbersAreIgnoredRatherThanFailingTheRow()
    {
        Assert.Equal([Field.MediaStreams], RowRequest.ParseList<Field>("Nope,MediaStreams,1,99,MediaStreams"));
    }
}
