using Antigen.Tree;

namespace Antigen.Statements
{
    public class Statement : Node
    {

#if DEBUG
        private readonly Dictionary<string, int> _statementsCount = new();
#endif

        public Statement(TestCase testCase) : base(testCase)
        {

        }


        protected override string Annotate()
        {
#if DEBUG
            string typeName = GetType().Name;
            if (!_statementsCount.ContainsKey(typeName))
            {
                _statementsCount[typeName] = 0;
            }
            _statementsCount[typeName]++;
            return $"{GetCode() /* S#{_statementsCount[typeName]}: {typeName} */}";
#else
            return GetCode();
#endif
        }

        protected override string GetCode()
        {
            return string.Empty;
        }
    }
}
