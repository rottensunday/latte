namespace Latte.Models;

using System.Diagnostics;

public class CompilationResult
{
    public CompilationResult(IEnumerable<CompilationError> errors, List<string> instructions = null)
    {
        Errors = errors?.ToList();
        SetInstructions(instructions);
    }

    public bool Success => Errors == null || Errors.Count == 0;

    public List<CompilationError> Errors { get; set; }

    public List<string> Instructions { get; set; }

    public void SetInstructions(List<string> instructions) => Instructions = instructions;

    public void WriteErrors()
    {
        if (Errors == null)
        {
            return;
        }

        foreach (var error in Errors)
        {
            switch (error.ErrorType)
            {
                case CompilationErrorType.TypeMismatch:
                    Console.WriteLine($"Type mismatch: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.UndefinedReference:
                    Console.WriteLine($"Undefined reference: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.DuplicateParameterName:
                    Console.WriteLine(
                        $"Duplicate parameter name: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.NotAFunction:
                    Console.WriteLine($"Not a function: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.NotAVariable:
                    Console.WriteLine($"Not a variable: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.WrongArgumentsLength:
                    Console.WriteLine(
                        $"Wrong arguments list length: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.VariableAlreadyDeclared:
                    Console.WriteLine(
                        $"Variable already declared in scope: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
                case CompilationErrorType.FunctionDoesntReturn:
                    Console.WriteLine(
                        $"Can't find achievable function return statement: {error.Message} in [l:{error.Line}; c:{error.Column}]");
                    break;
            }
        }
    }

    public string WriteInstructions()
    {
        if (Instructions is { Count: > 0 })
        {
            var text = String.Join('\n', Instructions);
            Console.Write(String.Join('\n', Instructions));

            return text;
        }

        return null;
    }

    public void WriteInstructionsToFile(string path)
    {
        if (Instructions is { Count: > 0 })
        {
            File.WriteAllLines(path, Instructions);
        }
    }

    public string WriteOutput()
    {
        if (Instructions is { Count: > 0 })
        {
            var tempAssembler = "temp.s";
            var tempBin = "temp";
            File.WriteAllLines(tempAssembler, Instructions);
            var generateProcess = new Process
            {
                StartInfo = new ProcessStartInfo("clang", $"-arch x86_64 -o {tempBin} library.o {tempAssembler}")
            };
            generateProcess.Start();
            generateProcess.WaitForExit();

            var readProcess = new Process { StartInfo = new ProcessStartInfo($"./{tempBin}") };
            readProcess.StartInfo.RedirectStandardOutput = true;
            readProcess.Start();
            var output = readProcess.StandardOutput.ReadToEnd();
            readProcess.WaitForExit();


            File.Delete(tempAssembler);
            File.Delete(tempBin);

            Console.Write(output);

            return output;
        }

        return null;
    }

    public void WriteOutputToFile(string path)
    {
        if (Instructions is { Count: > 0 })
        {
            var tempAssembler = "temp.s";
            var tempBin = "temp";
            File.WriteAllLines(tempAssembler, Instructions);
            var generateProcess = new Process
            {
                StartInfo = new ProcessStartInfo("clang", $"-arch x86_64 -o {tempBin} library.o {tempAssembler}")
            };
            generateProcess.Start();
            generateProcess.WaitForExit();

            var readProcess = new Process { StartInfo = new ProcessStartInfo($"./{tempBin}") };
            readProcess.StartInfo.RedirectStandardOutput = true;
            readProcess.Start();
            var output = readProcess.StandardOutput.ReadToEnd();
            readProcess.WaitForExit();

            File.Delete(tempAssembler);
            File.Delete(tempBin);

            File.WriteAllText(path, output);
        }
    }
}
