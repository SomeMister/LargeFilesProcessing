using LargeTestFileTool.Infrastructure;
using Xunit;

namespace LargeTestFileTool.Tests;

public sealed class SizeParserTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("1024", 1024)]
    [InlineData("1KB", 1024)]
    [InlineData("1 MB", 1024 * 1024)]
    [InlineData("2GB", 2L * 1024 * 1024 * 1024)]
    public void ParseToBytes_ValidValues(string input, long expected)
    {
        long actual = SizeParser.ParseToBytes(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]

// invalid
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("ABC")]
    [InlineData("10XB")]
    public void ParseToBytes_InvalidValues_Throws(string input)
    {
        Assert.ThrowsAny<Exception>(() => SizeParser.ParseToBytes(input));
    }
}
