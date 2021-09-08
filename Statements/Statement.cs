using Antigen.Tree;

namespace Antigen.Statements
{
    public class Statement : Node
    {
        protected string _contents;

        public Statement(TestCase testCase) : base(testCase)
        {

        }

        protected virtual void PopulateContent()
        {

        }
    }
}
