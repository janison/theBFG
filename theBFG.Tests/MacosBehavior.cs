using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace theBFG.Tests
{
    [TestClass]
    public class MacosBehavior
    {

        [TestMethod]
        public void should_parse_macos_system_resources()
        {
            var cpuInfo = "CPU usage: 3.27% user, 14.75% sys, 81.96% idle";
            var memInfo = "PhysMem: 5807M used (1458M wired), 10G unused.";


        }
    }
}
