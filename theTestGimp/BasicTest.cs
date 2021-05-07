using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rxns.Logging;

namespace theTestGimp
{
    [TestClass]
    public class SomeAppWorkflowBehavior

    {
        [TestMethod]
        [Ignore]
        public void generateTests()
        {

            foreach (var i in Enumerable.Range(0, 100))
            {
                Debug.WriteLine(@"[TestMethod]
        public void the_"+$"{i}_test()"+ @"
        {
            " + $"Console.WriteLine(\"runnning {i}\");" + @"
                Thread.Sleep(1 * 1000);
                Assert.IsTrue(true);        
        }

                ");

            }
        }

        [TestMethod]
        public void the2_2_test()
        {
            Console.WriteLine("runnning 0");
            Console.WriteLine(">>SCREENSHOT<<");
            Thread.Sleep(1 * 1000);
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void the_1_test()
        {
            Console.WriteLine("runnning 1");
            //Thread.Sleep(1 * 1000 * 180);
            Assert.IsTrue(true);
        }


        [TestMethod]
        public void the_22_test()
        {
            Console.WriteLine("runnning 2");
            Thread.Sleep(1 * 1000);
            Assert.IsTrue(true);
        }


        [TestMethod]
        public void the_5_test()
        {
            Console.WriteLine("runnning 3");
            Thread.Sleep(1 * 1000);
            Assert.IsTrue(false, "unit test exxxxxxception");
        }



        //[TestMethod]
        //public void the_1_test()
        //{
        //    Console.WriteLine("runnning 1");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_2_test()
        //{
        //    Console.WriteLine("runnning 2");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_3_test()
        //{
        //    Console.WriteLine("runnning 3");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_4_test()
        //{
        //    Console.WriteLine("runnning 4");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_5_test()
        //{
        //    Console.WriteLine("runnning 5");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_6_test()
        //{
        //    Console.WriteLine("runnning 6");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_7_test()
        //{
        //    Console.WriteLine("runnning 7");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_8_test()
        //{
        //    Console.WriteLine("runnning 8");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_9_test()
        //{
        //    Console.WriteLine("runnning 9");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_10_test()
        //{
        //    Console.WriteLine("runnning 10");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_11_test()
        //{
        //    Console.WriteLine("runnning 11");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_12_test()
        //{
        //    Console.WriteLine("runnning 12");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_13_test()
        //{
        //    Console.WriteLine("runnning 13");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_14_test()
        //{
        //    Console.WriteLine("runnning 14");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_15_test()
        //{
        //    Console.WriteLine("runnning 15");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_16_test()
        //{
        //    Console.WriteLine("runnning 16");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_17_test()
        //{
        //    Console.WriteLine("runnning 17");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_18_test()
        //{
        //    Console.WriteLine("runnning 18");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_19_test()
        //{
        //    Console.WriteLine("runnning 19");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_20_test()
        //{
        //    Console.WriteLine("runnning 20");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_21_test()
        //{
        //    Console.WriteLine("runnning 21");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_22_test()
        //{
        //    Console.WriteLine("runnning 22");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_23_test()
        //{
        //    Console.WriteLine("runnning 23");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_24_test()
        //{
        //    Console.WriteLine("runnning 24");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_25_test()
        //{
        //    Console.WriteLine("runnning 25");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_26_test()
        //{
        //    Console.WriteLine("runnning 26");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_27_test()
        //{
        //    Console.WriteLine("runnning 27");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_28_test()
        //{
        //    Console.WriteLine("runnning 28");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_29_test()
        //{
        //    Console.WriteLine("runnning 29");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_30_test()
        //{
        //    Console.WriteLine("runnning 30");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_31_test()
        //{
        //    Console.WriteLine("runnning 31");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_32_test()
        //{
        //    Console.WriteLine("runnning 32");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_33_test()
        //{
        //    Console.WriteLine("runnning 33");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_34_test()
        //{
        //    Console.WriteLine("runnning 34");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_35_test()
        //{
        //    Console.WriteLine("runnning 35");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_36_test()
        //{
        //    Console.WriteLine("runnning 36");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_37_test()
        //{
        //    Console.WriteLine("runnning 37");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_38_test()
        //{
        //    Console.WriteLine("runnning 38");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_39_test()
        //{
        //    Console.WriteLine("runnning 39");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_40_test()
        //{
        //    Console.WriteLine("runnning 40");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_41_test()
        //{
        //    Console.WriteLine("runnning 41");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_42_test()
        //{
        //    Console.WriteLine("runnning 42");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_43_test()
        //{
        //    Console.WriteLine("runnning 43");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_44_test()
        //{
        //    Console.WriteLine("runnning 44");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_45_test()
        //{
        //    Console.WriteLine("runnning 45");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_46_test()
        //{
        //    Console.WriteLine("runnning 46");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_47_test()
        //{
        //    Console.WriteLine("runnning 47");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_48_test()
        //{
        //    Console.WriteLine("runnning 48");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_49_test()
        //{
        //    Console.WriteLine("runnning 49");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_50_test()
        //{
        //    Console.WriteLine("runnning 50");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_51_test()
        //{
        //    Console.WriteLine("runnning 51");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_52_test()
        //{
        //    Console.WriteLine("runnning 52");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_53_test()
        //{
        //    Console.WriteLine("runnning 53");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_54_test()
        //{
        //    Console.WriteLine("runnning 54");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_55_test()
        //{
        //    Console.WriteLine("runnning 55");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_56_test()
        //{
        //    Console.WriteLine("runnning 56");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_57_test()
        //{
        //    Console.WriteLine("runnning 57");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_58_test()
        //{
        //    Console.WriteLine("runnning 58");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_59_test()
        //{
        //    Console.WriteLine("runnning 59");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_60_test()
        //{
        //    Console.WriteLine("runnning 60");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_61_test()
        //{
        //    Console.WriteLine("runnning 61");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_62_test()
        //{
        //    Console.WriteLine("runnning 62");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_63_test()
        //{
        //    Console.WriteLine("runnning 63");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_64_test()
        //{
        //    Console.WriteLine("runnning 64");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_65_test()
        //{
        //    Console.WriteLine("runnning 65");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_66_test()
        //{
        //    Console.WriteLine("runnning 66");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_67_test()
        //{
        //    Console.WriteLine("runnning 67");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_68_test()
        //{
        //    Console.WriteLine("runnning 68");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_69_test()
        //{
        //    Console.WriteLine("runnning 69");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_70_test()
        //{
        //    Console.WriteLine("runnning 70");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_71_test()
        //{
        //    Console.WriteLine("runnning 71");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_72_test()
        //{
        //    Console.WriteLine("runnning 72");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_73_test()
        //{
        //    Console.WriteLine("runnning 73");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_74_test()
        //{
        //    Console.WriteLine("runnning 74");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_75_test()
        //{
        //    Console.WriteLine("runnning 75");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_76_test()
        //{
        //    Console.WriteLine("runnning 76");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_77_test()
        //{
        //    Console.WriteLine("runnning 77");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_78_test()
        //{
        //    Console.WriteLine("runnning 78");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_79_test()
        //{
        //    Console.WriteLine("runnning 79");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_80_test()
        //{
        //    Console.WriteLine("runnning 80");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_81_test()
        //{
        //    Console.WriteLine("runnning 81");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_82_test()
        //{
        //    Console.WriteLine("runnning 82");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_83_test()
        //{
        //    Console.WriteLine("runnning 83");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_84_test()
        //{
        //    Console.WriteLine("runnning 84");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_85_test()
        //{
        //    Console.WriteLine("runnning 85");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_86_test()
        //{
        //    Console.WriteLine("runnning 86");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_87_test()
        //{
        //    Console.WriteLine("runnning 87");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_88_test()
        //{
        //    Console.WriteLine("runnning 88");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_89_test()
        //{
        //    Console.WriteLine("runnning 89");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_90_test()
        //{
        //    Console.WriteLine("runnning 90");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_91_test()
        //{
        //    Console.WriteLine("runnning 91");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_92_test()
        //{
        //    Console.WriteLine("runnning 92");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_93_test()
        //{
        //    Console.WriteLine("runnning 93");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_94_test()
        //{
        //    Console.WriteLine("runnning 94");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_95_test()
        //{
        //    Console.WriteLine("runnning 95");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_96_test()
        //{
        //    Console.WriteLine("runnning 96");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_97_test()
        //{
        //    Console.WriteLine("runnning 97");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_98_test()
        //{
        //    Console.WriteLine("runnning 98");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}


        //[TestMethod]
        //public void the_99_test()
        //{
        //    Console.WriteLine("runnning 99");
        //    Thread.Sleep(1 * 1000);
        //    Assert.IsTrue(true);
        //}




    }
}