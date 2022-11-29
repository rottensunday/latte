namespace Latte.Models;

public class CompilationResult
{
    public CompilationResult(IEnumerable<CompilationError> errors)
    {
        Errors = errors?.ToList();
    }

    public CompilationResult(CompilationError error)
    {
        Errors = new List<CompilationError> { error };
    }

    public CompilationResult()
    {
        Errors = null;
    }

    public bool Success => Errors == null || Errors.Count == 0;

    public List<CompilationError> Errors { get; set; }

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
}
