using System;
using NUnit.Framework;

namespace theBFG.Tests
{
    public class SomeAppWorkflowBehavior
    {
        [Test]
        public void the_first_test()
        {
            Console.WriteLine("the_first_test-ran-successfully");
            Assert.Pass();
        }
    }
}