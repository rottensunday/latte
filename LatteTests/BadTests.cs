namespace LatteTests;

using Latte.Compiler;
using Latte.Compiler.Models;

public class BadTests
{
    private static readonly string[] BadTestsFiles =
        Directory.EnumerateFiles("Tests/Bad").Where(x => x.EndsWith("lat")).ToArray();

    [TestCaseSource(nameof(BadTestsFiles))]
    public void BadInputTest(string path)
    {
        var result = LatteCompiler.Compile(path);

        Assert.IsTrue(result.ParsingResultType != ParsingResultType.Ok || !result.CompilationResult.Success);
    }
}




