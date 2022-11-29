namespace LatteTests;

using Latte.Compiler;
using Latte.Compiler.Models;

public class GoodTests
{
    private static readonly string[] GoodTestsFiles =
        Directory.EnumerateFiles("Tests/Good").Where(x => x.EndsWith("lat")).ToArray();

    [TestCaseSource(nameof(GoodTestsFiles))]
    public void GoodInputTest(string path)
    {
        var result = LatteCompiler.Compile(path);

        Assert.AreEqual(ParsingResultType.Ok, result.ParsingResultType);
        Assert.IsTrue(result.CompilationResult.Success);
    }
}




