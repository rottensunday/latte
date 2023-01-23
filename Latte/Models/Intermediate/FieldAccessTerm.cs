namespace Latte.Models.Intermediate;

using Scopes;

public class FieldAccessTerm : Term
{
    public FieldAccessTerm(ClassSymbol classSymbol, string instanceName, FieldAccess innerFieldAccess, RegisterTerm instanceRegister, string type)
    {
        ClassSymbol = classSymbol;
        InstanceName = instanceName;
        InnerFieldAccess = innerFieldAccess;
        InstanceRegister = instanceRegister;
        Type = type;
    }

    public ClassSymbol ClassSymbol { get; set; }
    
    public string InstanceName { get; set; }
    
    public RegisterTerm InstanceRegister { get; set; }
    
    public FieldAccess InnerFieldAccess { get; set; }

    public string Type { get; set; }
    
    // public RegisterTerm VirtualRegister { get; set; } = new 
    
    public override List<string> GetStringLiterals() => new();
    
    public override List<RegisterTerm> GetUsedRegisters() => new() { InstanceRegister };
    public override void SwitchRegisters(string used, string newRegister)
    {
        if (InstanceRegister.Name == used)
        {
            InstanceRegister.Name = newRegister;
        }
    }

    public override string ToString()
    {
        return $"{InstanceRegister}.{InnerFieldAccess}";
    }
}

public class FieldAccess
{
    public FieldAccess(string classField, FieldAccess innerFieldAccess)
    {
        ClassField = classField;
        InnerFieldAccess = innerFieldAccess;
    }

    public FieldAccess()
    {
    }
    
    public override string ToString()
    {
        return InnerFieldAccess != null ? $"{ClassField}.{InnerFieldAccess}" : ClassField;
    }

    public string ClassField { get; set; }
    
    public FieldAccess InnerFieldAccess { get; set; }
}