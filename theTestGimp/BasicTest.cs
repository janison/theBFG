using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace theTestGimp
{
    [TestClass]
    public class SomeAppWorkflowBehavior
    {
        [TestMethod]
        public void the_first_test()
        {
            Console.WriteLine("the_first_test");
            Thread.Sleep(10 * 1000);
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void the_2_test()
        {
            Console.WriteLine("the_2_test");
            Thread.Sleep(10 * 1000);

            Assert.IsTrue(true);

        }

        [TestMethod]
        public void the_3_test()
        {
            Console.WriteLine("the_3_test");
            Thread.Sleep(10 * 1000);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void the_4_test()
        {
            Console.WriteLine("the_4_test-ran-successfully");
            Thread.Sleep(10 * 1000);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void the_5_test()
        {
            Console.WriteLine("the_5_test-ran-successfully");
            Thread.Sleep(10 * 1000);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void the_6_test()
        {
            Console.WriteLine("the_first_test-ran-successfully");
            Thread.Sleep(10 * 1000);

            Assert.IsTrue(true);
        }

    }
}