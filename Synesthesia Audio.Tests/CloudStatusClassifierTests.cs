using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// PAT-05: `ListFilesAsync` returned an empty list for both "the folder is empty" and "the server refused you",
/// so a wrong password looked identical to a deleted library and the scan cached the empty result.
/// </summary>
public sealed class CloudStatusClassifierTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(407)]
    public void Authentication_and_permission_statuses_are_refusals(int status)
    {
        Assert.True(CloudStatusClassifier.IsAccessDenied(status));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(207)]   // 207 Multi-Status is the normal PROPFIND success
    [InlineData(301)]
    [InlineData(404)]
    [InlineData(500)]
    public void Other_statuses_are_not_refusals(int status)
    {
        Assert.False(CloudStatusClassifier.IsAccessDenied(status));
    }

    [Fact]
    public void A_401_message_points_at_the_credentials()
    {
        var message = CloudStatusClassifier.Describe(401, "Unauthorized");

        Assert.Contains("credential", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("401", message);
    }

    [Fact]
    public void A_403_message_points_at_permissions()
    {
        var message = CloudStatusClassifier.Describe(403, "Forbidden");

        Assert.Contains("permission", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("403", message);
    }

    [Fact]
    public void A_404_message_points_at_the_path()
    {
        var message = CloudStatusClassifier.Describe(404, "Not Found");

        Assert.Contains("not found", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("404", message);
    }

    [Fact]
    public void A_missing_reason_phrase_does_not_produce_a_broken_message()
    {
        var message = CloudStatusClassifier.Describe(500, null);

        Assert.Contains("500", message);
        Assert.DoesNotContain("()", message);
    }

    [Fact]
    public void An_unrecognised_status_still_describes_itself()
    {
        var message = CloudStatusClassifier.Describe(418, "I'm a teapot");

        Assert.Contains("418", message);
        Assert.Contains("teapot", message);
    }
}
