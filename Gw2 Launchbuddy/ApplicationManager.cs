﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;

namespace Gw2_Launchbuddy
{
    public class ProAccBinding
    {
        public Process pro { set; get; }
        public Account acc { set; get; }

        public ProAccBinding(Process tpro, Account tacc = null)
        {
            this.pro = tpro;
            this.acc = tacc;
        }
    }

    static class LaunchManager
    {
        private static List<int> nomutexpros = new List<int>();
        public static void Launch()
        {
            //Checking for existing Gw2 instances. Do not continue until closed.
            //if (Process.GetProcesses().ToList().Where(a => !nomutexpros.Contains(a.Id) && a.ProcessName == Regex.Replace(exename, @"\.exe(?=[^.]*$)", "", RegexOptions.IgnoreCase)).Any())
            if (!RegClients())
            {
                MessageBox.Show("At least one instance of Guild Wars is running that was not opened by LaunchBuddy. That instance will need to be closed.");
                return;
                //HandleManager.ClearMutex(exename, "AN-Mutex-Window-Guild Wars 2", ref nomutexpros);
            }

            if (Gw2_Launchbuddy.Properties.Settings.Default.useinstancegui)
            {
                if (Globals.Appmanager != null && Globals.Appmanager.Visibility == Visibility.Visible)
                {
                    Globals.Appmanager.Topmost = true;
                    Globals.Appmanager.Focus();
                    Globals.Appmanager.WindowState = WindowState.Normal;
                }
                else
                {
                    Globals.Appmanager.Show();
                }
            }

            //Launching the application with arguments
            if (AccountManager.Count > 0)
            {
                for (int i = 0; i <= AccountManager.Count - 1; i++)
                {
                    LaunchGW2(i);
                }
            }
            else
            {
                LaunchGW2();
            }

            GFXManager.RestoreDefault();

            //Launching AddOns
            try
            {
                AddOnManager.LaunchAll();
            }
            catch (Exception err)
            {
                MessageBox.Show("One or more AddOns could not be launched.\n" + err.Message);
            }
        }
        static bool UpdateRegClients(string created)
        {
            return RegClients(created);
        }
        static bool RegClients(string created = null)
        {
            var key = Globals.LBRegKey;
            List<string> listClients = new List<string>();
            try
            {
                listClients = ((string[])key.GetValue("Clients")).ToList();
            }
            catch (Exception e)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Reg key likely does not exist: " + e.Message);
#endif
            }
            var gw2Procs = Process.GetProcesses().ToList().Where(a => a.ProcessName == Regex.Replace(Globals.exename, @"\.exe(?=[^.]*$)", "", RegexOptions.IgnoreCase)).ToList().ConvertAll<string>(new Converter<Process, string>(ProcMD5));
            var running = gw2Procs.Count();
            var temp = listClients.Where(a => !gw2Procs.Contains(a)).ToList();
            foreach (var t in temp) listClients.Remove(t);
            if (created != null) listClients.Add(created);
            key.SetValue("Clients", listClients.ToArray(), Microsoft.Win32.RegistryValueKind.MultiString);
            var logged = listClients.Count();
            key.Close();
            return running == logged;
        }

        static void LaunchGW2(int? accnr = null)
        {
            try
            {
                ProcessStartInfo gw2proinfo = new ProcessStartInfo();
                gw2proinfo.FileName = Globals.exepath + Globals.exename;
                var selected = AccountManager.ToList().Skip(accnr ?? AccountManager.Count).SingleOrDefault();
                gw2proinfo.Arguments = selected.CommandLine();
                gw2proinfo.WorkingDirectory = Globals.exepath;
                Process gw2pro = new Process { StartInfo = gw2proinfo };
                if (accnr != null)
                {
                    Globals.LinkedAccs.Add(new ProAccBinding(gw2pro, AccountManager.ToList()[(int)accnr]));
                    GFXManager.UseGFX(AccountManager.ToList()[(int)accnr].ConfigurationPath);
                }
                else
                {
                    Account undefacc = new Account ("Acc Nr" + Globals.LinkedAccs.Count, null, null);
                    Globals.LinkedAccs.Add(new ProAccBinding(gw2pro, undefacc));
                }

                try
                {
                    gw2pro.Start();
                }
                catch (Exception err)
                {
                    System.Windows.MessageBox.Show("Could not launch Gw2. Invalid path?\n" + err.Message);
                }
                try
                {
                    HandleManager.ClearMutex(Globals.exename, "AN-Mutex-Window-Guild Wars 2", ref nomutexpros);
                    gw2pro.WaitForInputIdle(10000);
                    //Thread.Sleep(1000);
                    //Register the new client to prevent problems.
                    UpdateRegClients(ProcMD5(gw2pro));
                    Thread.Sleep(3000);
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message);
                }

                if (Properties.Settings.Default.use_reshade)
                {
                    try
                    {
                        ProcessStartInfo unlockerpro = new ProcessStartInfo();
                        unlockerpro.FileName = Globals.unlockerpath;
                        unlockerpro.WorkingDirectory = Path.GetDirectoryName(Globals.unlockerpath);
                        Process.Start(unlockerpro);
                    }
                    catch (Exception err)
                    {
                        MessageBox.Show("Could not launch ReshadeUnlocker. Invalid path?\n" + err.Message);
                    }
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        public static string ProcMD5(Process proc)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Start: " + proc.StartTime + " ID: " + proc.Id + " MD5: " + CalculateMD5(proc.StartTime.ToString() + proc.Id.ToString()));
#endif
            return CalculateMD5(proc.StartTime.ToString() + proc.Id.ToString());
        }

        public static string CalculateMD5(string input)
        {
            var md5 = MD5.Create();
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hash = md5.ComputeHash(inputBytes);

            var sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("X2"));

            return sb.ToString();
        }
    }
}
