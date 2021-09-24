namespace Antigen
{
    public static partial class Helpers
    {
        public static string GetVariableName(Tree.ValueType variableType, int id)
        {
            return variableType.VariableNameHint() + "_" + id;
        }
    }
}
