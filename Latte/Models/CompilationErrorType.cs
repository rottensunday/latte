namespace Latte.Models;

public enum CompilationErrorType
{
    DuplicateParameterName,
    TypeMismatch,
    WrongArgumentsLength,
    UndefinedReference,
    NotAVariable,
    NotAFunction,
    VariableAlreadyDeclared,
    FunctionDoesntReturn
}




