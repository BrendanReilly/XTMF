/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace XTMFUpdateServer
{
    public partial class UpdateServer : ServiceBase
    {
        private bool Alive;
        private string Core32BitDirectory;
        private string CoreDirectory;
        private string ModelSystemDirectory;
        private string Module32BitDirectory;
        private string ModuleDirectory;
        private string CodeDirectory;
        private Thread ServerThread;
        private int SocketAddress;
        private string _saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XTMFUpdateServer");

        public UpdateServer()
        {
            
            if(!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
            using (StreamWriter writer = new StreamWriter(Path.Combine(_saveDirectory, "XTMFUpdateServerTest.txt")))
            {
                writer.WriteLine("Starting!");
                InitializeComponent();
                writer.WriteLine("Initialized components");
                if (!System.Diagnostics.EventLog.SourceExists("XTMFUpdateServer"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "XTMFUpdateServer", "InitializationLog");
                }
                writer.WriteLine("Built event log!");
                eventLog1.Source = "InitializationLog";
                writer.WriteLine("Finished Constructor!");
            }
        }

        private enum RequestTypes
        {
            None = 0,
            Code = -1,
            RequestCoreUpdate = 1,
            RequestModuleUpdate = 2,
            Request32BitCoreUpdate = 3,
            Request32BitModuleUpdate = 4
        }

        protected override void OnShutdown()
        {
            Alive = false;
            base.OnShutdown();
        }

        protected override void OnStart(string[] args)
        {
            using (StreamWriter writer = new StreamWriter(@"C:\XTMFUpdateServerTest2.txt"))
            {
                try
                {
                    writer.WriteLine("OnStart");
                    Alive = true;
                    eventLog1.WriteEntry("Starting XTMF Update Server");
                    eventLog1.WriteEntry("Loading Settings");
                    writer.WriteLine("About to load Settings");
                    LoadSettings();
                    writer.WriteLine("Loaded Settings");
                    eventLog1.WriteEntry("Spawning Listener");
                    SpawnListenner();
                    eventLog1.WriteEntry("Setup Complete!");
                    writer.WriteLine("Setup Complete");
                }
                catch (Exception e)
                {
                    writer.WriteLine(e.Message + "\r\n" + e.StackTrace);
                    Environment.Exit(1);
                }
            }
        }

        protected override void OnStop()
        {
        }

        private static void CreateConfigurationDirectory(string configFile)
        {
            var dir = Path.GetDirectoryName(configFile);
            if (dir == null)
            {
                throw new Exception($"Unable to get the directory from the path {configFile}!");
            }
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void SendFiles(BinaryWriter writer, string[] filesToSend, string baseDirName)
        {
            writer.Write(filesToSend.Length);
            var baesDirLength = baseDirName.Length;
            foreach (var file in filesToSend)
            {
                byte[] contents;
                var fileName = file.Substring(baesDirLength);
                if (fileName.Length != 0)
                {
                    if ((fileName[0] == '/') | (fileName[0] == '\\'))
                    {
                        fileName = fileName.Substring(1);
                    }
                }
                writer.Write(fileName);
                try
                {
                    contents = File.ReadAllBytes(file);
                    writer.Write(contents.Length);
                    writer.Write(contents);
                }
                catch
                {
                    writer.Write(4);
                    writer.Write(0);
                    continue;
                }
                writer.Flush();
            }
        }

        private void CreateDefaultConfiguration(string configFile)
        {
            CreateConfigurationDirectory(configFile);
            using (XmlWriter writer = XmlWriter.Create(configFile, new XmlWriterSettings() { Encoding = Encoding.Unicode }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Root");
                WriteElement(writer, "CoreDirectory", "D:/Documents/XTMF");
                WriteElement(writer, "ModuleDirectory", "D:/Documents/XTMF/Modules");
                WriteElement(writer, "Core32BitDirectory", "D:/Documents/XTMF32");
                WriteElement(writer, "Module32BitDirectory", "D:/Documents/XTMF32/Modules");
                WriteElement(writer, "ModelSystemDirectory", "D:/Documents/ModelSystems");
                WriteElement(writer, "SocketAddress", "1448");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private void LoadSettings()
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configFile = Path.Combine(dir, "XTMFUpdateServer", "Configuration.xml");
            if (!File.Exists(configFile))
            {
                CreateDefaultConfiguration(configFile);
            }
            XmlDocument doc = new XmlDocument();
            doc.Load(configFile);
            var root = doc["Root"];
            CoreDirectory = @"D:\Documents\XTMF";
            ModuleDirectory = @"D:\Documents\XTMF\Modules";
            Core32BitDirectory = @"D:\Documents\XTMF32";
            Module32BitDirectory = @"D:\Documents\XTMF32\Modules";
            ModelSystemDirectory = @"D:\Documents\ModelSystems";
            CodeDirectory = @"D:\Documents\XTMF\Code";
            SocketAddress = 1448;
            if (root != null)
            {
                UpdateParameter(ref CoreDirectory, root["CoreDirectory"]);
                UpdateParameter(ref ModuleDirectory, root["ModuleDirectory"]);
                UpdateParameter(ref Core32BitDirectory, root["Core32BitDirectory"]);
                UpdateParameter(ref Module32BitDirectory, root["Module32BitDirectory,"]);
                UpdateParameter(ref ModelSystemDirectory, root["ModelSystemDirectory"]);
                string socketAddress = null;
                UpdateParameter(ref socketAddress, root["SocketAddress"]);
                if (!int.TryParse(socketAddress, out SocketAddress))
                {
                    SocketAddress = 1448;
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            Task processClientTask = new Task(delegate
           {
               try
               {
                   using (var stream = client.GetStream())
                   {
                       BinaryReader binary = new BinaryReader(stream);
                       BinaryWriter writer = new BinaryWriter(stream);
                       bool done = false;
                       while (!done)
                       {
                           int requestType = binary.ReadInt32();

                           switch ((RequestTypes)requestType)
                           {
                               case RequestTypes.None:
                                   done = true;
                                   break;
                               case RequestTypes.Code:
                                   UpdateCode(writer);
                                   break;
                               case RequestTypes.RequestCoreUpdate:
                                   UpdateCore(writer);
                                   break;

                               case RequestTypes.RequestModuleUpdate:
                                   UpdateModules(writer);
                                   break;

                               case RequestTypes.Request32BitCoreUpdate:
                                   UpdateCore(writer, false);
                                   break;

                               case RequestTypes.Request32BitModuleUpdate:
                                   UpdateModules(writer, false);
                                   break;

                               default:
                                   done = true;
                                   break;
                           }
                       }
                   }
               }
               catch
               {
                    // ignored
                }
               try
               {
                   client.Close();
               }
               catch
               {
                    // ignored
                }
           });
            processClientTask.Start();
        }



        private void ServerCode()
        {
            //Setup our socket connection
            eventLog1.WriteEntry("About to create TCP Socket");
            TcpListener listener = new TcpListener(IPAddress.Any, SocketAddress);
            listener.Start(50);
            while (Alive)
            {
                listener.Server.Poll(1000000, SelectMode.SelectRead);
                while (listener.Pending())
                {
                    var client = listener.AcceptTcpClient();
                    ProcessClient(client);
                }
            }
            listener.Stop();
            eventLog1.WriteEntry("Stopped Listenning");
        }

        private void SpawnListenner()
        {
            ServerThread = new Thread(ServerCode);
            ServerThread.Start();
        }

        private void UpdateCore(BinaryWriter writer, bool sixtyFourBit = true)
        {
            var dir = sixtyFourBit ? CoreDirectory : Core32BitDirectory;
            var corefiles = Directory.GetFiles(dir);
            SendFiles(writer, corefiles, dir);
        }

        private void UpdateCode(BinaryWriter writer)
        {
            var files = new List<string>();
            GetSourceFiles(files, CodeDirectory);
            SendFiles(writer, files.ToArray(), CodeDirectory);
        }

        private void GetSourceFiles(List<string> filesToWriteTo, string startingDirectory)
        {
            // search through all child directories
            var dirs = Directory.EnumerateDirectories(startingDirectory);
            foreach (var dir in dirs)
            {
                GetSourceFiles(filesToWriteTo, dir);
            }
            // add all of the files in this directory
            var files = Directory.EnumerateFiles(startingDirectory);
            foreach (var file in files)
            {
                filesToWriteTo.Add(Path.Combine(startingDirectory, file));
            }
        }

        private void UpdateModules(BinaryWriter writer, bool sixtyFourBit = true)
        {
            var dir = sixtyFourBit ? ModuleDirectory : Module32BitDirectory;
            var modules = Directory.GetFiles(dir);
            SendFiles(writer, modules, dir);
        }

        private void UpdateParameter(ref string setting, XmlNode value)
        {
            if (value != null)
            {
                setting = value.InnerText;
            }
        }

        private void WriteElement(XmlWriter writer, string name, string innerText)
        {
            writer.WriteStartElement(name);
            writer.WriteString(innerText);
            writer.WriteEndElement();
        }
    }
}