﻿using GameServer.Networking;
using GameServer.Properties;
using System;
using System.Threading;
using System.Windows.Forms;

namespace GameServer
{

    internal static class Program
    {

        [STAThread]
        private static void Main()
        {
            using var myMutex = new Mutex(false, "LOMCN_GameServer_Mutex", out bool flag);

            if (flag)
            {
                Application.EnableVisualStyles();
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.SetCompatibleTextRenderingDefault(false);

                Config.SendPacketsAsync = Settings.Default.SendPacketsAsync;
                Config.DebugPackets = Settings.Default.DebugPackets;
                GamePacket.Config(typeof(SConnection));

                Application.Run(new MainForm());
                return;
            }

            MessageBox.Show("The server is already up and running");
            Environment.Exit(0);
        }
    }
}
