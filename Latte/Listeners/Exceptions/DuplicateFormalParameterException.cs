namespace Latte.Listeners.Exceptions;

using Antlr4.Runtime;

public class DuplicateFormalParameterException : Exception
{
    public DuplicateFormalParameterException(IToken token)
    {
    }

    public int Line { get; set; }

    public int Column { get; set; }
}

