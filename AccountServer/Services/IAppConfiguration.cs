﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccountServer.Services
{
    public interface IAppConfiguration
    {
        ushort LoginGatePort { get; set; }
        ushort AccountServerPort { get; set; }
    }
}
