namespace Latte.Models.Intermediate;

using System.Text;

public class FunctionCallTerm : Term
{
    public FunctionCallTerm(string name, List<Term> arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public string Name { get; set; }

    public List<Term> Arguments { get; set; }

    public override string ToString()
    {
        var builder = new StringBuilder(Name);

        if (Arguments.Count == 0)
        {
            builder.Append("()");

            return builder.ToString();
        }

        builder.Append('(');

        for (var i = 0; i < Arguments.Count; i++)
        {
            if (i == Arguments.Count - 1)
            {
                builder.Append($"{Arguments[i]})");
            }
            else
            {
                builder.Append($"{Arguments[i]},");
            }
        }

        return builder.ToString();
    }

    public override List<string> GetStringLiterals() => Arguments.SelectMany(x => x.GetStringLiterals()).ToList();
    
    public override List<RegisterTerm> GetUsedRegisters() => Arguments.SelectMany(x => x.GetUsedRegisters()).ToList();
    public override void SwitchRegisters(string used, string newRegister)
    {
        foreach (var argument in Arguments)
        {
            argument.SwitchRegisters(used, newRegister);
        }
    }
}
