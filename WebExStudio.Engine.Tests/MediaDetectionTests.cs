using WebExStudio.Engine.Actions;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>Erkennung von Medien-URLs/-Streams für den download_stream-Node (reine Logik).</summary>
public class MediaDetectionTests
{
    [Theory]
    [InlineData("https://x/video.mp4", "", true)]
    [InlineData("https://x/playlist.m3u8", "", true)]
    [InlineData("https://x/manifest.mpd", "", true)]
    [InlineData("https://x/audio.mp3", "", true)]
    [InlineData("https://x/seg1.ts", "", true)]
    [InlineData("https://x/api?id=5", "video/mp4", true)]       // per Content-Type
    [InlineData("https://x/stream", "application/vnd.apple.mpegurl", true)]
    [InlineData("https://x/page.html", "text/html", false)]
    [InlineData("https://x/script.js", "application/javascript", false)]
    public void IsMediaUrl_DetectsMediaByUrlOrContentType(string url, string ctype, bool expected) =>
        Assert.Equal(expected, DownloadStreamHandler.IsMediaUrl(url, ctype));

    [Theory]
    [InlineData("https://x/playlist.m3u8", true)]
    [InlineData("https://x/manifest.mpd", true)]
    [InlineData("https://x/video.mp4", false)]
    [InlineData("https://x/audio.mp3", false)]
    public void IsManifest_DetectsHlsAndDash(string url, bool expected) =>
        Assert.Equal(expected, DownloadStreamHandler.IsManifest(url));
}
